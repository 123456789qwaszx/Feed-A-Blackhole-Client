using System;
using BlackHole.Core;
using BlackHole.Net;

namespace BlackHole.Sync
{
    // 코어 객체로 동기화 로그를 만든다(계약 5.2, 5.5). Unity에 의존하지 않아 시뮬레이션 도구(net8.0)도 쓴다.
    // seq는 부르는 쪽(로그 저장소)이 정해 넘긴다. 만든 로그의 JSON 글자는 이후 바뀌지 않는다.
    //
    // 구매·새 게임은 여기서 완성했다. 판 시작·결산은 W3-2에서 RunStartInfo·BattleRawData(W3-1의 칸별 장부)로 채운다.
    public sealed class SyncLogFactory
    {
        private readonly IJsonSerializer _json;
        private readonly string _buildVersion;
        private readonly string _contentVersion;
        private readonly Func<DateTime> _utcNow;

        public SyncLogFactory(IJsonSerializer json, string buildVersion, string contentVersion, Func<DateTime> utcNow)
        {
            _json = json ?? throw new ArgumentNullException(nameof(json));
            _buildVersion = buildVersion ?? "";
            _contentVersion = contentVersion ?? "";
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        }

        // 노드 구매. rank는 구매 후 Rank.
        public PendingLog NodePurchase(long seq, string nodeId, int rank, long cost) =>
            Compose(seq, LogTypes.NodePurchase, new NodePurchasePayloadDto { nodeId = nodeId, rank = rank, cost = cost });

        // 새 게임.
        public PendingLog Reset(long seq) => Compose(seq, LogTypes.Reset, new ResetPayloadDto());

        // 판 시작. runId는 이 판의 소문자 UUID(battle-summary의 battleId와 같은 값). 시작 Gold는 판을 시작할 때의 진행도 값.
        public PendingLog RunStart(long seq, string runId, RunStartInfo start, long startGold) =>
            throw new NotImplementedException("W3-2에서 구현한다.");

        // 판 결산. runId는 판 시작 로그와 같은 값.
        public PendingLog Settlement(long seq, string runId, BattleRawData raw) =>
            throw new NotImplementedException("W3-2에서 구현한다(W3-1의 칸별 장부가 필요하다).");

        // 소문자 종류 id(계약 1.4): EnemyType 이름을 소문자로.
        public static string EnemyIdOf(EnemyType type) => type.ToString().ToLowerInvariant();

        private PendingLog Compose(long seq, string type, object payload)
        {
            string json = SyncJson.ComposeLog(
                seq, type, SyncJson.FormatUtc(_utcNow()), _buildVersion, _contentVersion, _json.ToJson(payload));
            return new PendingLog(seq, type, json);
        }
    }
}
