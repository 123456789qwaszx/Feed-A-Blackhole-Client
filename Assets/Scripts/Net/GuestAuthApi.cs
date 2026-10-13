using System;
using System.Threading.Tasks;

namespace BlackHole.Net
{
    // Unreachable: 흐름을 멈추고 오프라인(사유는 UnreachableReason). Rejected: 흐름이 코드를 보고 다음 단계를 정한다.
    internal enum AuthResultKind { Success, Rejected, Unreachable }

    // 인증 요청 하나의 결과. AuthSession은 이 세 갈래만 본다.
    internal sealed class AuthResult
    {
        public AuthResultKind Kind { get; private set; }
        public string ErrorCode { get; private set; } // Rejected : 서버 오류
        public OfflineReason UnreachableReason { get; private set; } // Unreachable : 사유
        public string GuestId { get; private set; } // 등록 응답
        public string Secret { get; private set; }
        public string AccessToken { get; private set; } // 로그인 갱신/응답
        public string RefreshToken { get; private set; }

        public static AuthResult Registered(string guestId, string secret) =>
            new AuthResult { Kind = AuthResultKind.Success, GuestId = guestId, Secret = secret };
        public static AuthResult Tokens(string access, string refresh) =>
            new AuthResult { Kind = AuthResultKind.Success, AccessToken = access, RefreshToken = refresh };
        public static AuthResult Rejected(string code) =>
            new AuthResult { Kind = AuthResultKind.Rejected, ErrorCode = code };
        public static AuthResult Unreachable(OfflineReason reason) =>
            new AuthResult { Kind = AuthResultKind.Unreachable, UnreachableReason = reason };
    }

    // API 명세 "인증"의 세 요청. HttpResult를 성공 / 거절 / 서버에 닿지 않음으로 나눈다.
    internal sealed class GuestAuthApi
    {
        const string RegisterPath = "/api/v1/auth/guest/register";
        const string LoginPath = "/api/v1/auth/guest/login";
        const string RefreshPath = "/api/v1/auth/refresh";

        readonly IHttpTransport _transport;
        readonly IJsonSerializer _json;

        public GuestAuthApi(IHttpTransport transport, IJsonSerializer json)
        {
            _transport = transport;
            _json = json;
        }

        public Task<AuthResult> RegisterAsync(int timeoutSeconds) =>
            PostAsync<RegisterResponse>(RegisterPath, "{}", timeoutSeconds,
                d => Has(d.guestId) && Has(d.secret) ? AuthResult.Registered(d.guestId, d.secret) : null);

        public Task<AuthResult> LoginAsync(string guestId, string secret, int timeoutSeconds) =>
            PostAsync<TokenResponse>(LoginPath,
                _json.ToJson(new LoginRequest { guestId = guestId, secret = secret }),
                timeoutSeconds, ToTokens);

        public Task<AuthResult> RefreshAsync(string refreshToken, int timeoutSeconds) =>
            PostAsync<TokenResponse>(RefreshPath,
                _json.ToJson(new RefreshRequest { refreshToken = refreshToken }),
                timeoutSeconds, ToTokens);

        async Task<AuthResult> PostAsync<TResponse>(string path, string jsonBody, int timeoutSeconds, Func<TResponse, AuthResult> onOk) where TResponse : class
        {
            // 인증 요청은 토큰 없이 보낸다(bearerToken: null).
            // timeoutSeconds는 AuthSession.WithinBudget이 1 이상만 넘긴다 — 생성자의 1초 이상 조건과 맞는다.
            var request = new HttpRequest(HttpRequest.Post, path, jsonBody, bearerToken: null, timeoutSeconds: timeoutSeconds);

            HttpResult r = await _transport.SendAsync(request);


            // 실제 멤버에 맞추기 (타임아웃, 네트워크에러, 응답 구분)
            // 네트워크 오류, 타임아웃, 5xx -> Unreachable
            if(!r.Reached) return AuthResult.Unreachable(OfflineReason.Unreachable);

            if (r.IsSuccess)
            {
                var dto = _json.FromJson<TResponse>(r.Body);
                var ok = dto == null ? null : onOk(dto);
                // 2xx인데 형식이 맞지 않는 본문 → ProtocolError. 흐름을 멈추고 계정 정보를 건드리지 않는다.
                return ok ?? AuthResult.Unreachable(OfflineReason.ProtocolError);
            }

            // 서버가 답했지만 2xx가 아님(4xx 등) → 거절. 코드를 보고 다음 단계는 AuthSession이 정한다.
            // 오류 본문이 아니면 Error가 null이라 코드도 null — AuthSession에서 ProtocolError로 끝난다.
            // TODO: ApiError의 코드 멤버 이름 확인
            return AuthResult.Rejected(r.Error?.Code);
        }

        static AuthResult ToTokens(TokenResponse d) =>
            Has(d.accessToken) && Has(d.refreshToken) ? AuthResult.Tokens(d.accessToken, d.refreshToken) : null;
        static bool Has(string s) => !string.IsNullOrEmpty(s);

        #region DTO (JsonUtility 규칙: [Serializable] + public 필드). 필드 이름은 API 명세에 맞춘다.
        [Serializable] sealed class LoginRequest { public string guestId; public string secret; }
        [Serializable] sealed class RefreshRequest { public string refreshToken; }
        [Serializable] sealed class RegisterResponse { public string guestId; public string secret; }
        [Serializable] sealed class TokenResponse { public string accessToken; public string refreshToken; }
        #endregion
    }
}
