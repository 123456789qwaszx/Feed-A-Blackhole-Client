using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlackHole.Net
{
    public sealed class FakeHttpTransport : IHttpTransport
    {
        private readonly Queue<HttpResult> _responses = new();
        private readonly List<HttpRequest> _requests = new();

        // 지금까지 받은 요청 기록
        public IReadOnlyList<HttpRequest> Requests => _requests;

        public void EnqueueResponse(HttpResult response)
        {
            _responses.Enqueue(response);
        }

        public Task<HttpResult> SendAsync(HttpRequest request)
        {
            _requests.Add(request);

            // 설정된 응답이 없으면 네트워크 오류 반환
            if(_responses.Count == 0)
                return Task.FromResult(HttpResult.NetworkError("Fake 응답이 설정되지 않았습니다."));

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
