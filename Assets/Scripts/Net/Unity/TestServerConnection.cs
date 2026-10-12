using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using BlackHole.Net;

public class TestServerConnection : MonoBehaviour
{
        private async void Start()
        {
            var fake = new DebugFakeHttpTransport();

            // 응답 성공
            fake.EnqueueResponse(
                HttpResult.Response(200, "{\"token\":\"test-token\"}"));

            // 클라이언트 오류
            fake.EnqueueResponse(
                HttpResult.Response(400, "{\"error\":{\"code\":\"INVALID_REQUEST\",\"message\":\"잘못된 요청\"}}",
                new ApiError("INVALID_REQUEST", "잘못된 요청"))
                );

            // 3. 서버 오류
            fake.EnqueueResponse(
                HttpResult.Response(500, "Internal Server Error")
            );

            // 4. 타임아웃
            fake.EnqueueResponse(
                HttpResult.Timeout("요청 시간 초과")
            );

            // 5. 연결 실패
            fake.EnqueueResponse(
                HttpResult.NetworkError("연결 실패")
            );

            // 요청 5개를 보내고 결과 확인
            for (int i = 0; i < 5; i++)
            {
                var request = new HttpRequest(
                    HttpRequest.Post,
                    "/api/v1/auth/guest/login",
                    "{\"nickname\":\"Player01\"}",
                    null,
                    5
                );

                HttpResult result = await fake.SendAsync(request);

                Debug.Log(
                    $"[Fake 테스트 {i + 1}] " +
                    $"Outcome={result.Outcome}, " +
                    $"StatusCode={result.StatusCode}, " +
                    $"IsSuccess={result.IsSuccess}, " +
                    $"Reached={result.Reached}, " +
                    $"Error={result.Error?.Code}, " +
                    $"Message={result.TransportMessage}"
                );
            }

            // 요청 기록 확인
            Debug.Log($"기록된 요청 수: {fake.Requests.Count}");

            foreach (var request in fake.Requests)
            {
                Debug.Log(
                    $"요청: {request.Method} {request.Path}, " +
                    $"Body={request.JsonBody}"
                );
            }
        }

        // 에디터에서 동작을 확인하기 위한 임시 가짜 전송 클래스
        private sealed class DebugFakeHttpTransport : IHttpTransport
        {
            private readonly Queue<HttpResult> _responses = new();
            private readonly List<HttpRequest> _requests = new();

            public IReadOnlyList<HttpRequest> Requests => _requests;

            public void EnqueueResponse(HttpResult response)
            {
                _responses.Enqueue(response);
            }

            public Task<HttpResult> SendAsync(HttpRequest request)
            {
                _requests.Add(request);

                if (_responses.Count == 0)
                {
                    return Task.FromResult(
                        HttpResult.NetworkError(
                            "Fake 응답이 설정되지 않았습니다.")
                    );
                }

                return Task.FromResult(_responses.Dequeue());
            }
        }
    }
