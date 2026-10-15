using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlackHole.Core;
using BlackHole.Net.Unity;
using BlackHole.Sync;
using BlackHole.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BlackHole.Net.Tests
{
    // 로컬 서버를 상대로 M1 시나리오를 본다. 서버를 켜 두고 테스트 러너에서 골라 돌린다(Explicit).
    //   1) 첫 실행(온라인): 등록·로그인 → 새 게임(RESET) → 판 시작·결산(구매할 Gold) → NODE_PURCHASE → 전송 → 진행도 반영
    //   2) 두 번째 실행(오프라인): 서버에 닿지 않음 → 계속 → NODE_PURCHASE를 저장에만 쌓는다
    //   3) 세 번째 실행(온라인): 갱신 로그인 → 저장에 남은 로그를 보낸다 → 진행도 반영 → 다음 실행에도 대기 로그가 없다
    //
    // 새 게임 직후 Gold는 0이라 구매가 NOT_ENOUGH_GOLD로 거부된다. 그래서 서버 샘플(계약 3.8)과 같은 판(시드 123456,
    // 소행성 20개, 20 Gold)의 판 시작·결산을 먼저 보낸다. 결산 → 구매 순서는 ScreenFlow와 같다(SettlementOrderTests).
    //
    // 동기화 전송(SyncSender·ISyncApi 구현, W3-4)이 아직 없어 /me/sync를 여기서 직접 보낸다. 장부·응답 반영은 실제 ProgressStore다.
    // "실행"은 세션·저장소·진행 상태를 새로 만들어 흉내 낸다. 파일만 다음 실행으로 이어진다.
    // 콘텐츠 버전은 빈 요청의 응답에서 서버 값을 받아 쓴다(게임의 콘텐츠 버전이 서버와 같은지는 이 테스트가 보지 않는다).
    // 끝나면 결과 표(마크다운)를 콘솔에 남긴다. 서버 주소는 환경 변수 BLACKHOLE_SERVER_URL, 없으면 http://localhost:8080.
    [Explicit("로컬 서버 필요")]
    [Category("LocalServer")]
    public sealed class SyncLocalServerTests
    {
        private const int RequestTimeout = 5;
        private const int LoginBudget = 8;
        private const int SyncTimeout = 10;

        // 연결을 거부하는 주소(discard 포트). 오프라인 실행을 흉내 낸다.
        private const string UnreachableUrl = "http://127.0.0.1:9";

        // 시작 노드와 그 이웃(NodeCatalog의 배치). 비용은 노드 표(CSV)에서 읽는다.
        private const string FirstNode = "timer-01";
        private const string SecondNode = "asteroid.count-01";

        // 서버 샘플의 첫 판: 소행성(색 등급 0, 크기 1) 20개 → 20 Gold, 20 EXP.
        private const int SampleSeed = 123456;
        private const int SampleKills = 20;
        private const long SampleGold = 20;
        private const float SampleSeconds = 12f;

        private static readonly EnemyDefinition Asteroid =
            new(EnemyType.Asteroid, 1f, 1f, 0f, new[] { new EnemyTier(1f, 1, 1) });

        private static string BaseUrl =>
            Environment.GetEnvironmentVariable("BLACKHOLE_SERVER_URL") is { Length: > 0 } url ? url : "http://localhost:8080";

        private static NodeContent _nodes;
        private static NodeTree _tree;
        private static HqGrowthDefinition _growth;

        private string _directory;
        private readonly List<string> _report = new();
        private string _contentVersion = "";

        [OneTimeSetUp]
        public void LoadContent()
        {
            string folder = Path.Combine(Application.dataPath, "Data", "NodeTable");
            string Read(string name) => File.ReadAllText(Path.Combine(folder, name + ".csv"), Encoding.UTF8);

            NodeContentLoadResult content = NodeContentCsv.Load(
                Read(NodeContentCsv.StatsTab), Read(NodeContentCsv.NodesTab),
                Read(NodeContentCsv.CostTab), Read(NodeContentCsv.EffectsTab));
            Assert.IsNotNull(content.Content, string.Join("\n", content.Diagnostics));
            _nodes = content.Content;

            // 이 시나리오가 사는 두 노드만 배치한 트리. 구매 규칙(Gold, 드러남, 다음 Rank 비용)은 실제 NodePurchase를 쓴다.
            var layout = new NodeTreeData();
            layout.Nodes.Add(new NodeData
            {
                Id = FirstNode,
                Start = true,
                Links = new List<string> { SecondNode }
            });
            layout.Nodes.Add(new NodeData { Id = SecondNode });
            NodeTreeLoadResult tree = NodeTreeLoader.Load(layout, _nodes);
            Assert.IsTrue(tree.Succeeded, string.Join("\n", tree.Diagnostics));
            _tree = tree.Tree;

            // 성장도는 0에서 움직이지 않는다. 저장 검사(성장도 범위)만 통과하면 된다.
            _growth = new HqGrowthDefinition(
                new long[] { 10, 25, 45 },
                new[] { new HqMilestone(2, 100, 1f), new HqMilestone(3, 200, 1.2f) });
        }

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "BlackHole.Sync.LocalServer", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _report.Clear();
            _contentVersion = "";
        }

        [TearDown]
        public void TearDown()
        {
            var text = new StringBuilder();
            text.AppendLine("[M1 로컬 서버 확인]");
            text.AppendLine($"- 일시(UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            text.AppendLine($"- 서버: {BaseUrl}");
            text.AppendLine($"- 콘텐츠 버전(서버): {(_contentVersion.Length > 0 ? _contentVersion : "(받지 못함)")}");
            text.AppendLine($"- 결과: {TestContext.CurrentContext.Result.Outcome.Status}");
            text.AppendLine();
            text.AppendLine("| 실행 | 단계 | 결과 |");
            text.AppendLine("|---|---|---|");
            foreach (string line in _report)
                text.AppendLine(line);
            Debug.Log(text.ToString());

            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [UnityTest]
        public IEnumerator M1_LoginSyncAndOfflineLogAppliedOnNextRun()
        {
            // ---------- 1) 첫 실행: 온라인 ----------
            var first = new Launch(BaseUrl, _directory, Debug.LogError);
            yield return TestTasks.Wait(first.Session.StartAsync(), 15);
            first.AssertOnline();
            Note(1, "로그인", "Online(등록 -> 로그인)");

            // 새 계정이라 서버 진행도가 비어 있다. 응답의 콘텐츠 버전을 로그에 싣는다.
            Task<SyncResponseDto> probe = SyncAsync(first, Array.Empty<PendingLog>());
            yield return TestTasks.Wait(probe, SyncTimeout + 5);
            Assert.AreEqual(0, probe.Result.progress.lastAppliedSeq, "새 계정의 서버 진행도는 비어 있어야 한다.");
            Assert.IsNotEmpty(probe.Result.contentVersion, "빈 요청의 응답에 콘텐츠 버전이 있어야 한다.");
            _contentVersion = probe.Result.contentVersion;
            SyncLogFactory logs = new(new UnityJsonSerializer(), Application.version, _contentVersion, () => DateTime.UtcNow);

            ProgressStore store = LoadStore(out ProgressState progress);
            store.StartNew(progress, seq => logs.Reset(seq));
            PlaySampleRun(store, progress, logs);
            Buy(store, progress, logs, FirstNode);

            CollectionAssert.AreEqual(
                new[] { LogTypes.Reset, LogTypes.RunStart, LogTypes.Settlement, LogTypes.NodePurchase },
                store.Pending.Select(l => l.Type).ToArray());

            Task<SyncResponseDto> firstSync = SyncAsync(first, store.Pending);
            yield return TestTasks.Wait(firstSync, SyncTimeout + 5);
            SyncResponseDto firstResponse = firstSync.Result;
            AssertAppliedThrough(firstResponse, 4);
            Assert.AreEqual(SyncApplyOutcome.Applied, store.ApplyResponse(firstResponse), "로컬과 서버 진행도가 같아야 한다(덮어쓰기 없음).");
            Assert.IsEmpty(store.Pending, "반영된 로그는 장부에서 지워진다.");
            AssertSameProgress(progress, firstResponse.progress);
            Note(1, "RESET·RUN_START·SETTLEMENT·NODE_PURCHASE 전송", $"appliedThroughSeq {firstResponse.appliedThroughSeq}, 거부 없음");
            Note(1, "진행도 반영", Describe(firstResponse.progress));

            GuestCredentials credentials = new FileCredentialStore(_directory).Load();

            // ---------- 2) 두 번째 실행: 오프라인 ----------
            // 오프라인 경로의 오류 로그가 테스트를 깨지 않도록 경고로 남긴다.
            var offline = new Launch(UnreachableUrl, _directory, Debug.LogWarning);
            yield return TestTasks.Wait(offline.Session.StartAsync(), 15);
            Assert.AreEqual(ConnectionState.Offline, offline.Session.State);
            Assert.AreEqual(OfflineReason.Unreachable, offline.Session.OfflineReason);
            Note(2, "로그인", $"Offline({offline.Session.OfflineReason})");

            ProgressStore offlineStore = LoadStore(out ProgressState offlineProgress);
            Assert.IsEmpty(offlineStore.Pending);
            offlineStore.Continue(offlineProgress);
            Assert.AreEqual(firstResponse.progress.gold, offlineProgress.Gold, "계속하면 첫 실행의 진행도로 이어진다.");

            Buy(offlineStore, offlineProgress, logs, SecondNode);
            Assert.AreEqual(1, offlineStore.Pending.Count);
            Assert.AreEqual(5, offlineStore.Pending[0].Seq, "다음 seq는 서버가 반영한 seq 다음이다.");
            long goldAfterOffline = offlineProgress.Gold;
            Note(2, "NODE_PURCHASE 저장(전송 없음)", $"대기 로그 seq {offlineStore.Pending[0].Seq}, 로컬 Gold {goldAfterOffline}");

            GuestCredentials afterOffline = new FileCredentialStore(_directory).Load();
            Assert.AreEqual(credentials.GuestId, afterOffline.GuestId, "오프라인 실행은 계정 정보를 바꾸지 않는다.");
            Assert.AreEqual(credentials.RefreshToken, afterOffline.RefreshToken, "오프라인 실행은 계정 정보를 바꾸지 않는다.");

            // ---------- 3) 세 번째 실행: 다시 온라인 ----------
            var third = new Launch(BaseUrl, _directory, Debug.LogError);
            yield return TestTasks.Wait(third.Session.StartAsync(), 15);
            third.AssertOnline();
            Note(3, "로그인", "Online(갱신)");

            // 타이틀(새 게임·계속을 고르기 전)에서 보낸다. 장부는 저장 파일에서 불러온 것이다.
            ProgressStore thirdStore = LoadStore(out ProgressState thirdProgress);
            Assert.AreEqual(1, thirdStore.Pending.Count, "오프라인에서 쌓은 로그가 다음 실행의 장부에 남아 있어야 한다.");
            Assert.AreEqual(5, thirdStore.Pending[0].Seq);
            Assert.AreEqual(LogTypes.NodePurchase, thirdStore.Pending[0].Type);

            Task<SyncResponseDto> thirdSync = SyncAsync(third, thirdStore.Pending);
            yield return TestTasks.Wait(thirdSync, SyncTimeout + 5);
            SyncResponseDto thirdResponse = thirdSync.Result;
            AssertAppliedThrough(thirdResponse, 5);
            Assert.AreEqual(SyncApplyOutcome.Applied, thirdStore.ApplyResponse(thirdResponse), "로컬과 서버 진행도가 같아야 한다(덮어쓰기 없음).");
            Assert.IsEmpty(thirdStore.Pending);
            Assert.AreEqual(goldAfterOffline, thirdResponse.progress.gold, "서버 Gold가 오프라인 구매 뒤의 로컬 Gold와 같아야 한다.");
            Assert.IsTrue(thirdResponse.progress.nodes.Any(n => n.nodeId == SecondNode && n.rank == 1), $"서버 진행도에 {SecondNode} Rank 1이 있어야 한다.");
            Note(3, "오프라인 로그 전송", $"appliedThroughSeq {thirdResponse.appliedThroughSeq}, 거부 없음");
            Note(3, "진행도 반영", Describe(thirdResponse.progress));

            thirdStore.Continue(thirdProgress);
            AssertSameProgress(thirdProgress, thirdResponse.progress);

            // 다음 실행에도 대기 로그가 남지 않는다(반영 결과를 파일에 썼다).
            ProgressStore fourthStore = LoadStore(out ProgressState fourthProgress);
            Assert.IsEmpty(fourthStore.Pending, "반영한 뒤 다음 실행의 장부는 비어 있어야 한다.");
            fourthStore.Continue(fourthProgress);
            AssertSameProgress(fourthProgress, thirdResponse.progress);
            Note(4, "다시 불러오기", "대기 로그 0, 저장 진행도 = 서버 진행도");
        }

        // ---------- 흐름(ScreenFlow와 같은 순서) ----------
        private ProgressStore LoadStore(out ProgressState progress)
        {
            progress = new ProgressState();
            return ProgressStore.Load(_directory, _nodes, _growth, progress);
        }

        // ScreenFlow.Battle: 판 시작 로그 → (판) → 결산이 진행도에 Gold를 더함 → 결산 로그.
        private static void PlaySampleRun(ProgressStore store, ProgressState progress, SyncLogFactory logs)
        {
            string runId = Guid.NewGuid().ToString();
            long startGold = progress.Gold;
            var start = new RunStartInfo(SampleSeed, progress.GrowthStage, 0, SampleSeconds,
                new[] { new SupplyRequest(Asteroid, SampleKills) });
            store.Commit(seq => logs.RunStart(seq, runId, start, startGold));

            progress.EarnGold(SampleGold);

            var raw = new BattleRawData(SampleSeed, SampleSeconds, Array.Empty<EnemyKillCount>(),
                new[] { new KillBucketCount(new KillBucket(Asteroid, 0, 1, false), SampleKills) },
                0, 0, 0, SampleGold, 0, SampleKills, progress.GrowthStage, progress.GrowthStage, null, SampleGold, null);
            store.Commit(seq => logs.Settlement(seq, runId, raw));
        }

        // ScreenFlow.NodeTreeView.HandleNodeTreeNodeClicked: 비용은 사기 전에 읽고, rank는 산 뒤의 값.
        private static void Buy(ProgressStore store, ProgressState progress, SyncLogFactory logs, string nodeId)
        {
            Assert.IsTrue(NodePurchase.TryGetNextCost(progress, _tree, nodeId, out long cost), $"{nodeId}의 다음 Rank가 없다.");
            PurchaseResult result = NodePurchase.TryPurchase(progress, _tree, nodeId);
            Assert.AreEqual(PurchaseResult.Purchased, result, $"{nodeId}를 살 수 없다(Gold {progress.Gold}, 비용 {cost}).");

            int rank = progress.RankOf(nodeId);
            store.Commit(seq => logs.NodePurchase(seq, nodeId, rank, cost));
        }

        // ---------- 전송 ----------

        // /me/sync 하나. 401이면 갱신 후 한 번 다시 보낸다(ISyncApi 계약과 같다).
        private static async Task<SyncResponseDto> SyncAsync(Launch launch, IReadOnlyList<PendingLog> logs)
        {
            string body = SyncJson.ComposeRequest(logs.Select(log => log.Json));
            HttpResult result = await Send(launch, body);

            if (result.Outcome == HttpOutcome.Response && result.StatusCode == 401 &&
                await launch.Session.RefreshAfterUnauthorizedAsync())
                result = await Send(launch, body);

            Assert.IsTrue(result.IsSuccess,
                $"동기화 요청 실패 : {result.Outcome} {result.StatusCode} {result.Error?.Code} {result.Error?.Message} {result.TransportMessage}");

            SyncResponseDto response = new UnityJsonSerializer().FromJson<SyncResponseDto>(result.Body);
            Assert.IsNotNull(response, "동기화 응답 본문을 읽지 못했다. " + result.Body);
            return response;
        }

        private static Task<HttpResult> Send(Launch launch, string body) =>
            launch.Http.SendAsync(new HttpRequest(HttpRequest.Post, ApiPaths.Sync, body, launch.Session.AccessToken, SyncTimeout));

        // ---------- 검사·기록 ----------

        private static void AssertAppliedThrough(SyncResponseDto response, long seq)
        {
            Assert.IsFalse(response.HasRejection,
                $"seq {response.rejected?.seq} {response.rejected?.type} 거부 : {response.rejected?.code} {response.rejected?.message}");
            Assert.AreEqual(seq, response.progress.lastAppliedSeq);
        }

        private static void AssertSameProgress(ProgressState local, ProgressDto server) =>
            Assert.IsTrue(ServerProgress.Matches(local, server),
                $"로컬 (Gold {local.Gold}, " +
                $"성장도 {local.GrowthStage}, " +
                $"노드 {string.Join(", ", local.OwnedNodes.Select(id => $"{id}:{local.RankOf(id)}"))}과 " +
                $"서버 ({Describe(server)})가 다르다.");

        private static string Describe(ProgressDto progress) =>
            $"Gold {progress.gold}, " +
            $"성장도 {progress.growthStage}, " +
            $"노드 [{string.Join(", ", progress.nodes.Select(n => $"{n.nodeId}:{n.rank}"))}], " +
            $"lastAppliedSeq {progress.lastAppliedSeq}";

        private void Note(int launch, string step, string result) => _report.Add($"| {launch} | {step} | {result} |");

        // 앱 한 번의 실행: 전송·세션을 새로 만든다. 계정 정보는 같은 폴더의 파일로 이어진다.
        private sealed class Launch
        {
            public readonly IHttpTransport Http;
            public readonly AuthSession Session;
            private readonly string _baseUrl;

            public Launch(string baseUrl, string directory, Action<string> logError)
            {
                _baseUrl = baseUrl;
                Http = new UnityHttpTransport(baseUrl);
                Session = new AuthSession(Http, new UnityJsonSerializer(), new FileCredentialStore(directory),
                    RequestTimeout, LoginBudget, Debug.Log, logError);
            }

            public void AssertOnline() =>
                Assert.AreEqual(ConnectionState.Online, Session.State,
                    $"{Session.OfflineReason}. {_baseUrl}에 서버가 켜져 있는지 본다.");
        }
    }
}
