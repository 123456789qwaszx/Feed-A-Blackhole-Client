using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.Simulation
{
    // 시뮬레이션한 판 하나의 결과(결산까지 마친 뒤). 판의 기록(BattleRawData)에서 옮기고, 결산 전후의 진행 상태를 더한다.
    public sealed class BattleRecord
    {
        public int Seed { get; set; }
        public string Bot { get; set; }

        // 판을 시작할 때의 성장도와 결산 뒤의 성장도(이정표에 닿았으면 +1).
        public int Stage { get; set; }
        public int NextStage { get; set; }

        // 시작 Level, 목표 Level(다음 이정표, 없으면 0), 닿은 Level.
        public int StartLevel { get; set; }
        public int GoalLevel { get; set; }
        public int ReachedLevel { get; set; }
        public bool ReachedMilestone { get; set; }

        // 판이 제한 시간·이정표로 끝나지 않아 실행기의 상한에서 끊었는가(시간 추가가 흐르는 시간보다 빨랐다).
        public bool Capped { get; set; }

        // 판이 흐른 시간(초). 시간 추가·성장으로 늘어난 시간을 포함한다.
        public float PlayedSeconds { get; set; }

        // 판에서 번 Gold, 결산이 실제로 준 Gold(이정표 판은 목표 Gold까지 채운 차액), 결산 전후 잔액.
        public long EarnedGold { get; set; }
        public long SettledGold { get; set; }
        public long GoldBefore { get; set; }
        public long GoldAfter { get; set; }

        public Dictionary<EnemyType, int> Kills { get; } = new Dictionary<EnemyType, int>();
        public int TotalKills { get; set; }

        public double BreakerDamage { get; set; }
        public double ElectricAsteroidDamage { get; set; }
        public double ElectricStarDamage { get; set; }
        public double LaserDamage { get; set; }
        public double SupernovaDamage { get; set; }
        public int BreakerTicks { get; set; }
        public int CollectedMoons { get; set; }
        public int CollectedComets { get; set; }
        public float AddedSeconds { get; set; }

        // 이 판에서 번 EXP(시작 Level에 닿는 EXP부터 센 누적값. BattleRawData.Exp).
        public long Exp { get; set; }

        public int KillsOf(EnemyType type) => Kills.TryGetValue(type, out int count) ? count : 0;
    }
}
