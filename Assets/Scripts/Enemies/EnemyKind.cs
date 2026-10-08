using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 적 종류 하나의 저작 에셋: 규칙 수치, 외형. 종류(EnemyType)마다 에셋이 하나이고 적 종류 목록(EnemyCatalog)에 넣는다.
    // 움직임은 모든 종류가 HQ 공전이다(EnemyBehaviors).
    // 규칙 칸은 Core의 저작 형식(EnemyData)으로 옮겨져 EnemyContentLoader가 검증한다.
    // 외형 칸(스프라이트, 색 등급의 색)은 Core로 가지 않고 화면(EnemyView)만 읽는다. 규칙과 외형이 한 에셋에 있어 외형 연결이 빠지지 않는다.
    // 모든 칸은 이 에셋을 Inspector에서 직접 고친다.
    //
    // 종류는 계열(소행성·행성·별, 픽업인 혜성)이고 색은 종류 안에 둔다.
    // - 반지름: 크기 1의 반지름. 모든 색이 같다.
    // - 색 등급: 같은 윤곽(스프라이트)에 색마다 색·HP·Gold·EXP가 다르다. 공급되는 종류는 6색(빨주노초파보), 픽업은 한 줄이다.
    // - 어떤 색이 나오는지는 그 종류의 질량(노드 asteroid.massScale 등, MassRule), 어떤 크기가 나오는지는 그 종류의 크기
    //   (노드 asteroid.size 등, SizeRule)가 정한다. 둘 다 종류마다 따로이고 이 에셋에는 칸이 없다.
    // - 특수 성질: 황금·전기·달·레이저·슈퍼노바는 종류가 아니라 출현 때 한 마리에 붙는 성질이다(최대 하나, 배타).
    //   성질 종류(EnemyTraitType)가 사망 효과를 정하고, 성질마다 효과 수치와 표식 색이 있다. 붙는 확률은 노드(electricAsteroid.spawnChance 등)가 정하고 기본 0%다.
    //   성질이 붙은 적(특수 적)은 사망 효과의 피해를 받지 않는다.
    // - 출현 주기: 0보다 크면 공급되지 않는 픽업(혜성)이다. 주기마다 등장 확률(노드 comet.spawnChance)로 나오고, 성질 하나가 언제나 붙는다.
    [CreateAssetMenu(fileName = "EnemyKind", menuName = "BlackHole/Enemy Kind")]
    public sealed class EnemyKind : ScriptableObject
    {
        [Serializable]
        public struct Trait
        {
            [Tooltip("성질 종류(종류 안에서 유일). 사망 효과를 정한다: Golden 황금, Electric 연쇄 번개, Moon 달 버프, Laser 레이저, Supernova 폭발, Comet 혜성 버프.")]
            public EnemyTraitType type;
            [Tooltip("이 성질이 붙은 적의 표식 색. Core는 모른다.")]
            public Color color;
            [Tooltip("Golden: Gold 배율.")]
            public float multiplier;
            [Tooltip("Golden: 치명타 Gold 배율.")]
            public float critRewardScale;
            [Tooltip("Electric·Laser: 피해.")]
            public float damage;
            [Tooltip("Electric: 번개가 한 번 옮겨 가는 최대 거리. Supernova: 폭발 반경.")]
            public float radius;
            [Tooltip("Electric: 한 줄기가 옮겨 가는 최대 횟수.")]
            public int maxTargets;
            [Tooltip("Electric: 줄기가 하나 더 나갈 확률(0 ~ 1).")]
            public float branchChance;
            [Tooltip("Electric·Laser·Golden: 치명타 확률(0 ~ 1).")]
            public float critChance;
            [Tooltip("Electric·Laser: 치명타일 때 피해 배율.")]
            public float critMultiplier;
            [Tooltip("Supernova: 맞는 순간 대상의 현재 HP에 대한 피해 비율(0 ~ 1). 올림, 최소 1.")]
            public float healthFraction;
            [Tooltip("Laser: 레이저 너비.")]
            public float width;
            [Tooltip("이 성질의 동시 상한: 살아 있는 그 성질 적 + Breaker에 남은 그 버프 중첩. 0이면 상한 없음. 다 찼으면 뽑혀도 붙지 않는다(원작 달 최대 개수, 노드 moonPlanet.maxCount).")]
            public int maxActive;
        }

        [Serializable]
        public struct Tier
        {
            [Tooltip("이 색 등급을 그리는 색. Core는 모른다.")]
            public Color color;
            public float maxHealth;
            [Tooltip("사망 때 판의 합계에 드는 Gold의 기본값(크기 1). 0 이상.")]
            public long gold;
            [Tooltip("사망 때 블랙홀에 드는 EXP. 0 이상. 크기는 곱하고, 황금은 곱하지 않는다.")]
            public long exp;
        }

        [Tooltip("적 종류. 적 종류 목록에는 종류마다 에셋 하나만 넣는다.")]
        [SerializeField] private EnemyType type;

        [Tooltip("초당 이동 거리. 모든 색 등급이 같다. 0이 아닌 값이고, 부호가 공전 방향이다(양수 = 반시계, 음수 = 시계방향).")]
        [SerializeField] private float moveSpeed = 1;

        [Tooltip("크기 1의 반지름. 모든 색이 같다. 공격 판정과 화면에 그리는 크기다. 크기 k는 1 + radiusStep × (k − 1)배.")]
        [SerializeField] private float radius = 0.25f;

        [Tooltip("크기가 1 오를 때 늘어나는 반지름(크기 1의 반지름 대비, 0 이상). 원작 실측: 소행성 0.35, 행성 0.09, 별 0.33. 픽업은 크기가 없다.")]
        [SerializeField] private float radiusStep = 0.5f;

        [Header("종류 사이")]
        [Tooltip("판 시작 때 시작 공급 중 변환 수(노드 asteroid.toPlanet·planet.toStar, 마리 수)만큼 바뀌는 다음 종류. 비우면 변환하지 않는다. 적 종류 목록에 있어야 한다.")]
        [SerializeField] private EnemyKind upgradesTo;
        [Tooltip("0보다 크면 픽업(혜성): 공급되지 않고 이 주기(초)마다 등장 확률(노드 comet.spawnChance, %)로 나온다. 성질이 정확히 하나여야 한다.")]
        [SerializeField] private float spawnPeriod;
        [Tooltip("픽업: 혜성 비(노드 comet.rainChance)일 때 한꺼번에 나오는 수. 0이면 혜성 비가 없다.")]
        [SerializeField] private int rainCount;
        [Header("색 등급 (번호가 적의 색 등급, 빨주노초파보)")]
        [SerializeField] private List<Tier> tiers = new List<Tier>();

        [Header("특수 성질 (출현 때 최대 하나가 붙는다)")]
        [SerializeField] private List<Trait> traits = new List<Trait>();

        [Header("외형 (Core는 모른다)")]
        [Tooltip("모든 색 등급이 같은 윤곽을 쓴다. 비우면 임시 원으로 그린다.")]
        [SerializeField] private Sprite sprite;

        public EnemyType Type => type;
        public Sprite Sprite => sprite;

        // 색 등급의 색. 없는 번호는 흰색이다.
        public Color ColorOf(int tier) => tier >= 0 && tier < tiers.Count ? tiers[tier].color : Color.white;

        // 성질의 표식 색. 없는 성질은 투명이다.
        public Color TraitColorOf(EnemyTraitType trait)
        {
            for (int i = 0; i < traits.Count; i++)
            {
                if (traits[i].type == trait)
                    return traits[i].color;
            }

            return Color.clear;
        }

        internal EnemyData ToData()
        {
            var data = new EnemyData
            {
                Type = type,
                MoveSpeed = moveSpeed,
                Radius = radius,
                RadiusStep = radiusStep,
                UpgradesTo = upgradesTo != null ? upgradesTo.Type : (EnemyType?)null,
                SpawnPeriod = spawnPeriod,
                RainCount = rainCount,
            };

            foreach (Trait trait in traits)
            {
                data.Traits.Add(new EnemyTraitData
                {
                    Type = trait.type,
                    MaxActive = trait.maxActive,
                    Effect = new DeathEffectData
                    {
                        Multiplier = trait.multiplier,
                        CritRewardScale = trait.critRewardScale,
                        Damage = trait.damage,
                        Radius = trait.radius,
                        MaxTargets = trait.maxTargets,
                        BranchChance = trait.branchChance,
                        CritChance = trait.critChance,
                        CritMultiplier = trait.critMultiplier,
                        HealthFraction = trait.healthFraction,
                        Width = trait.width,
                    },
                });
            }

            foreach (Tier tier in tiers)
                data.Tiers.Add(new EnemyTierData { MaxHealth = tier.maxHealth, Gold = tier.gold, Exp = tier.exp });

            return data;
        }
    }
}
