#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using BlackHole.Analytics;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 세팅으로 판을 사람 없이 돌려 본 흐름(M4 AI 묶음). AI가 Unity를 돌릴 수 없어 숫자를 미리 만들어 둔다.
    // 게임과 같은 길(ScenarioRunner → StatsFor → GameSessionFactory → Begin → 시작 Level 채우기)로 판을 만들고,
    // 조준점을 한 곳(Aim)에 고정한 채 1/60초씩 진행하며 Every초마다 표본을 뜬다. 판이 끝나거나 MaxSeconds가 되면 멈춘다.
    // 사람 플레이와 같지 않다. 같은 세팅·시드로 원본과 초안을 비교하는 기준선으로 쓴다(실제 판 상태는 메모의 battle).
    internal static class PlaytestSimulation
    {
        public const float Step = 1f / 60;
        public const float Every = 2f;
        public const float MaxSeconds = 60f;
        public static readonly Point2 Aim = new Point2(0, 4);

        internal sealed class Sample
        {
            public float Elapsed;
            public float Remaining;
            public int Level;
            public long Kills;
            public long Gold;
            public long Exp;
            public readonly List<(EnemyType Type, int Count)> Alive = new List<(EnemyType Type, int Count)>();
            // 출현 띠 넓이 대비 적 원 넓이의 합(겹침 무시, 픽업 빼고). 0.3이면 띠의 30%를 적이 덮는다.
            public double Cover;
        }

        internal sealed class Result
        {
            public int Seed;
            public readonly List<string> Errors = new List<string>();
            public readonly List<Sample> Samples = new List<Sample>();
            public bool Ended;
            public float FieldScale;
            public float BandMin;
            public float BandMax;

            public JsonObject ToJson()
            {
                var samples = new List<object>(Samples.Count);
                foreach (Sample sample in Samples)
                {
                    var alive = new JsonObject();
                    foreach ((EnemyType type, int count) in sample.Alive)
                        alive.Add(ContractIds.Of(type.ToString()), count);

                    samples.Add(new JsonObject
                    {
                        { "t", Round(sample.Elapsed) },
                        { "remaining", Round(sample.Remaining) },
                        { "level", sample.Level },
                        { "alive", alive },
                        { "cover", Math.Round(sample.Cover, 3) },
                        { "kills", sample.Kills },
                        { "gold", sample.Gold },
                        { "exp", sample.Exp },
                    });
                }

                return new JsonObject
                {
                    { "method", $"조준 고정({Aim.X:0.#},{Aim.Y:0.#}) · 1/60초 진행 · {Every:0.#}초마다 · 최대 {MaxSeconds:0}초 · 사람 플레이 아님(비교 기준선)" },
                    { "seed", Seed },
                    { "errors", new List<object>(Errors) },
                    { "fieldScale", Round(FieldScale) },
                    { "band", new JsonObject { { "min", Round(BandMin) }, { "max", Round(BandMax) } } },
                    { "ended", Ended },
                    { "samples", samples },
                };
            }

            private static double Round(float value) => Math.Round(value, 2);
        }

        public static Result Run(PlaytestScenario setup, GameContent content, NodeTree tree)
        {
            var result = new Result { Seed = setup.seed != 0 ? setup.seed : TestSetupPreview.PreviewSeed };
            var progress = new ProgressState();
            var warnings = new List<string>();

            if (!ScenarioRunner.TryApply(setup, progress, tree, content.Growth, result.Errors, warnings))
                return result;

            UpgradeStatValues upgrades = NodePurchase.StatsFor(progress, tree);
            GameSession session = GameSessionFactory.Create(content, progress, result.Seed, upgrades);
            session.Begin();
            session.SetAimPoint(Aim);

            if (setup.startLevel > session.World.Hq.Level)
            {
                try
                {
                    BattleCheats.ReachLevel(session, setup.startLevel);
                }
                catch (ArgumentException error)
                {
                    result.Errors.Add($"시작 Level {setup.startLevel}: {error.Message}");
                }
            }

            World world = session.World;
            EnemyPlacementDefinition band = content.Enemies.EnemyPlacement;
            long kills = 0;
            float next = 0;

            // 첫 표본은 시작 Level을 채운 직후(Level업 성장 공급이 나온 뒤)다.
            session.Advance(Step);
            CountKills(world, ref kills);

            while (true)
            {
                if (session.Elapsed + 1e-4f >= next || session.IsEnded)
                {
                    result.FieldScale = world.Hq.FieldScale;
                    result.BandMin = band.MinDistance * world.Hq.FieldScale;
                    result.BandMax = band.MaxDistance * world.Hq.FieldScale;
                    result.Samples.Add(Take(session, kills, result.BandMin, result.BandMax));
                    next += Every;
                }

                if (session.IsEnded || session.Elapsed >= MaxSeconds)
                    break;

                session.Advance(Step);
                CountKills(world, ref kills);
            }

            result.Ended = session.IsEnded;
            return result;
        }

        private static void CountKills(World world, ref long kills)
        {
            foreach (DeathRecord death in world.Deaths)
            {
                if (!death.IsPickup)
                    kills++;
            }
        }

        private static Sample Take(GameSession session, long kills, float bandMin, float bandMax)
        {
            World world = session.World;
            var sample = new Sample
            {
                Elapsed = session.Elapsed,
                Remaining = session.Remaining,
                Level = world.Hq.Level,
                Kills = kills,
                Gold = world.EarnedGold,
                Exp = world.Hq.Exp,
            };

            var alive = new SortedDictionary<EnemyType, int>();
            double area = 0;

            foreach (Enemy enemy in world.Enemies)
            {
                alive.TryGetValue(enemy.Definition.Type, out int count);
                alive[enemy.Definition.Type] = count + 1;

                if (!enemy.Definition.IsPickup)
                    area += Math.PI * enemy.Stats.Radius * enemy.Stats.Radius;
            }

            foreach (KeyValuePair<EnemyType, int> pair in alive)
                sample.Alive.Add((pair.Key, pair.Value));

            double band = Math.PI * (bandMax * (double)bandMax - bandMin * (double)bandMin);
            sample.Cover = band > 0 ? area / band : 0;
            return sample;
        }
    }
}
#endif
