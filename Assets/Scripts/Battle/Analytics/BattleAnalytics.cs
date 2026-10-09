using System;
using System.IO;
using BlackHole.Analytics;
using BlackHole.Analytics.Transport;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 판마다 전투 요약을 만들어 통계 서버로 보낸다. 화면 흐름(ScreenFlow)이 판의 시작·끝·포기를 알린다.
    // - 시작: 설치의 다음 판 번호를 정하고 시작 조건을 적어 둔다.
    // - 끝: 결과를 채워 큐에 넣고 보낸다. 닿지 않으면 큐에 남아 다음 기회(다음 판의 끝, 앱 시작·복귀)에 보낸다.
    // - 포기: 적어 둔 요약을 버린다. 끝나지 않은 판은 보내지 않는다(판 번호는 건너뛴다).
    //
    // 통계 때문에 게임이 멈추면 안 된다. 그래서 여기가 경계다: 요약을 만들다 난 예외는 여기서 로그로 남기고 그 판의 통계만 잃는다.
    // 보내기(AnalyticsSender)는 원래 예외를 던지지 않는다.
    internal sealed class BattleAnalytics
    {
        private const string FolderName = "analytics";
        private const string QueueFolderName = "queue";
        // 큐에 쌓아 둘 판의 수. 오래 오프라인이어도 이만큼은 남긴다.
        private const int QueueCapacity = 200;

        // 설정이 없거나 꺼져 있을 때. 보낼 곳이 없어 아무것도 하지 않는다.
        private static readonly BattleAnalytics Disabled = new(null, null, null, null);

        private readonly AnalyticsInstall _install;
        private readonly AnalyticsSender _sender;
        private readonly string _buildVersion;
        private readonly string _platform;
        // 시작해서 아직 끝나지 않은 판의 요약. 없으면 null이다.
        private BattleSummaryDto _pending;

        private BattleAnalytics(AnalyticsInstall install, AnalyticsSender sender, string buildVersion, string platform)
        {
            _install = install;
            _sender = sender;
            _buildVersion = buildVersion;
            _platform = platform;
        }

        private bool Enabled => _sender != null;

        // 통계 폴더(persistentDataPath/analytics)에 설치 정보와 큐를 둔다.
        // 준비하지 못하면(저장 공간 등) 이번 실행은 통계 없이 간다.
        public static BattleAnalytics Create(
            AnalyticsSettings settings,
            string persistentDataPath,
            string buildVersion,
            string platform)
        {
            if (settings == null || !settings.Enabled)
            {
                Debug.Log("[통계] 설정이 없거나 꺼져 있어 통계를 보내지 않는다.");
                return Disabled;
            }

            try
            {
                string directory = Path.Combine(persistentDataPath, FolderName);
                AnalyticsInstall install = AnalyticsInstall.Load(directory);
                AnalyticsQueue queue = new(Path.Combine(directory, QueueFolderName), QueueCapacity);
                AnalyticsClient client = new(settings.BaseUrl, settings.TimeoutSeconds);
                AnalyticsSender sender = new(queue, client);

                return new BattleAnalytics(install, sender, buildVersion, platform);
            }
            catch (Exception error)
            {
                Debug.LogError($"[통계] 준비하지 못해 이번 실행은 통계를 보내지 않는다: {error}");
                return Disabled;
            }
        }

        // 판을 막 시작했을 때(흐르기 전) 부른다.
        public void BattleStarted(GameSession session, ProgressState progress)
        {
            if (!Enabled)
                return;

            try
            {
                int battleIndex = _install.NextBattleIndex();
                _pending = BattleSummaryBuilder.Begin(
                    session,
                    progress,
                    _install.InstallId,
                    battleIndex,
                    _buildVersion,
                    _platform,
                    DateTime.UtcNow);
            }
            catch (Exception error)
            {
                _pending = null;
                Debug.LogError($"[통계] 판의 시작 조건을 적지 못해 이 판의 통계를 버린다: {error}");
            }
        }

        // 판이 끝나 결산했을 때 부른다. 시작 조건을 적지 못한 판(꺼져 있음, 실패)은 보내지 않는다.
        public void BattleEnded(BattleRawData raw)
        {
            if (_pending == null)
                return;

            BattleSummaryDto summary = _pending;
            _pending = null;

            try
            {
                BattleSummaryBuilder.Complete(summary, raw, DateTime.UtcNow);
            }
            catch (Exception error)
            {
                Debug.LogError($"[통계] 판의 결과를 적지 못해 이 판의 통계를 버린다: {error}");
                return;
            }

            _ = _sender.SendAsync(summary);
        }

        // 판을 포기했을 때 부른다.
        public void BattleAbandoned() => _pending = null;

        // 큐에 남은 통계를 보낸다. 앱을 켰을 때와 앱으로 돌아왔을 때 부른다.
        public void Flush()
        {
            if (!Enabled)
                return;

            _ = _sender.FlushAsync();
        }
    }
}
