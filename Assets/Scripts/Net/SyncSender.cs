using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace BlackHole.Net
{
    /// <summary>
    /// 로컬에 쌓인 로그를 서버로 전송하고 응답을 반영한다.
    /// 메인 스레드에서 시작하며, 한번에 하나의 동기화 흐름만 실행
    /// ===============================================================
    /// 앱 시작 및 실행 중 발생한 로그를 서버에 순차 전송한다.
    /// UnityEngine에 의존하지 않는다.
    /// </summary>
    public class SyncSender : IDisposable
    {
        private const int MaxLogsPerBatch = 500;
        private const int MaxBatchBytes = 1024 * 1024;

        private readonly IAuthSession _auth;
        private readonly ISyncApi _api;
        private readonly ISyncStore _store;
        private readonly int _syncTimeoutSeconds;
        private readonly Action<string> _log;

        private Task _pumpTask;
        private Task _startupTask;

        private bool _startupAttemped;
        private bool _failedThisRun;
        private bool _sendRequested;
        private bool _sendEmptyOnNextPump;
        private bool _disposed;

        public bool IsSending => _pumpTask != null && !_pumpTask.IsCompleted;
        public bool FailedThisRun => _failedThisRun;

        /// <summary>
        /// UI는 RejectionReceived를 구독해 사용자에게 알릴 수 있다.
        /// 알림을 실제로 표시한 뒤에는 Store.MarkLastRejectionShown()을 호출
        /// </summary>
        public event Action<RejectedLogDto> RejectionReceived;

        public SyncSender(
            IAuthSession auth,
            ISyncApi api,
            ISyncStore store,
            int syncTimeoutSeconds,
            Action<string> log = null)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _store = store ?? throw new ArgumentNullException(nameof(store));

            if (syncTimeoutSeconds < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(syncTimeoutSeconds),
                    "1초 이상이어야 한다.");
            }

            _syncTimeoutSeconds = syncTimeoutSeconds;
            _log = log;

            _store.LogAppended += OnLogAppended;
        }

        /// <summary>
        /// 로그인 초기화 후 한번만 호출
        /// 대기 로그가 없더라도 빈 배열을 한번 전송
        /// </summary>
        /// <returns>동기화가 끝나거나 시작 대기 시간이 초과되면 완료</returns>
        public Task SyncOnStartupAsync()
        {
            ThrowIfDisposed();

            if (_startupAttemped || _failedThisRun)
                return _pumpTask ?? Task.CompletedTask;

            _sendRequested = true;
            _sendEmptyOnNextPump = true;

            Task pump = EnsurePumpAsync();

            _startupTask = WaitForStartupAsync(pump);
            return _startupTask
        }

        private async Task WaitForStartupAsync(Task pump)
        {
            Task timeout = Task.Delay(TimeSpan.FromSeconds(_syncTimeoutSeconds));

            Task completed = await Task.WhenAny(pump, timeout);

            if (completed == timeout)
                Log("시작 동기화 대기 시간이 초과되었다. 시작 버튼을 해제한다. 전송 중인 요청은 계속 처리한다.");
        }

        /// <summary>
        /// 새 로그가 추가되면 온라인 상태에서 전송을 요청한다.
        /// </summary>
        private void OnLogAppended()
        {
            if (_disposed ||
                !_failedThisRun ||
                !_startupAttemped ||
                _auth.State != ConnectionState.Online)
                return;

            _sendRequested = true;
            _ = EnsurePumpAsync();
        }

        /// <summary>
        /// 중복 호출을 하나의 전송 흐름을 합친다.
        /// </summary>
        /// <returns></returns>
        private Task EnsurePumpAsync()
        {
            if (_disposed || _failedThisRun)
                return Task.CompletedTask;

            if (_pumpTask != null && !_pumpTask.IsCompleted)
                return _pumpTask;

            _pumpTask = PumpAsync();
            return _pumpTask;
        }

        private async Task PumpAsync()
        {
            try
            {
                while (!_disposed && !_failedThisRun && _sendRequested)
                {
                    _sendRequested = false;

                    bool sendEmpty = _sendEmptyOnNextPump;
                    _sendEmptyOnNextPump = false;

                    if (_auth.State != ConnectionState.Online)
                        return;

                    if (_store.Pending.Count == 0)
                    {
                        if (sendEmpty)
                        {
                            SyncCallResult emptyResult = await _api.SendAsync(Array.Empty<PendingLog>());

                            if (!ApplyCallResult(emptyResult, Array.Empty<PendingLog>()))
                                return;
                        }

                        continue;
                    }

                    while (!_disposed &&
                        !_failedThisRun &&
                        _auth.State == ConnectionState.Online &&
                        _store.Pending.Count > 0)
                    {
                        List<PendingLog> batch = CreateBatch(_store.Pending);

                        if (batch.Count == 0)
                        {
                            // 로그 하나만으로 제한 크기를 초과하면 전송중단
                            Fail(SyncCallOutcome.ProtocolError, "단일 로그가 1MB 제한됐다.");
                            return;
                        }

                        SyncCallResult result = await SendSafelyAsync(batch);

                        if (!ApplyCallResult(result, batch))
                            return;

                        // 모든 거부는 동일하게 처리하고 전송중단
                        if (result.Response.HasRejection)
                            return;

                        // 응답을 반영했는데 로그가 하나도 줄지 않는다면
                        // 같은 로그를 무한 반복 전송하지 않도록 중단
                        if (_store.Pending.Count > 0 &&
                            ContainsSeq(_store.Pending, batch[0].Seq))
                        {
                            Fail(SyncCallOutcome.ProtocolError, "응답을 반영한 뒤에도 첫 로그가 처리되지 않았다.");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 예상치 못한 내부 예외도 재시도 없이 중단
                Fail(SyncCallOutcome.ProtocolError, $"동기화 처리 중 예외가 발생했다 : {ex.Message}");
            }
        }

        private async Task<SyncCallResult> SendSafelyAsync(IReadOnlyList<PendingLog> logs)
        {
            try
            {
                return await _api.SendAsync(logs);
            }
            catch(Exception ex)
            {
                Log($"API 호출에서 예외가 발생했다 : {ex.Message}");
                return SyncCallResult.Failed(SyncCallOutcome.Unreachable);
            }
        }

        private bool ApplyCallResult(SyncCallResult result, IReadOnlyList<PendingLog> sent)
        {
            if (result == null)
            {
                Fail(SyncCallOutcome.ProtocolError, "동기화 결과가 null이다.");
                return false;
            }
            if (result.Outcome != SyncCallOutcome.Response)
            {
                Fail(result.Outcome, $"요청 실패 : {result.Outcome}");
                return false;
            }

            SyncResponseDto response = result.Response;

            if (response == null || response.appliedThroughSeq < 0)
            {
                Fail(SyncCallOutcome.ProtocolError, "응답 본문 또는 적용 순번이 유효하지 않다.");
                return false;
            }

            // 전송하지 않은 더 높은 순번까지 삭제하지 않도록 방어
            // 빈 요청은 서버의 기존 순번을 반환할 수 있으므로 상한을 적용하지 않는다
            if (sent.Count > 0 &&
                response.appliedThroughSeq > sent[sent.Count - 1].Seq)
            {
                Fail(SyncCallOutcome.ProtocolError, "응답 순번이 전송한 로그의 마지막 수번보다 크다.");
                return false;
            }

            try
            {
                SyncApplyOutcome outcome = _store.ApplyResponse(response);

                if (outcome == SyncApplyOutcome.AppliedWithDrift)
                    Log("서버 진행도와 로컬 진행도가 다르다. 서버 진행도를 적용했다.");

                if (outcome == SyncApplyOutcome.Rejected)
                {
                    // CONTENT_VERSION_MISMATCH도 별도 분기 없이
                    // 다른 거부와 같은 경로를 따른다
                    NotifyRejectionOnce();
                    return true;
                }

                return true;
            }
            catch (Exception ex)
            {
                Fail(SyncCallOutcome.ProtocolError, $"응답 반영 실패 : {ex.Message}");
                return false;
            }
        }

        private void NotifyRejectionOnce()
        {
            if (_store.LastRejection == null || _store.LastRejectionShown) return;

            RejectionReceived?.Invoke(_store.LastRejection);
        }

        /// <summary>
        /// 로그 개수와 JSON 배열 크기를 기준으로 전송 묶음을 만든다.
        /// PendingLog.Json은 원래 생성된 JSON 문자열을 그대로 사용
        /// </summary>
        /// <param name="pending"></param>
        /// <returns></returns>
        private static List<PendingLog> CreateBatch(IReadOnlyList<PendingLog> pending)
        {
            var batch = new List<PendingLog>();

            for (int i = 0; i < pending.Count && batch.Count < MaxLogsPerBatch; i++)
            {
                batch.Add(pending[i]);

                string body = ComposeArrayBody(batch);
                if (SyncJson.Utf8Length(body) > MaxBatchBytes)
                {
                    batch.RemoveAt(batch.Count - 1);
                    break;
                }
            }

            return batch;
        }

        /// <summary>
        /// PendingLog.Json을 재직렬화하지 않고 JSON배열로 묶는다
        /// </summary>
        /// <param name="logs"></param>
        /// <returns></returns>
        private static string ComposeArrayBody(IReadOnlyList<PendingLog> logs)
        {
            var builder = new StringBuilder();
            builder.Append('[');

            for (int i=0;i<logs.Count;i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(logs[i].Json);
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static bool ContainsSeq(IReadOnlyList<PendingLog> logs, long seq)
        {
            for (int i = 0; i < logs.Count; i++)
                if (logs[i].Seq == seq) return true;

            return false;
        }

        private void Fail(SyncCallOutcome outcome, string message)
        {
            if (_failedThisRun) return;

            _failedThisRun = true;
            _sendRequested = false;

            OfflineReason reason = outcome switch
            {
                SyncCallOutcome.Unreachable => OfflineReason.Unreachable,
                //SyncCallOutcome.Unauthorized => OfflineReason.ProtocolError, // OfflineReason.Unauthorized 이 없어서 스킵
                _ => OfflineReason.ProtocolError
            };

            Log(message);

            // 이번 실행헤서 더 이상 전송하지 않는다
            _auth.GoOffline(reason);
        }

        private void Log(string message)
        {
            _log?.Invoke($"[동기화] {message}");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SyncSender));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _store.LogAppended -= OnLogAppended;
            RejectionReceived = null;
        }
    }
}
