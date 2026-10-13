using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using BlackHole.Net;
using BlackHole.Net.Unity;
using UnityEngine;

namespace BlackHole.DevTools
{
    // 로컬 서버를 상대로 W2-2 완료 조건을 확인한다.
    //   1) 첫 실행: 등록 → 로그인
    //   2) 재시작: 갱신만 (새 refresh 토큰 저장)
    //   3) 저장 전 종료 흉내: 옛 refresh 토큰으로 되돌림 → 갱신 거절(재사용 탐지) → 아이디/시크릿 로그인으로 복구
    // "재시작"은 AuthSession과 저장소를 새로 만들어 흉내 낸다 — 메모리 상태 없이 파일만 다음 실행으로 이어진다.
    // 진짜 진행 데이터를 건드리지 않도록 임시 폴더를 쓴다.
    public sealed class AuthLocalServerTest : MonoBehaviour
    {
        [SerializeField] string baseUrl = "http://localhost:8080";
        [SerializeField] bool runAllOnStart;

        string Folder => Path.Combine(Application.temporaryCachePath, "AuthLocalServerTest");

        readonly StringBuilder _log = new StringBuilder();
        Vector2 _scroll;
        bool _busy;
        int _failures;
        string _refreshBeforeLastRestart; // 3번에서 되돌릴, 재시작 전의 refresh 토큰

        // TODO: 실제 파일 저장소 클래스와 생성자에 맞춘다.
        ICredentialStore CreateStore() => new FileCredentialStore(Folder);

        void Start()
        {
            if (runAllOnStart) RunAll();
        }

        #region 시나리오

        [ContextMenu("전체 시나리오")] public void RunAll() => Run(RunAllAsync);
        [ContextMenu("0. 초기화")] public void ResetAccount() => Run(() => { ResetFolder(); return Task.CompletedTask; });
        [ContextMenu("1. 첫 실행")] public void FirstRun() => Run(FirstRunAsync);
        [ContextMenu("2. 재시작")] public void Restart() => Run(RestartAsync);
        [ContextMenu("3. 저장 전 종료 흉내")] public void CrashBeforeSave() => Run(CrashBeforeSaveAsync);

        async Task RunAllAsync()
        {
            _failures = 0;
            ResetFolder();
            await FirstRunAsync();
            await RestartAsync();
            await CrashBeforeSaveAsync();
            Log(_failures == 0 ? "\n=== 전체 통과 ===" : $"\n=== 실패 {_failures}건 ===");
        }

        // Reset()은 Unity 메시지(컴포넌트 추가 시 에디터에서 호출)라 이름을 피한다.
        void ResetFolder()
        {
            Header("0. 초기화");
            if (Directory.Exists(Folder)) Directory.Delete(Folder, recursive: true);
            Directory.CreateDirectory(Folder);
            _refreshBeforeLastRestart = null;
            Log("폴더 비움: " + Folder);
        }

        async Task FirstRunAsync()
        {
            Header("1. 첫 실행 — 등록 → 로그인");
            if (CreateStore().Load() != null) { Fail("계정 정보가 이미 있다. 초기화부터 실행한다."); return; }

            var (session, calls) = await LaunchAsync();

            Check(session.State == ConnectionState.Online, "Online");
            Check(!string.IsNullOrEmpty(session.AccessToken), "액세스 토큰이 있다");
            ExpectCalls(calls, ("register", true), ("login", true));

            var saved = CreateStore().Load();
            Check(!string.IsNullOrEmpty(saved?.GuestId) && !string.IsNullOrEmpty(saved?.GuestSecret), "아이디·시크릿이 저장됐다");
            Check(!string.IsNullOrEmpty(saved?.RefreshToken), "refresh 토큰이 저장됐다");
        }

        async Task RestartAsync()
        {
            Header("2. 재시작 — 갱신");
            var before = CreateStore().Load();
            if (string.IsNullOrEmpty(before?.RefreshToken)) { Fail("저장된 refresh 토큰이 없다. 1번부터 실행한다."); return; }

            var (session, calls) = await LaunchAsync();

            Check(session.State == ConnectionState.Online, "Online");
            ExpectCalls(calls, ("refresh", true));

            var after = CreateStore().Load();
            Check(after?.GuestId == before.GuestId, "계정은 그대로다");
            Check(!string.IsNullOrEmpty(after?.RefreshToken) && after.RefreshToken != before.RefreshToken,
                  "새 refresh 토큰이 저장됐다");

            _refreshBeforeLastRestart = before.RefreshToken;
        }

        async Task CrashBeforeSaveAsync()
        {
            Header("3. 저장 전 종료 흉내 — 재사용 탐지 → 로그인으로 복구");
            if (string.IsNullOrEmpty(_refreshBeforeLastRestart)) { Fail("되돌릴 토큰이 없다. 2번부터 실행한다."); return; }

            // 갱신 응답은 받았지만 새 토큰을 저장하기 전에 죽은 상황:
            // 파일에는 서버가 이미 회전시킨 옛 토큰이 남아 있다.
            var current = CreateStore().Load();
            CreateStore().Save(new GuestCredentials(current.GuestId, current.GuestSecret, _refreshBeforeLastRestart));
            Log("refresh 토큰을 재시작 전 값으로 되돌림: " + Short(_refreshBeforeLastRestart));

            var (session, calls) = await LaunchAsync();

            Check(session.State == ConnectionState.Online, "Online");
            ExpectCalls(calls, ("refresh", false), ("login", true));

            var after = CreateStore().Load();
            Check(after?.GuestId == current.GuestId, "같은 계정으로 복구됐다(새 계정 없음)");
            Check(!string.IsNullOrEmpty(after?.RefreshToken) && after.RefreshToken != _refreshBeforeLastRestart,
                  "새 refresh 토큰이 저장됐다");

            _refreshBeforeLastRestart = null;
        }

        // 앱 한 번의 실행: 저장소·전송·세션을 새로 만들고 StartAsync.
        async Task<(AuthSession, List<Call>)> LaunchAsync()
        {
            var transport = new RecordingTransport(new UnityHttpTransport(baseUrl), Log);
            var store = CreateStore();
            var loaded = store.Load();
            Log($"  저장소: {(loaded == null ? "없음" : $"guestId {Short(loaded.GuestId)}, refresh {Short(loaded.RefreshToken)}")}");

            // AuthSession 내부 로그도 화면에 보이게 한다.
            var session = new AuthSession(transport, new UnityJsonSerializer(), store, msg => Log("  " + msg));

            await session.StartAsync();

            Log($"→ {session.State}" +
                (session.State == ConnectionState.Offline ? $" ({session.OfflineReason})" : ""));
            return (session, transport.Calls);
        }
        #endregion

        #region 확인

        void ExpectCalls(List<Call> calls, params (string kind, bool success)[] expected)
        {
            bool same = calls.Count == expected.Length;
            for (int i = 0; same && i < expected.Length; i++)
                same = calls[i].Kind == expected[i].kind && calls[i].Success == expected[i].success;

            string Fmt(string k, bool ok) => k + (ok ? "(2xx)" : "(거절)");
            var want = new List<string>();
            foreach (var e in expected) want.Add(Fmt(e.kind, e.success));
            var got = new List<string>();
            foreach (var c in calls) got.Add(Fmt(c.Kind, c.Success));

            Check(same, $"요청 순서: 기대 [{string.Join(", ", want)}] / 실제 [{string.Join(", ", got)}]");
        }

        void Check(bool ok, string what)
        {
            if (ok) Log("  PASS  " + what);
            else Fail(what);
        }

        void Fail(string what)
        {
            _failures++;
            Log("  FAIL  " + what);
            Debug.LogError("[AuthLocalServerTest] " + what);
        }
        #endregion

        #region 실행·로그

        async void Run(Func<Task> scenario)
        {
            if (_busy) { Log("이미 실행 중"); return; }
            _busy = true;
            try { await scenario(); }
            catch (Exception e) { Fail("예외: " + e); }
            finally { _busy = false; }
        }

        void Header(string title) => Log("\n── " + title);

        void Log(string line)
        {
            _log.AppendLine(line);
            Debug.Log("[AuthLocalServerTest] " + line);
            _scroll.y = float.MaxValue;
        }

        // 로그에 토큰·시크릿 전체를 남기지 않는다.
        static string Short(string s) => string.IsNullOrEmpty(s) ? "(없음)" : s.Substring(0, Math.Min(6, s.Length)) + "…";

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));
            GUILayout.Label($"서버: {baseUrl}   폴더: {Folder}");

            GUI.enabled = !_busy;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("전체 시나리오")) RunAll();
            if (GUILayout.Button("0. 초기화")) ResetAccount();
            if (GUILayout.Button("1. 첫 실행")) FirstRun();
            if (GUILayout.Button("2. 재시작")) Restart();
            if (GUILayout.Button("3. 저장 전 종료")) CrashBeforeSave();
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label(_log.ToString());
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        #endregion

        #region 요청 기록

        struct Call
        {
            public string Kind;   // register / login / refresh
            public bool Success;  // 2xx
        }

        // 실제 전송을 감싸 어떤 요청이 나갔고 어떻게 끝났는지 남긴다.
        sealed class RecordingTransport : IHttpTransport
        {
            readonly IHttpTransport _inner;
            readonly Action<string> _log;
            public readonly List<Call> Calls = new List<Call>();

            public RecordingTransport(IHttpTransport inner, Action<string> log)
            {
                _inner = inner;
                _log = log;
            }

            public async Task<HttpResult> SendAsync(HttpRequest request)
            {
                _log($"  → {request.Method} {request.Path} (timeout {request.TimeoutSeconds}s)");
                HttpResult result;
                try
                {
                    result = await _inner.SendAsync(request);
                }
                catch (Exception e)
                {
                    _log("  전송 예외: " + e);
                    throw;
                }
                string kind = KindOf(request.Path);
                Calls.Add(new Call { Kind = kind, Success = result.IsSuccess });
                _log($"  {request.Method} {request.Path} → {Describe(result)}");
                return result;
            }

            // TODO: ApiPaths가 있으면 경로 비교로 바꾼다.
            static string KindOf(string path)
            {
                if (path.EndsWith("/register")) return "register";
                if (path.EndsWith("/login")) return "login";
                if (path.EndsWith("/refresh")) return "refresh";
                return path;
            }

            static string Describe(HttpResult r) =>
                r.Outcome == HttpOutcome.Response
                    ? $"{r.StatusCode}{(r.Error != null ? " " + r.Error.Code : "")}" // TODO: ApiError 코드 멤버 이름
                    : $"{r.Outcome} {r.TransportMessage}";
        }
        #endregion
    }
}
