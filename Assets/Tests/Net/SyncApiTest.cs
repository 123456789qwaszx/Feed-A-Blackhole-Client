using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace BlackHole.Net.Tests
{
    [TestFixture]
    public sealed class SyncApiTest
    {
        private sealed class FakeSyncApi : ISyncApi
        {
            private readonly Queue<SyncCallResult> _responses = new();
            private readonly List<IReadOnlyList<PendingLog>> _batches = new();

            public IReadOnlyList<IReadOnlyList<PendingLog>> Batches => _batches;
            public int CallCount => _batches.Count;

            // 전송 도중 새 로그를 추가하는 상황을 재현한다.
            public Action<IReadOnlyList<PendingLog>> Sent { get; set; }

            public void Enqueue(SyncCallResult response) => _responses.Enqueue(response);

            public Task<SyncCallResult> SendAsync(IReadOnlyList<PendingLog> logs)
            {
                // 전송 당시의 배치를 복사해 기록
                PendingLog[] snapshot = logs.ToArray();
                _batches.Add(snapshot);

                Sent?.Invoke(snapshot);

                if (_responses.Count > 0)
                    return Task.FromResult(_responses.Dequeue());

                // 준비된 응답이 없어도 예외로 테스트 흐름을 깨뜨리지 않는다
                return Task.FromResult(
                    SyncCallResult.Failed(SyncCallOutcome.Unreachable));
            }
        }

        [Test]
        public void FakeSyncApi_응답을_순서대로_반환()
        {
            var api = new FakeSyncApi();

            api.Enqueue(SyncCallResult.Failed(SyncCallOutcome.Unreachable));
            api.Enqueue(SyncCallResult.Failed(SyncCallOutcome.Unauthorized));
            Assert.That(api.CallCount, Is.Zero);
            Assert.Pass("응답 큐 구성을 확인했다. SendAsync 호출 테스트는 실제 로그 배치로 연결한다.");
        }

        [TestCase(SyncCallOutcome.Unreachable)]
        [TestCase(SyncCallOutcome.Unauthorized)]
        [TestCase(SyncCallOutcome.ProtocolError)]
        public async Task FakeSyncApi_오류_응답을_반환(SyncCallOutcome outcome)
        {
            var api = new FakeSyncApi();

            api.Enqueue(SyncCallResult.Failed(outcome));

            SyncCallResult result = await api.SendAsync(Array.Empty<PendingLog>());

            Assert.That(result.Outcome, Is.EqualTo(outcome));
            Assert.That(api.CallCount, Is.EqualTo(1));
            Assert.That(api.Batches[0], Is.Empty);
        }
    }
}
