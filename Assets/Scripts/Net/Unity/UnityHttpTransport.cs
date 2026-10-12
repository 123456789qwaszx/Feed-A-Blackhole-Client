using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace BlackHole.Net.Unity
{
    /// <summary>
    /// UnityWebRequest를 사용해 실제 HTTP 요청을 전송하는 클래스
    /// IHttpTransport를 구현하므로 호출하는 쪽에서
    /// 실제 전송 구현이 무엇인지 알 필요가 있다.
    /// </summary>
    public sealed class UnityHttpTransport : IHttpTransport
    {
        // 서버의 기본 주소 (ex http://localhost:8080)
        private readonly string _baseUrl;

        /// <summary>
        /// HTTP 요청에 사용할 기본 서버 주소를 설정한다.
        /// </summary>
        /// <param name="baseUrl">서버의 기본 URL</param>
        public UnityHttpTransport(string baseUrl)
        {
            // 서버 주소가 없으면 이후 요청을 정상적으로 구성할 수 없다.
            if (string.IsNullOrEmpty(baseUrl))
                throw new ArgumentException(
                    "baseUrl은 비어 있을 수 없습니다.",
                    nameof(baseUrl));

            // URL 끝의 '/'를 제거해 경로와 연결할 때
            // '//'가 중복으로 생기는 것을 방지한다.
            _baseUrl = baseUrl.TrimEnd('/');
        }

        /// <summary>
        /// HttpRequest에 담긴 설정을 사용해 HTTP요청을 전송한다.
        /// 서버 응답, 네트워크 오류, 타임아웃을 HttpResult로 반환한다.
        /// </summary>
        /// <param name="request">전송할 HTTP 요청 정보</param>
        /// <returns>요청 결과</returns>
        public async Task<HttpResult> SendAsync(HttpRequest request)
        {
            if (request == null) // 요청 객체가 없으면 URL이나 헤더를 구성할 수 없다.
                return HttpResult.NetworkError("요청이 Null 입니다.");

            try
            {
                // 기본 URL과 API 경로를 연결해 최종 요청 주소를 만든다. (ex "/api/v1/auth/guest/login")
                string url = _baseUrl + request.Path;

                // UnityWebRequest를 생성한다.
                // using을 사용해 요청이 끝난 뒤 관련 리소스를 정리한다.
                using var webRequest = new UnityWebRequest(url, request.Method)
                {
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = request.TimeoutSeconds
                };

                // JSON 본문이 있을 때만 업로드 핸들러와 Content-Type 설정
                // GET처럼 본문이 없는 요청에는 설정하지 않는다.
                if (request.JsonBody != null)
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(
                        Encoding.UTF8.GetBytes(request.JsonBody));

                    webRequest.SetRequestHeader(
                        "Content-Type",
                        "application/json");
                }

                // 액세스 토큰이 있으면 인증 헤더 설정
                if (!string.IsNullOrEmpty(request.BearerToken))
                {
                    webRequest.SetRequestHeader(
                        "Authorization",
                        "Bearer " + request.BearerToken);
                }

                // 비동기로 요청을 전송하고 완료될 때까지 기다린다.
                await webRequest.SendWebRequest();

                // 서버가 반환한 응답 본문과 HTTP 상태 코드를 읽는다.
                string body = webRequest.downloadHandler.text;
                long statusCode = webRequest.responseCode;

                // 타임아웃과 연결 오류 구분
                if (webRequest.result == UnityWebRequest.Result.ConnectionError ||
                    webRequest.result == UnityWebRequest.Result.DataProcessingError)
                {
                    string message = webRequest.error ?? "";

                    if (message.IndexOf(
                        "timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                        return HttpResult.Timeout(message);

                    return HttpResult.NetworkError(message);
                }

                // 2xx, 4xx, 5xx 모두 HTTP 응답으로 반환
                ApiError error = ParseApiError(body);

                return HttpResult.Response(statusCode, body, error);
            }
            catch (Exception ex)
            {
                // 전송 구현에서 예외를 외부로 전파하지 않는다.
                return HttpResult.NetworkError(ex.Message);
            }
        }

        /// <summary>
        /// 응답 본문을 서버 공통 오류 형식으로 파싱
        /// 형식이 다르거나 파싱이 안되면 null 반환
        /// </summary>
        /// <param name="body">서버에서 받은 응답 본문</param>
        /// <returns>파싱한 API</returns>
        private static ApiError ParseApiError(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;

            try
            {
                var envelope = JsonUtility.FromJson<ApiErrorEnvelope>(body);

                if (envelope?.error == null) return null;

                return new ApiError(envelope.error.code, envelope.error.message);
            }
            catch (Exception ex)
            {
                // 오류 형식이 아닌 응답 본문은 그대로 유지한다.
                return null;
            }
        }

        // JSON 최상위 객체의 error 필드를 받기 위한 래퍼 클래스.
        // JsonUtility는 최상위 필드를 기준으로 역직렬화하므로 필요하다.
        [Serializable]
        private sealed class ApiErrorEnvelope
        {
            public ApiErrorData error;
        }

        // JSON 내부 error 객체의 code와 message를 받는 클래스.
        [Serializable]
        private sealed class ApiErrorData
        {
            public string code;
            public string message;
        }
    }
}
