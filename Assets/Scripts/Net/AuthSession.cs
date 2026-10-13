using System;
using System.Threading;
using System.Threading.Tasks;
using UnityPipeline.Microsoft.CodeAnalysis.Operations;

namespace BlackHole.Net
{
    public sealed class AuthSession : IAuthSession
    {
        public static readonly TimeSpan LoginBudget = TimeSpan.FromSeconds(8);
        const string InvalidCredentialCode = "INVALID_CREDENTIALS";

        readonly GuestAuthApi _api;
        readonly ICredentialStore _store;
        readonly Action<string> _log;
        readonly Func<DateTime> _now;
        readonly SynchronizationContext _mainThread;
        readonly object _gate = new object();

        GuestCredentials _creds; //저장소 내용의 메모리 사본 (실패해도 최신상태 유지)
        string _accessToken;
        Task<bool> _recoverInFlight; // 401 복구 단일 비행

        public ConnectionState State { get; private set; } = ConnectionState.Connecting;
        public OfflineReason OfflineReason { get; private set; } = OfflineReason.None;
        public string AccessToken => State == ConnectionState.Online ? _accessToken : null;
        public event Action<ConnectionState> StateChanged;


        // 메인 스레드에서 생성한다(StateChanged를 메인 스레드로 보내기 위해).
        // now는 테스트에서 시간 예산을 흉내 낼 때 넣는다.
        public AuthSession(IHttpTransport transport, IJsonSerializer json, ICredentialStore store,
            Action<string> log = null, Func<DateTime> now = null)
        {
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            if (json == null) throw new ArgumentNullException(nameof(json));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _api = new GuestAuthApi(transport, json);
            _log = log ?? (_ => { });
            _now = now ?? (() => DateTime.UtcNow);
            _mainThread = SynchronizationContext.Current;
        }

        #region 앱 시작
        public async Task StartAsync()
        {
            OfflineReason reason;
            try
            {
                _creds = _store.Load(); // 없음/깨짐 (백업처리)이면 null
                reason = await SignInAsync(allowRegister: true, deadline: _now() + LoginBudget);
            }
            catch (Exception ex)
            {
                _log("[Auth] 시작중 예외 : " + ex);
                reason = OfflineReason.ProtocolError; // 예상하지 못한 예외는 버그
            }

            if (reason == OfflineReason.None) SetOnline();
            else SetOffline(reason);
        }
        #endregion

        #region 앱 실행 401
        public Task<bool> RefreshAfterUnauthorizedAsync()
        {
            lock (_gate)
            {
                if (State != ConnectionState.Online) return Task.FromResult(false);

                // 진행 중인 복구가 있으면 같이 기다린다 → 갱신 요청은 하나만 나간다.
                // 완료된 Task는 재사용하지 않는다(지난 결과가 남지 않도록).
                if (_recoverInFlight != null && !_recoverInFlight.IsCompleted)
                    return _recoverInFlight;

                _recoverInFlight = RecoverAsync();
                return _recoverInFlight;
            }
        }

        async Task<bool> RecoverAsync()
        {
            OfflineReason reason;
            try
            {
                // 갱신 한번, 안되면 로그인 한번, 등록은 안함
                reason = await SignInAsync(allowRegister: false, deadline: _now() + LoginBudget);
            }
            catch (Exception ex)
            {
                _log("[Auth] 401 복구중 예외! " + ex);
                reason = OfflineReason.ProtocolError;
            }

            if (reason == OfflineReason.None)
                return State == ConnectionState.Online; // 그 사이 GoOffline 됐으면 false

            SetOffline(reason);
            return false;
        }

        public void GoOffline(OfflineReason reason) => SetOffline(reason);
        #endregion

        #region 흐름 본체

        // 성공이면 OfflineReason.None 실패면 왜 오프라인인지?
        async Task<OfflineReason> SignInAsync(bool allowRegister, DateTime deadline)
        {
            // 1. refresh 갱신
            string refreshToken = RefreshTokenOf(_creds);
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var r = await WithInBudget(deadline, t => _api.RefreshAsync(refreshToken, t));
                if (r.Kind == AuthResultKind.Success) return Accept(r);
                if (r.Kind == AuthResultKind.Unreachable) return r.UnreachableReason; // 계정 정보는 그래도 유지
                // 거절(만료, 재사용 탐지 등) → 아이디/시크릿 로그인으로.
                // 저장 전 종료로 refresh가 어긋난 경우도 여기서 로그인으로 복구된다.
            }

            // 2. 계정 정보가 없으면 등록
            if (!HasAccount(_creds))
            {
                if (!allowRegister) return OfflineReason.ProtocolError; // 온라인인데 계정이 없으면 버그

                var reg = await WithInBudget(deadline, t => _api.RegisterAsync(t));
                if (reg.Kind == AuthResultKind.Unreachable) return reg.UnreachableReason;
                if (reg.Kind != AuthResultKind.Success) return OfflineReason.ProtocolError; //등록 거절은 예상못한 4xx

                var registred = MakeCredentials(reg.GuestId, reg.Secret, refreshToken: null);
                // 로그인 전 아이디/시크릿을 저장. 저장 못한 계정은 진행하지 않음
                // 맞는 사유가 없어 Unreachable로 둔다 (다음 실행에 등록부터 다시 시도). 로그로 구분
                if (!TrySave(registred)) return OfflineReason.Unreachable;
                _creds = registred;
            }

            // 3. 아이디/시크릿 로그인
            var login = await WithInBudget(deadline, t =>
                _api.LoginAsync(GuestIdOf(_creds), SecretOf(_creds), t
            ));
            if (login.Kind == AuthResultKind.Success) return Accept(login);
            if (login.Kind == AuthResultKind.Unreachable) return login.UnreachableReason;

            // INVALID_CREDENTIALS먄, 새 계정을 만들지 않고 오프라인
            if (login.ErrorCode == InvalidCredentialCode) return OfflineReason.CredentialsRejected;
            return OfflineReason.ProtocolError; // 그외 4xx
        }

        // 새 refresh 토큰은 다른 일을 하기 전에 저장
        OfflineReason Accept(AuthResult r)
        {
            var updated = MakeCredentials(GuestIdOf(_creds), SecretOf(_creds), r.RefreshToken);
            _creds = updated; // 디스크 저장이 실패해도 이번 실행은 메모리 값으로 계속 진행
            if (!TrySave(updated))
                _log("[Auth] refresh 토큰 저장 실패. 다음 실행은 재사용 탐지 후 로그인으로 복구된다.");

            _accessToken = r.AccessToken;
            return OfflineReason.None;
        }

        bool TrySave(GuestCredentials creds)
        {
            try { _store.Save(creds); return true; }
            catch (Exception e) { _log("[Auth] 계정 정보 저장 실패 : " + e); return false; }
        }

        /*
         * 남은 예산을 요청 타임이웃(s)으로 넘어간다. 전송이 취소를 지원하지 않으므로
         * 요청마다 남은 시간 이상 걸리지 않게 하는 방식으로 예산을 지킨다.
         * 1초 미만이면 보내지 않는다 - UnityWebRequest에서 timeout 0은 무제한
         */
        Task<AuthResult> WithInBudget(DateTime deadline, Func<int, Task<AuthResult>> call)
        {
            double remaining = (deadline - _now()).TotalSeconds;
            int seconds = (int)Math.Floor((deadline - _now()).TotalSeconds);
            if (seconds < 1)
            {
                // 로그인 시간 예산 초과
                _log($"[Auth] 남은 예산 {remaining:F2}초 — 요청을 보내지 않고 오프라인");
                return Task.FromResult(AuthResult.Unreachable(OfflineReason.Unreachable));
            }
            return call(seconds);
        }
        #endregion

        #region Guest Credentials 접근 - 실제 필드 이름에 맞춰 여기만 고친다
        static string GuestIdOf(GuestCredentials c) => c?.GuestId;
        static string SecretOf(GuestCredentials c) => c?.GuestSecret;
        static string RefreshTokenOf(GuestCredentials c) => c?.RefreshToken;

        static bool HasAccount(GuestCredentials c) =>
            !string.IsNullOrEmpty(GuestIdOf(c)) && !string.IsNullOrEmpty(SecretOf(c));
        static GuestCredentials MakeCredentials(string guestId, string secret, string refreshToken) =>
            new GuestCredentials(guestId, secret, refreshToken);
        #endregion

        #region 상태
        private void SetOnline()
        {
            lock(_gate)
            {
                if (State != ConnectionState.Connecting) return; // 오프라인은 이번 실행동안 유지
                State = ConnectionState.Online;
            }
            Raise(ConnectionState.Online);
        }

        public void SetOffline(OfflineReason reason)
        {
            lock (_gate)
            {
                if (State == ConnectionState.Offline) return; // 처음 사유를 유지
                State = ConnectionState.Offline;
                OfflineReason = reason;
                _accessToken = null;
            }
            Raise(ConnectionState.Offline);
        }

        private void Raise(ConnectionState state)
        {
            if (_mainThread == null || SynchronizationContext.Current == _mainThread)
                StateChanged?.Invoke(state);
            else
                _mainThread.Post(_ => StateChanged?.Invoke(state), null);
        }
        #endregion
    }
}
