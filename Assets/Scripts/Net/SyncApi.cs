using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Codice.Client.Common.WebApi.Responses;

namespace BlackHole.Net
{

    /// <summary>
    /// 서버에 동기화 로그를 전송한다.
    /// HTTP 전송과 인증 세션은 외부에서 주입받는다.
    /// </summary>
    public sealed class SyncApi : ISyncApi
    {
        private const int RequestTimeoutSeconds = 10;

        private readonly IHttpTransport _transport;
        private readonly IAuthSession _authSession;
        private readonly IJsonSerializer _jsonSerializer;

        public SyncApi(
            IHttpTransport transport,
            IAuthSession authSession,
            IJsonSerializer jsonSerializer)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _authSession = authSession ?? throw new ArgumentNullException(nameof(authSession));
            _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
        }

        public async Task<SyncCallResult> SendAsync(IReadOnlyList<PendingLog> logs)
        {
            if (logs == null)
                return SyncCallResult.Failed(SyncCallOutcome.ProtocolError);

            try
            {
                // PendingLog에 저장된 JSON을 그대로 사용해 요청 본문을 만든다.
                var logJsons = new List<string>(logs.Count);

                foreach (PendingLog log in logs)
                {
                    if (log == null || string.IsNullOrEmpty(log.Json))
                        return SyncCallResult.Failed(SyncCallOutcome.ProtocolError);

                    logJsons.Add(log.Json);
                }

                // 로그의 JSON을 요청 본문으로 구성한다
                string body = ComposeRequestBody(logs);

                // 최초 요청. 401이면, 인증 갱신 후 한번만 재시도
                HttpResult result = await SendRequestAsync(body);

                // 네트워크 오류, 타임아웃, 5xx는 즉시 Unreachable
                if (IsUnreachable(result))
                {
                    _authSession.GoOffline(OfflineReason.Unreachable);
                    return SyncCallResult.Failed(SyncCallOutcome.Unreachable);
                }

                if (result.StatusCode == 401)
                {
                    bool refreshed = await _authSession.RefreshAfterUnauthorizedAsync();

                    if (!refreshed)
                    {
                        // 인증 세션에서 offline 상태를 처리
                        return SyncCallResult.Failed(SyncCallOutcome.Unauthorized);
                    }

                    // 같은 본문을 새 AccessToken으로 한 번 재전송
                    result = await SendRequestAsync(body);

                    if (result.Outcome != HttpOutcome.Response)
                    {
                        _authSession.GoOffline(OfflineReason.Unreachable);
                        return SyncCallResult.Failed(SyncCallOutcome.Unreachable);
                    }

                    if (result.StatusCode == 401)
                    {
                        _authSession.GoOffline(OfflineReason.Unauthorized);
                        return SyncCallResult.Failed(SyncCallOutcome.Unauthorized);
                    }

                }
                return ParseHttpResponse(result);
            }
            catch (Exception)
            {
                // ISyncApi 계약: 호출자에게 예외를 던지지 않는다.
                // JSON 구성·파싱 등의 오류는 프로토콜 오류로 취급한다.
                _authSession.GoOffline(OfflineReason.Unreachable);

                return SyncCallResult.Failed(SyncCallOutcome.ProtocolError);
            }
        }

        private Task<HttpResult> SendRequestAsync(string body)
        {
            var reqeust = new HttpRequest(
                HttpRequest.Post,
                ApiPaths.Sync,
                body,
                _authSession.AccessToken,
                RequestTimeoutSeconds);

            return _transport.SendAsync(reqeust);
        }

        private static bool IsUnreachable(HttpResult result)
        {
            if (result == null)
                return true;

            if (result.Outcome == HttpOutcome.NetworkError ||
                result.Outcome == HttpOutcome.Timeout)
            {
                return true;
            }

            if (result.Outcome != HttpOutcome.Response)
                return true;

            return result.StatusCode >= 500 &&
                   result.StatusCode <= 599;
        }

        private SyncCallResult HandleTransportFailure(HttpResult result)
        {
            if (result.Outcome == HttpOutcome.Timeout ||
                result.Outcome == HttpOutcome.NetworkError)
            {
                _authSession.GoOffline(OfflineReason.Unreachable);

                return ParseHttpResponse(result);
            }

            return ParseHttpResponse(result);
        }

        private SyncCallResult ParseHttpResponse(HttpResult result)
        {
            if (result == null)
                return SyncCallResult.Failed(SyncCallOutcome.ProtocolError);

            if (result.Outcome != HttpOutcome.Response)
            {
                _authSession.GoOffline(OfflineReason.Unreachable);
                return SyncCallResult.Failed(SyncCallOutcome.Unreachable);
            }

            long statusCode = result.StatusCode;

            // 서버 내부 오류
            if (statusCode >= 500 && statusCode <= 599)
            {
                _authSession.GoOffline(OfflineReason.Unreachable);
                return SyncCallResult.Failed(SyncCallOutcome.Unreachable);
            }

            // 요청 데이터가 잘못되었거나 요청 크기 제한 초과
            if (statusCode == 400 || statusCode == 413)
            {
                _authSession.GoOffline(OfflineReason.ProtocolError);
                return SyncCallResult.Failed(SyncCallOutcome.ProtocolError);
            }

            // 인증 실패
            // 최초 401은 SendAsync에서 토큰 갱신 후 재시도하도록 처리해야 한다.
            if (statusCode == 401)
            {
                _authSession.GoOffline(OfflineReason.Unauthorized);
                return SyncCallResult.Failed(SyncCallOutcome.Unauthorized);
            }

            // 동기화 API의 정상 응답은 HTTP 200만 허용
            if (statusCode != 200)
            {
                _authSession.GoOffline(OfflineReason.ProtocolError);
                return SyncCallResult.Failed(SyncCallOutcome.ProtocolError);
            }

            // 정상 응답 JSON 파싱
            SyncResponseDto response;

            try
            {
                response = _jsonSerializer.FromJson<SyncResponseDto>(result.Body);
            }
            catch (Exception)
            {
                _authSession.GoOffline(OfflineReason.ProtocolError);
                return SyncCallResult.Failed(SyncCallOutcome.ProtocolError);
            }

            // 응답 데이터 유효성 검증
            if (!IsValidResponse(response))
            {
                _authSession.GoOffline(OfflineReason.ProtocolError);
                return SyncCallResult.Failed(SyncCallOutcome.ProtocolError);
            }

            return SyncCallResult.Of(response);
        }

        private static bool IsValidResponse(SyncResponseDto response)
        {
            if (response == null) return false;
            if (response.appliedThroughSeq < 0) return false;
            if (response.rejected == null) return false;
            if (response.progress == null || response.progress.nodes == null) return false;

            return true;
        }

        private static string ComposeRequestBody(IReadOnlyList<PendingLog> logs)
        {
            var logJsons = new List<string>(logs.Count);

            foreach (PendingLog log in logs)
            {
                // PendingLog에 저장된 JSON 문자열을 가져와야 한다.
                // 실제 프로퍼티 이름에 맞게 수정할 것.
                logJsons.Add(log.Json);
            }

            return SyncJson.ComposeRequest(logJsons);
        }
    }
}
