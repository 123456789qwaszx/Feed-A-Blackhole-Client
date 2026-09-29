using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 적 종류 하나의 저작 에셋: 규칙 수치, 외형. 종류를 더할 때는 코드를 고치지 않고
    // 에셋을 하나 만들어 적 종류 목록(EnemyCatalog)에 넣는다. 움직임은 모든 종류가 HQ 공전이다(EnemyBehaviors).
    // 규칙 칸은 Core의 저작 형식(EnemyData)으로 옮겨져 EnemyContentLoader가 검증한다.
    // 외형 칸(스프라이트, 색 등급의 색)은 Core로 가지 않고 화면(EnemyView)만 읽는다. 규칙과 외형이 한 에셋에 있어 외형 연결이 빠지지 않는다.
    // ID와 스프라이트 말고는 값의 원본이 데이터 시트(Enemies·EnemyTiers·EnemyStageColors·EnemyMassLevels·EnemySizeClasses 탭)이고, 가져오기가 채운다.
    //
    // 종류는 계열(소행성·행성·별·달·혜성)이고 색은 종류 안에 둔다(BATTLE_COMPOSITION_PLAN 4.1).
    // - 색 등급: 같은 윤곽(스프라이트)에 색마다 색·크기·HP·Gold·EXP가 다르다. 색이 없는 종류는 한 줄이다.
    // - 성장도별 색 비율: 블랙홀 성장도가 몇부터 색마다 어떤 비율로 나오는가. 판을 시작할 때의 성장도로 한 줄이 골라진다(GAME_RULES 3.2).
    // - 질량 단계: 질량 증가를 산 수마다 한 줄. HP·Gold 계수. 판 조립 때 한 줄이 골라진다. 색 비율과는 무관하다.
    // - 크기 등급: 같은 윤곽·색에 크기와 수치가 다른 줄(원작 소행성 크기1·2·3). 크기·HP·Gold·EXP 계수. 크기 노드를 산 수만큼 열리고,
    //   열린 등급이 생성 때 섞여 나온다. 비우면 크기 등급이 없다.
    // - 황금 배율: 황금은 종류가 아니라 생성 때 정해지는 특성이다. 황금이면 Gold에 이 값을 곱한다. 0이면 황금이 되지 않는다.
    // 특수 효과(전기·폭발·처치 버프)는 종류가 아니라 종류에 붙는 특성이다: 사망 효과 칸. 효과를 가진 적은 사망 효과의 피해를 받지 않는다.
    [CreateAssetMenu(fileName = "EnemyKind", menuName = "BlackHole/Enemy Kind")]
    public sealed class EnemyKind : ScriptableObject
    {
        // 사망 효과 종류. None은 효과가 없다. 이름이 Core 저작 형식의 종류 이름이 된다.
        public enum DeathEffectKind { None, ChainLightning, Explosion, AttackHaste, GuaranteedCritical }

        [Serializable]
        public struct Tier
        {
            [Tooltip("이 색 등급을 그리는 색. Core는 모른다.")]
            public Color color;
            public float maxHealth;
            [Tooltip("반지름. 화면에 그리는 크기도 이 값이다.")]
            public float size;
            [Tooltip("사망 때 판의 합계에 드는 Gold의 기본값. 0 이상.")]
            public long gold;
            [Tooltip("사망 때 블랙홀에 드는 EXP. 0 이상. 질량 단계와 황금은 곱하지 않고, 크기 등급은 곱한다.")]
            public long exp;
        }

        [Serializable]
        public struct MassLevel
        {
            [Tooltip("색 등급의 HP에 곱한다.")]
            public float healthMultiplier;
            [Tooltip("색 등급의 Gold에 곱한다(반올림).")]
            public float goldMultiplier;
        }

        [Serializable]
        public struct SizeClass
        {
            [Tooltip("색 등급의 크기(반지름)에 곱한다. 공격 판정에도 쓰인다.")]
            public float sizeMultiplier;
            [Tooltip("색 등급의 HP에 곱한다.")]
            public float healthMultiplier;
            [Tooltip("색 등급의 Gold에 곱한다(반올림).")]
            public float goldMultiplier;
            [Tooltip("색 등급의 EXP에 곱한다(반올림).")]
            public float expMultiplier;
        }

        [Serializable]
        public struct StageColor
        {
            [Tooltip("이 줄을 쓰기 시작하는 블랙홀 성장도(0 이상). 앞 줄보다 커야 한다.")]
            public int fromStage;
            [Tooltip("색 등급 표와 같은 순서·개수. 0 이상이고 합이 0보다 커야 한다(합이 1이 아니어도 된다).")]
            public List<float> tierRatios;
        }

        [Tooltip("공급과 다른 데이터가 이 종류를 가리키는 식별자. 정한 뒤에는 바꾸지 않는다.")]
        [SerializeField] private string id;

        [Tooltip("초당 이동 거리. 모든 색 등급이 같다.")]
        [SerializeField] private float moveSpeed = 1;

        [Header("종류 사이 (BATTLE_COMPOSITION_PLAN 3.4)")]
        [Tooltip("이 종류의 생성 중 변환 비율(노드 enemy.<id>.upgrade, %)만큼 나오는 다음 종류. 비우면 변환하지 않는다. 적 종류 목록에 있어야 한다.")]
        [SerializeField] private EnemyKind upgradesTo;
        [Tooltip("노드 밖의 기본 변환 비율(%). 성장도가 아래 값 이상이면 이만큼이 변환 대상으로 나오고, 변환 노드가 여기에 더한다. 0부터 100까지, 변환 대상이 있어야 한다.")]
        [SerializeField] private float baseUpgrade;
        [Tooltip("기본 변환 비율을 쓰기 시작하는 성장도(0 이상).")]
        [SerializeField] private int baseUpgradeFromStage;
        [Tooltip("특수 종류이면 부모 종류. 부모로 정해진 생성 중 생성 확률(노드 enemy.<id>.chance, %)만큼 이 종류로 나온다. 비우면 특수 종류가 아니다.")]
        [SerializeField] private EnemyKind specialOf;

        [Header("색 등급 (번호가 적의 색 등급)")]
        [SerializeField] private List<Tier> tiers = new List<Tier>();

        [Header("성장도별 색 비율 (판을 시작할 때의 성장도가 고른다)")]
        [SerializeField] private List<StageColor> stageColors = new List<StageColor>();

        [Header("질량 단계 (0 = 질량 증가를 사지 않음, HP·Gold 계수)")]
        [SerializeField] private List<MassLevel> massLevels = new List<MassLevel>();

        [Header("크기 등급 (0 = 크기 노드를 사지 않아도 나옴, 비우면 없음)")]
        [SerializeField] private List<SizeClass> sizeClasses = new List<SizeClass>();

        [Header("황금")]
        [Tooltip("황금일 때 Gold에 곱하는 값. 0이면 이 종류는 황금이 되지 않는다(원작은 소행성만, 기본 50). 얼마나 섞일지는 판 조립이 정한다.")]
        [SerializeField] private float goldenMultiplier;

        [Header("사망 효과 (특성)")]
        [SerializeField] private DeathEffectKind deathEffect;
        [Tooltip("ChainLightning·Explosion: 효과 피해.")]
        [SerializeField] private float effectDamage;
        [Tooltip("ChainLightning: 번개가 한 번 옮겨 가는 최대 거리. Explosion: 폭발 반경.")]
        [SerializeField] private float effectRadius;
        [Tooltip("ChainLightning: 번개가 옮겨 가는 최대 횟수.")]
        [SerializeField] private int effectMaxTargets;
        [Tooltip("AttackHaste·GuaranteedCritical: 버프 시간(초). 버프는 Breaker에만 붙는다.")]
        [SerializeField] private float effectDuration;
        [Tooltip("AttackHaste: Breaker 공격 주기 배율(0 ~ 1). 0.5면 주기가 절반이다.")]
        [SerializeField] private float effectIntervalMultiplier;

        [Header("외형 (Core는 모른다)")]
        [Tooltip("모든 색 등급이 같은 윤곽을 쓴다. 비우면 임시 원으로 그린다.")]
        [SerializeField] private Sprite sprite;

        public string Id => id;
        public Sprite Sprite => sprite;

        // 색 등급의 색. 없는 번호는 흰색이다.
        public Color ColorOf(int tier) => tier >= 0 && tier < tiers.Count ? tiers[tier].color : Color.white;

        // ToData의 반대. 데이터 시트 가져오기(메뉴 BlackHole > Data Sheets)만 부른다. ID와 스프라이트는 이 에셋의 것이라 두고,
        // 색 등급의 색(colors)과 종류 사이의 연결(ID를 가져오기가 에셋으로 찾은 것)은 따로 받는다.
        internal void Replace(EnemyData data, IReadOnlyList<Color> colors, EnemyKind upgradesToKind, EnemyKind specialOfKind)
        {
            moveSpeed = data.MoveSpeed;
            upgradesTo = upgradesToKind;
            baseUpgrade = data.BaseUpgrade;
            baseUpgradeFromStage = data.BaseUpgradeFromStage;
            specialOf = specialOfKind;
            goldenMultiplier = data.GoldenMultiplier;

            tiers = new List<Tier>();

            for (int i = 0; i < data.Tiers.Count; i++)
            {
                EnemyTierData tier = data.Tiers[i];
                tiers.Add(new Tier { color = colors[i], maxHealth = tier.MaxHealth, size = tier.Size, gold = tier.Gold, exp = tier.Exp });
            }

            stageColors = new List<StageColor>();

            foreach (StageColorData row in data.StageColors)
                stageColors.Add(new StageColor { fromStage = row.FromStage, tierRatios = new List<float>(row.TierRatios) });

            massLevels = new List<MassLevel>();

            foreach (MassLevelData level in data.MassLevels)
                massLevels.Add(new MassLevel { healthMultiplier = level.HealthMultiplier, goldMultiplier = level.GoldMultiplier });

            sizeClasses = new List<SizeClass>();

            foreach (SizeClassData size in data.SizeClasses)
            {
                sizeClasses.Add(new SizeClass
                {
                    sizeMultiplier = size.SizeMultiplier,
                    healthMultiplier = size.HealthMultiplier,
                    goldMultiplier = size.GoldMultiplier,
                    expMultiplier = size.ExpMultiplier,
                });
            }

            DeathEffectData effect = data.DeathEffect;
            deathEffect = effect == null ? DeathEffectKind.None : (DeathEffectKind)Enum.Parse(typeof(DeathEffectKind), effect.Kind);
            effectDamage = effect?.Damage ?? 0;
            effectRadius = effect?.Radius ?? 0;
            effectMaxTargets = effect?.MaxTargets ?? 0;
            effectDuration = effect?.Duration ?? 0;
            effectIntervalMultiplier = effect?.IntervalMultiplier ?? 0;
        }

        internal EnemyData ToData()
        {
            var data = new EnemyData
            {
                Id = id,
                MoveSpeed = moveSpeed,
                UpgradesTo = upgradesTo != null ? upgradesTo.Id : null,
                BaseUpgrade = baseUpgrade,
                BaseUpgradeFromStage = baseUpgradeFromStage,
                SpecialOf = specialOf != null ? specialOf.Id : null,
                GoldenMultiplier = goldenMultiplier,
                DeathEffect = deathEffect == DeathEffectKind.None ? null : new DeathEffectData
                {
                    Kind = deathEffect.ToString(),
                    Damage = effectDamage,
                    Radius = effectRadius,
                    MaxTargets = effectMaxTargets,
                    Duration = effectDuration,
                    IntervalMultiplier = effectIntervalMultiplier,
                },
            };

            foreach (Tier tier in tiers)
                data.Tiers.Add(new EnemyTierData { MaxHealth = tier.maxHealth, Size = tier.size, Gold = tier.gold, Exp = tier.exp });

            foreach (StageColor row in stageColors)
            {
                data.StageColors.Add(new StageColorData
                {
                    FromStage = row.fromStage,
                    TierRatios = row.tierRatios != null ? new List<float>(row.tierRatios) : new List<float>(),
                });
            }

            foreach (MassLevel level in massLevels)
            {
                data.MassLevels.Add(new MassLevelData
                {
                    HealthMultiplier = level.healthMultiplier,
                    GoldMultiplier = level.goldMultiplier,
                });
            }

            foreach (SizeClass size in sizeClasses)
            {
                data.SizeClasses.Add(new SizeClassData
                {
                    SizeMultiplier = size.sizeMultiplier,
                    HealthMultiplier = size.healthMultiplier,
                    GoldMultiplier = size.goldMultiplier,
                    ExpMultiplier = size.expMultiplier,
                });
            }

            return data;
        }
    }
}
