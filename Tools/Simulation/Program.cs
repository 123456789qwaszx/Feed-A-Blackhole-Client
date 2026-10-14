using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using BlackHole.Core;

namespace BlackHole.Simulation.Tool
{
    // 판 시뮬레이션 실행기. 레포 맨 위에서:
    //   dotnet run -c Release --project Tools/Simulation -- campaign [--bot greedy|touch|circle] [--seed 1] [--runs 1] [--max-battles 5000] [--out 폴더]
    //   dotnet run -c Release --project Tools/Simulation -- battle [--bot greedy|touch|circle|both] [--nodes none|all] [--scenario 파일] [--stage N] [--seeds 3]
    // 설명: Docs/Simulation/README.md
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "help" || args[0] == "--help")
            {
                Console.WriteLine(Usage);
                return args.Length == 0 ? 1 : 0;
            }

            try
            {
                var options = Options.Parse(args.Skip(1));
                string root = FindRoot(options.Get("root"));
                GameAssets assets = GameAssets.Load(root, options.Get("setup") ?? GameAssets.DefaultSetup);

                if (assets.Content == null)
                {
                    Console.Error.WriteLine("콘텐츠를 불러오지 못했다:");
                    foreach (string error in assets.Errors)
                        Console.Error.WriteLine("  " + error);
                    return 2;
                }

                switch (args[0])
                {
                    case "campaign": return Campaign(root, assets, options);
                    case "battle": return Battle(assets, options);
                    default:
                        Console.Error.WriteLine($"모르는 명령: {args[0]}\n\n{Usage}");
                        return 1;
                }
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidDataException || error is IOException)
            {
                Console.Error.WriteLine("오류: " + error.Message);
                return 2;
            }
        }

        private const string Usage =
@"판 시뮬레이션 (Docs/Simulation/README.md)

  campaign  처음부터 클리어까지 전체 루프: 판 → 결산 → 가장 싼 노드부터 구매 → 다음 판
    --bot greedy|touch|circle    조준 봇(기본 greedy)
    --seed N               시드(기본 1)
    --runs N               시드를 1씩 올리며 N번 돌고 runs.csv·runs.md로 모은다(기본 1)
    --max-battles N        판 상한(기본 5000)
    --stall N              N판 연속 구매·이정표가 없으면 멈춤(기본 300)
    --fps N                초당 진행 수(기본 60)
    --max-seconds N        판 하나의 상한(판 시간, 기본 3600). 끝나지 않는 판을 끊는다
    --out 폴더             결과 폴더(기본 PlaytestData/sim/<시각>-<봇>-s<시드>[x<실행 수>])

  battle    고정한 세팅으로 판 몇 개
    --bot greedy|touch|circle|both
    --nodes none|all       노드 없음(기본) / 모두 최대 Rank
    --scenario 파일        테스트 세팅 JSON(Assets/Playtest/Scenarios/*.json)의 노드·성장도
    --stage N              성장도(기본: none은 0, all은 마지막, 세팅은 그 값)
    --seeds N              판 수(기본 3)
    --max-seconds N        판 하나의 상한(기본 600)
    --fps N

  공통: --root 레포 경로(기본: 지금 폴더에서 위로 찾기), --setup 게임 콘텐츠 세트 에셋 경로";

        private static int Campaign(string root, GameAssets assets, Options options)
        {
            string botName = options.Get("bot") ?? "greedy";
            int firstSeed = options.Int("seed", 1);
            int runs = options.Int("runs", 1);
            Func<IAimBot> bot = BotFactory(botName);

            string output = options.Get("out") ?? Path.Combine(root, "PlaytestData", "sim",
                $"{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{botName}-s{firstSeed}" + (runs > 1 ? $"x{runs}" : ""));

            Console.WriteLine($"전체 루프: 봇 {botName}, 시드 {firstSeed}" + (runs > 1 ? $"부터 {runs}번" : "") +
                $", 노드 {assets.Tree.Nodes.Count}개, 이정표 {assets.Content.Growth.MaxStage}개");
            var watch = Stopwatch.StartNew();
            var results = new List<CampaignResult>();

            for (int run = 0; run < runs; run++)
            {
                var settings = new CampaignSettings
                {
                    Seed = firstSeed + run,
                    MaxBattles = options.Int("max-battles", 5000),
                    StallBattles = options.Int("stall", 300),
                    FrameSeconds = 1f / options.Int("fps", 60),
                    MaxBattleSeconds = options.Int("max-seconds", (int)BattleRunner.SafetySeconds),
                    CreateBot = bot,
                };

                int lastStage = -1;
                CampaignResult result = CampaignRunner.Run(assets.Content, assets.Tree, settings, entry =>
                {
                    if (runs > 1)
                        return;

                    BattleRecord b = entry.Battle;
                    bool stageChanged = b.Stage != lastStage;
                    lastStage = b.Stage;

                    if (stageChanged || b.ReachedMilestone || entry.Index % 25 == 0 || entry.Index <= 5)
                    {
                        Console.WriteLine($"  판 {entry.Index,4} · 성장도 {b.Stage} · Level {b.StartLevel,2}→{b.ReachedLevel,2}{(b.ReachedMilestone ? " 이정표!" : "")}" +
                            $" · {b.PlayedSeconds,5:0.0}초 · 번 Gold {b.EarnedGold,22:N0} · 산 Rank {entry.Purchases.Count,3} (보유 {entry.OwnedRanks})" +
                            $" · 누적 {Clock(entry.ElapsedSeconds)} · 실제 {watch.Elapsed.TotalSeconds:0}초");
                    }
                });

                CampaignReport.Write(result, runs > 1 ? Path.Combine(output, $"run-s{settings.Seed}") : output);
                results.Add(result);

                if (runs > 1)
                {
                    Console.WriteLine($"  시드 {settings.Seed,4}: {result.End} · 판 {result.Battles.Count} · 판 시간 합 {Clock(result.TotalSeconds)}" +
                        $" · 이정표 판 {string.Join("/", result.Stages.Select(st => st.Battle))} · Rank {result.OwnedRanks} · 실제 {watch.Elapsed.TotalSeconds:0}초");
                }
            }

            Console.WriteLine();

            if (runs > 1)
            {
                CampaignReport.WriteRuns(results, output);
                Console.WriteLine(CampaignReport.RunsSummary(results));
                Console.WriteLine($"결과: {output} (runs.csv, runs.md, 실행마다 run-s<시드>/) · 실제 {watch.Elapsed.TotalSeconds:0}초");
            }
            else
            {
                Console.WriteLine(CampaignReport.Summary(results[0]));
                Console.WriteLine($"결과: {output} (battles.csv, purchases.csv, summary.md) · 실제 {watch.Elapsed.TotalSeconds:0}초");
            }

            return results.All(r => r.End == CampaignEnd.Cleared) ? 0 : 3;
        }

        private static int Battle(GameAssets assets, Options options)
        {
            NodeTree tree = assets.Tree;
            var ranks = new List<(string, int)>();
            int stage = 0;
            string scenario = options.Get("scenario");
            string nodes = options.Get("nodes") ?? "none";

            if (scenario != null)
            {
                using JsonDocument json = JsonDocument.Parse(File.ReadAllText(scenario));
                stage = json.RootElement.TryGetProperty("growthStage", out JsonElement s) ? s.GetInt32() : 0;

                foreach (JsonElement node in json.RootElement.GetProperty("nodes").EnumerateArray())
                    ranks.Add((node.GetProperty("nodeId").GetString(), node.GetProperty("rank").GetInt32()));
            }
            else if (nodes == "all")
            {
                ranks.AddRange(tree.Nodes.Select(n => (n.Id, n.MaxRank)));
                stage = assets.Content.Growth.MaxStage;
            }
            else if (nodes != "none")
            {
                throw new ArgumentException("--nodes는 none 또는 all이다.");
            }

            stage = options.Int("stage", stage);
            float frame = 1f / options.Int("fps", 60);
            int seeds = options.Int("seeds", 3);
            int maxSeconds = options.Int("max-seconds", 600);
            string botOption = options.Get("bot") ?? "greedy";
            string[] bots = botOption == "both" ? new[] { "circle", "greedy" } : new[] { botOption };

            foreach (string name in bots)
            {
                Func<IAimBot> make = BotFactory(name);
                Console.WriteLine($"== 봇 {name}, 성장도 {stage}, 노드 Rank {ranks.Sum(r => r.Item2)}");

                for (int seed = 1; seed <= seeds; seed++)
                {
                    // 판마다 같은 시작 상태(Gold 0)에서 시작한다.
                    ProgressState state = StartState(stage, ranks, assets);
                    UpgradeStatValues upgrades = NodePurchase.StatsFor(state, tree);
                    BattleRecord b = BattleRunner.Run(assets.Content, state, upgrades, make(), seed, frame, maxSeconds);
                    Console.WriteLine($"  시드 {seed}: {b.PlayedSeconds,5:0.0}초{(b.Capped ? "(상한에서 끊음)" : "")} · Level {b.StartLevel}→{b.ReachedLevel}{(b.ReachedMilestone ? " 이정표" : "")}" +
                        $" · 번 Gold {b.EarnedGold,22:N0} · 처치 {b.TotalKills,6} · Breaker 피해 {b.BreakerDamage:0.###E+0}" +
                        $" · EXP {b.Exp:N0} · 소/행/별/은/혜 {b.KillsOf(EnemyType.Asteroid)}/{b.KillsOf(EnemyType.Planet)}/{b.KillsOf(EnemyType.Star)}/{b.KillsOf(EnemyType.Galaxy)}/{b.KillsOf(EnemyType.Comet)}" +
                        $" · 추가 시간 {b.AddedSeconds:0.0}초 · 달 {b.CollectedMoons} · 혜성 {b.CollectedComets} · 공격 {b.BreakerTicks}");
                }
            }

            return 0;
        }

        // 저장을 불러올 때와 같은 검사(ProgressSave.Load)를 거친 시작 상태. Gold는 0이다.
        private static ProgressState StartState(int stage, List<(string NodeId, int Rank)> ranks, GameAssets assets)
        {
            var data = new ProgressSaveData
            {
                FormatVersion = ProgressSaveData.CurrentFormatVersion,
                GrowthStage = stage,
                SavedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            };

            foreach ((string nodeId, int rank) in ranks)
                data.Nodes.Add(new NodeRankSaveData { NodeId = nodeId, Rank = rank });

            ProgressLoadResult loaded = ProgressSave.Load(data, assets.Tree.Content, assets.Content.Growth);

            if (!loaded.Succeeded)
                throw new ArgumentException("시작 상태를 만들지 못했다: " + string.Join("; ", loaded.Diagnostics));

            var progress = new ProgressState();
            ProgressSave.Restore(progress, loaded.Progress);
            return progress;
        }

        private static Func<IAimBot> BotFactory(string name)
        {
            switch (name)
            {
                case "greedy": return () => new GreedyAimBot();
                case "touch": return () => new GreedyAimBot(AimBotProfile.Touch);
                case "circle": return () => new CircleAimBot();
                default: throw new ArgumentException($"모르는 봇: {name} (greedy, touch, circle)");
            }
        }

        // --root가 없으면 지금 폴더에서 위로 올라가며 게임 콘텐츠 세트가 있는 폴더를 찾는다.
        private static string FindRoot(string given)
        {
            if (given != null)
                return Path.GetFullPath(given);

            for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, GameAssets.DefaultSetup)))
                    return dir.FullName;
            }

            throw new ArgumentException($"레포 맨 위({GameAssets.DefaultSetup}가 있는 폴더)를 찾지 못했다. --root로 알려 준다.");
        }

        private static string Clock(double seconds)
        {
            var span = TimeSpan.FromSeconds(seconds);
            return span.TotalHours >= 1 ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}" : $"{span.Minutes}:{span.Seconds:00}";
        }

        private sealed class Options
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);

            public static Options Parse(IEnumerable<string> args)
            {
                var options = new Options();
                string key = null;

                foreach (string arg in args)
                {
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        key = arg.Substring(2);
                        options._values[key] = "";
                    }
                    else if (key != null)
                    {
                        options._values[key] = arg;
                        key = null;
                    }
                    else
                    {
                        throw new ArgumentException($"알 수 없는 인자: {arg}");
                    }
                }

                return options;
            }

            public string Get(string key) => _values.TryGetValue(key, out string value) && value.Length > 0 ? value : null;

            public int Int(string key, int fallback)
            {
                string text = Get(key);

                if (text == null)
                    return fallback;

                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value <= 0 && key != "stage")
                    throw new ArgumentException($"--{key}에는 양의 정수가 필요하다. 받은 값: {text}");

                return value;
            }
        }
    }
}
