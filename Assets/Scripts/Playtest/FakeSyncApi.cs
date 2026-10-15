using BlackHole.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace BlackHole.Unity
{
    /// <summary>
    /// SyncSender 테스트용 가짜 API
    /// 실제 네트워크 요청 없이 전송 순서와 응답 처리를 검증
    /// </summary>
    public sealed class FakeSyncApi : ISyncApi
    {
        private readonly List<IReadOnlyList<PendingLog>> _sentBatches = new();
        private readonly Queue<SyncCallResult> _results = new();

        public IReadOnlyList<IReadOnlyList<PendingLog>> SentGatches => _sentBatches;

        public int CallCount => _sentBatches.Count;

        // 설정된 응답이 없을 때 호출할 기본 응답 생성기.
        // 비동기 지연 테스트에도 사용할 수 있다.
        public Func<IReadOnlyList<PendingLog>, Task<SyncCallResult>> SendHandler { get; set; }

        public void EnqueueResult(SyncCallResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            _results.Enqueue(result);
        }

        public Task<SyncCallResult> SendAsync(IReadOnlyList<PendingLog> logs)
        {
            if (logs == null) throw new ArgumentNullException(nameof(logs));

            // 호출 당시 로그 목록을 복사해 테스트 기록을 보여줌
            var snapshot = logs.ToArray();
            _sentBatches.Add(snapshot);

            if (SendHandler != null)
                return SendHandler(snapshot);

            if (_results.Count > 0)
                return Task.FromResult(_results.Dequeue());

            throw new InvalidOperationException("FakeSyncApi에 반환할 응답이 설정되지 않았다.");
        }
    }
}
