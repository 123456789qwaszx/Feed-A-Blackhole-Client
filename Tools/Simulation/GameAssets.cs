using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BlackHole.Core;

namespace BlackHole.Simulation.Tool
{
    // 레포의 실제 Unity 에셋을 읽어 게임과 같은 정의(GameContent·NodeTree)를 만든다. Unity 없이 돈다.
    // 게임의 GameContentLoader와 같은 길이다: 게임 콘텐츠 세트(GameContentSetup) → 각 설정 에셋의 ToData()와 같은 변환 → Core 로더(ContentLoader·NodeContentCsv·NodeTreeLoader) → UpgradeContentCheck.
    // 에셋에 없는 칸은 Unity처럼 그 클래스의 필드 기본값을 쓴다(아래 기본값은 C# 파일의 필드 초기값과 같다).
    internal sealed class GameAssets
    {
        public const string DefaultSetup = "Assets/Data/GameContentSetup.asset";

        private readonly string _root;
        private readonly Dictionary<string, string> _pathOfGuid;
        private readonly Dictionary<string, YamlNode> _assets = new Dictionary<string, YamlNode>(StringComparer.Ordinal);

        // 읽은 저작 데이터(Core 로더에 넘기기 전). 확인용.
        public ContentData Data { get; private set; }

        public GameContent Content { get; private set; }
        public NodeTree Tree { get; private set; }
        public List<string> Errors { get; } = new List<string>();

        private GameAssets(string root, Dictionary<string, string> pathOfGuid)
        {
            _root = root;
            _pathOfGuid = pathOfGuid;
        }

        public static GameAssets Load(string root, string setupPath = DefaultSetup)
        {
            var assets = new GameAssets(root, IndexGuids(Path.Combine(root, "Assets")));
            assets.Build(Path.Combine(root, setupPath));
            return assets;
        }

        private void Build(string setupPath)
        {
            YamlNode setup = UnityYaml.ReadAsset(setupPath);
            var data = new ContentData();

            YamlNode rules = Asset(setup.Guid("_battleRules"), "판 규칙(_battleRules)");
            data.BattleRules = new BattleRulesData
            {
                TimeLimit = Float(rules, "timeLimit", 12),
                KillTimeBonus = Float(rules, "killTimeBonus", 0.3f),
                MaxKillTimeBonus = Float(rules, "maxKillTimeBonus", 0),
            };

            YamlNode skills = Asset(setup.Guid("_skills"), "스킬 설정(_skills)");
            data.Breaker = new BreakerData
            {
                Damage = Float(skills, "breakerDamage", 2),
                Interval = Float(skills, "breakerInterval", 1),
                Radius = Float(skills, "breakerRadius", 1.5f),
                CritChance = Float(skills, "breakerCritChance", 0),
                CritDamage = Float(skills, "breakerCritDamage", 1),
                MoonDuration = Float(skills, "breakerMoonDuration", 5),
                MoonSpeedBonus = Float(skills, "breakerMoonSpeedBonus", 0.2f),
                MoonRadiusBonus = Float(skills, "breakerMoonRadiusBonus", 0.1f),
                CometDuration = Float(skills, "breakerCometDuration", 5),
                CometCritDamageBonus = Float(skills, "breakerCometCritDamageBonus", 0.5f),
                HoldSlow = Float(skills, "breakerHoldSlow", 0.05f),
            };

            YamlNode catalog = Asset(setup.Guid("_enemies"), "적 종류 목록(_enemies)");

            foreach (YamlNode kind in catalog["kinds"]?.Items ?? Array.Empty<YamlNode>())
                data.Enemies.Enemies.Add(EnemyOf(Asset(GuidOf(kind.Scalar), "적 종류")));

            YamlNode supply = Asset(setup.Guid("_supply"), "적 공급 설정(_supply)");
            data.Enemies.EnemyPlacement = new EnemyPlacementData
            {
                MinDistance = Float(supply, "minDistance", 2),
                MaxDistance = Float(supply, "maxDistance", 4),
            };
            data.Enemies.PeriodicSpawnPlacement = new PeriodicSpawnPlacementData
            {
                InnerOffset = Float(supply, "periodicSpawnInnerOffset", -1),
                OuterOffset = Float(supply, "periodicSpawnOuterOffset", 0.5f),
            };

            foreach (YamlNode entry in supply["startSupply"]?.Items ?? Array.Empty<YamlNode>())
            {
                string kind = entry.Guid("kind");
                data.Enemies.StartSupply.Add(new SupplyData
                {
                    Enemy = kind != null ? (EnemyType)Asset(kind, "시작 공급 적").Int("type") : (EnemyType?)null,
                    Count = entry.Int("count"),
                });
            }

            data.Growth = GrowthOf(Asset(setup.Guid("_growth"), "블랙홀 성장 설정(_growth)"));

            YamlNode nodes = Asset(setup.Guid("_nodes"), "노드 목록(_nodes)");
            YamlNode source = Asset(nodes.Guid("_content"), "노드 콘텐츠(_content)");
            NodeContentLoadResult nodeContent = NodeContentCsv.Load(
                Text(source.Guid("_upgradeStats"), "UpgradeStats CSV"),
                Text(source.Guid("_nodes"), "Nodes CSV"),
                Text(source.Guid("_nodeCost"), "NodeCost CSV"),
                Text(source.Guid("_nodeEffects"), "NodeEffects CSV"));
            Collect("노드 콘텐츠", nodeContent.Diagnostics);

            Data = data;
            ContentLoadResult content = ContentLoader.Load(data);
            Collect("콘텐츠", content.Diagnostics);

            if (!content.Succeeded || !nodeContent.Succeeded)
                return;

            NodeTreeLoadResult tree = NodeTreeLoader.Load(LayoutOf(nodes), nodeContent.Content);
            Collect("노드 트리", tree.Diagnostics);

            if (!tree.Succeeded)
                return;

            Collect("노드 트리 × 콘텐츠", UpgradeContentCheck.Check(content.Content, tree.Tree));

            if (Errors.Count > 0)
                return;

            Content = content.Content;
            Tree = tree.Tree;
        }

        // EnemyKind.ToData()와 같다.
        private EnemyData EnemyOf(YamlNode kind)
        {
            string next = kind.Guid("upgradesTo");
            var data = new EnemyData
            {
                Type = (EnemyType)kind.Int("type"),
                MoveSpeed = Float(kind, "moveSpeed", 1),
                Radius = Float(kind, "radius", 0.25f),
                RadiusStep = Float(kind, "radiusStep", 0.5f),
                UpgradesTo = next != null ? (EnemyType)Asset(next, "변환 대상(upgradesTo)").Int("type") : (EnemyType?)null,
                SpawnPeriod = Float(kind, "spawnPeriod", 0),
                RainCount = kind.Int("rainCount"),
            };

            foreach (YamlNode trait in kind["traits"]?.Items ?? Array.Empty<YamlNode>())
            {
                data.Traits.Add(new EnemyTraitData
                {
                    Type = (EnemyTraitType)trait.Int("type"),
                    MaxActive = trait.Int("maxActive"),
                    Effect = new DeathEffectData
                    {
                        Multiplier = trait.Float("multiplier"),
                        CritRewardScale = trait.Float("critRewardScale"),
                        Damage = trait.Float("damage"),
                        Radius = trait.Float("radius"),
                        MaxTargets = trait.Int("maxTargets"),
                        BranchChance = trait.Float("branchChance"),
                        CritChance = trait.Float("critChance"),
                        CritMultiplier = trait.Float("critMultiplier"),
                        HealthFraction = trait.Float("healthFraction"),
                        Width = trait.Float("width"),
                        Force = trait.Float("force"),
                        Duration = trait.Float("duration"),
                    },
                });
            }

            foreach (YamlNode tier in kind["tiers"]?.Items ?? Array.Empty<YamlNode>())
                data.Tiers.Add(new EnemyTierData { MaxHealth = tier.Float("maxHealth"), Gold = tier.Long("gold"), Exp = tier.Long("exp") });

            return data;
        }

        // HqGrowthSetup.ToData()와 같다. Unity는 long 목록을 8바이트 리틀엔디언의 16진 문자열 한 줄로 쓴다.
        private static HqGrowthData GrowthOf(YamlNode growth)
        {
            var data = new HqGrowthData();
            string hex = new string((growth.Text("levelExp") ?? "").TakeWhile(Uri.IsHexDigit).ToArray());

            for (int i = 0; i + 16 <= hex.Length; i += 16)
                data.LevelExp.Add(BitConverter.ToInt64(Convert.FromHexString(hex.Substring(i, 16)), 0));

            foreach (YamlNode milestone in growth["milestones"]?.Items ?? Array.Empty<YamlNode>())
            {
                data.Milestones.Add(new HqMilestoneData
                {
                    Level = milestone.Int("level"),
                    TargetGold = milestone.Long("targetGold"),
                    FieldScale = milestone.Float("fieldScale"),
                });
            }

            return data;
        }

        // NodeCatalog.ToData()와 같다.
        private static NodeTreeData LayoutOf(YamlNode catalog)
        {
            var data = new NodeTreeData();

            foreach (YamlNode node in catalog["_layout"]?["Nodes"]?.Items ?? Array.Empty<YamlNode>())
            {
                data.Nodes.Add(new NodeData
                {
                    Id = node.Text("Id"),
                    Start = node.Int("Start") != 0,
                    X = node.Int("X"),
                    Y = node.Int("Y"),
                    Links = node["Links"]?.Items.Select(link => link.Scalar).ToList() ?? new List<string>(),
                });
            }

            return data;
        }

        private YamlNode Asset(string guid, string what)
        {
            string path = PathOf(guid, what);

            if (!_assets.TryGetValue(path, out YamlNode asset))
            {
                asset = UnityYaml.ReadAsset(path);
                _assets.Add(path, asset);
            }

            return asset;
        }

        private string Text(string guid, string what) => File.ReadAllText(PathOf(guid, what));

        private string PathOf(string guid, string what)
        {
            if (guid == null)
                throw new InvalidDataException($"{what}: 연결이 비어 있다.");

            if (!_pathOfGuid.TryGetValue(guid, out string path))
                throw new InvalidDataException($"{what}: guid {guid}인 에셋을 찾지 못했다.");

            return path;
        }

        private static string GuidOf(string reference)
        {
            var node = new YamlNode { Map = new Dictionary<string, YamlNode> { ["ref"] = new YamlNode { Scalar = reference } } };
            return node.Guid("ref");
        }

        private static float Float(YamlNode node, string key, float fallback) =>
            node[key]?.Scalar != null && float.TryParse(node.Text(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;

        private void Collect(string stage, IReadOnlyList<ContentDiagnostic> diagnostics)
        {
            foreach (ContentDiagnostic diagnostic in diagnostics)
                Errors.Add($"[{stage}] {diagnostic}");
        }

        // Assets 아래 .meta의 guid → 그 에셋 경로.
        private static Dictionary<string, string> IndexGuids(string assets)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string meta in Directory.EnumerateFiles(assets, "*.meta", SearchOption.AllDirectories))
            {
                foreach (string line in File.ReadLines(meta).Take(4))
                {
                    if (!line.StartsWith("guid: ", StringComparison.Ordinal))
                        continue;

                    map[line.Substring(6).Trim().ToLowerInvariant()] = meta.Substring(0, meta.Length - ".meta".Length);
                    break;
                }
            }

            return map;
        }
    }
}
