using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace BlackHole.Net.Unity
{
    public sealed class UnityHttpTransport : IHttpTransport
    {
        private readonly string _baseUrl;

        public UnityHttpTransport(string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl))
                throw new ArgumentException(
                    "baseUrl은 비어 있을 수 없습니다.",
                    nameof(baseUrl));

            _baseUrl = baseUrl.TrimEnd('/');
        }

        public async Task<HttpResult> SendAsync(HttpRequest request)
        {
            if (request == null)
                return HttpResult.NetworkError("요청이 Null 입니다.");

            try
            {
                string url = _baseUrl + request.Path;

                using var webRequest = new UnityWebRequest(url, request.Method)
                {
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = request.TimeoutSeconds
                };

                // JSON 본문이 있을 때만 업로드 핸들러와 Content-Type 설정
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

                await webRequest.SendWebRequest();

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

        [Serializable]
        private sealed class ApiErrorEnvelope
        {
            public ApiErrorData error;
        }

        [Serializable]
        private sealed class ApiErrorData
        {
            public string code;
            public string message;
        }
    }
}
