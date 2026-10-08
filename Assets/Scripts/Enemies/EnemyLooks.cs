using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 적 종류의 외형 조회: 종류 → 스프라이트, (종류, 색 등급) → 색. 외형은 적 종류 에셋(EnemyKind)이 가진다.
    // 스프라이트가 없는 종류와 목록에 없는 종류는 임시 다각형(흰색)이다. 적 화면과 조종 콘솔이 같은 외형을 쓴다.
    // 혜성(픽업)은 일반 적과 성격이 달라 여기서 다루지 않는다. 스프라이트가 아니라 셰이더로 그리며, 외형은 CometLook이 가진다.
    internal sealed class EnemyLooks : IDisposable
    {
        private const int ShapePixels = 64;
        private static readonly Vector2[][] ShapeVertices =
        {
            new[] { new Vector2(-0.47f, 0.16f), new Vector2(-0.18f, 0.49f), new Vector2(0.43f, 0.36f), new Vector2(0.34f, -0.27f), new Vector2(-0.32f, -0.45f) },
            new[] { new Vector2(-0.49f, 0.01f), new Vector2(-0.27f, 0.43f), new Vector2(0.18f, 0.49f), new Vector2(0.47f, 0.12f), new Vector2(0.29f, -0.43f) },
            new[] { new Vector2(-0.43f, -0.33f), new Vector2(-0.39f, 0.28f), new Vector2(-0.05f, 0.49f), new Vector2(0.45f, 0.23f), new Vector2(0.24f, -0.39f) },
            new[] { new Vector2(-0.49f, 0.31f), new Vector2(-0.07f, 0.48f), new Vector2(0.27f, 0.27f), new Vector2(0.48f, 0.02f), new Vector2(0.35f, -0.45f), new Vector2(-0.29f, -0.34f) },
            new[] { new Vector2(-0.49f, -0.05f), new Vector2(-0.31f, 0.33f), new Vector2(-0.07f, 0.49f), new Vector2(0.37f, 0.34f), new Vector2(0.48f, -0.16f), new Vector2(0.06f, -0.43f) },
        };

        // 특수 성질 표식(속 채움)이 차지하는 크기(윤곽 대비). 나머지 테두리가 원래 색으로 보인다 [임시].
        // 원작의 황금 소행성은 원래 색의 윤곽에 속이 노랗다(BATTLE_COMPOSITION_PLAN 2.1). 다른 성질도 같은 방식의 임시 표식이다.
        public const float TraitFillScale = 0.65f;

        private readonly Dictionary<EnemyType, EnemyKind> _kinds = new Dictionary<EnemyType, EnemyKind>();
        private readonly Texture2D[] _shapeTextures = new Texture2D[ShapeVertices.Length];
        private readonly Sprite[] _shapes = new Sprite[ShapeVertices.Length];

        // 계열별 전용 도형(행성·별). 소행성은 위 _shapes 5종을 그대로 사용. 혜성은 도형이 아니라 셰이더로 그린다(EnemyView).
        private Texture2D _planetTexture;
        private Sprite _planetShape;
        private Texture2D _starTexture;
        private Sprite _starShape;

        public EnemyLooks(IReadOnlyList<EnemyKind> kinds)
        {
            foreach (EnemyKind kind in kinds)
            {
                if (kind != null)
                    _kinds[kind.Type] = kind;
            }

            for (int i = 0; i < ShapeVertices.Length; i++)
            {
                _shapeTextures[i] = CreateShape(i);
                _shapes[i] = Sprite.Create(_shapeTextures[i], new Rect(0, 0, ShapePixels, ShapePixels), new Vector2(0.5f, 0.5f), ShapePixels);
            }

            _planetTexture = CreateCircle();
            _planetShape = Sprite.Create(_planetTexture, new Rect(0, 0, ShapePixels, ShapePixels), new Vector2(0.5f, 0.5f), ShapePixels);

            _starTexture = CreateStarBurst();
            _starShape = Sprite.Create(_starTexture, new Rect(0, 0, ShapePixels, ShapePixels), new Vector2(0.5f, 0.5f), ShapePixels);
        }

        public Sprite SpriteOf(EnemyType type, int enemyId)
        {
            if (_kinds.TryGetValue(type, out EnemyKind kind) && kind.Sprite != null)
                return kind.Sprite;

            switch (type)
            {
                case EnemyType.Planet:
                    return _planetShape;
                case EnemyType.Star:
                    return _starShape;
                default:
                    // 소행성을 비롯해 전용 도형이 없는 종류는 기존 불규칙 다각형 5종을 그대로 쓴다.
                    return _shapes[(enemyId - 1) % _shapes.Length];
            }
        }

        public Color ColorOf(EnemyType type, int tier) =>
            _kinds.TryGetValue(type, out EnemyKind kind) ? kind.ColorOf(tier) : Color.white;

        // 성질의 표식 색. 없는 종류·성질은 투명이다(표식을 그리지 않는다).
        public Color TraitColorOf(EnemyType type, EnemyTraitType? trait) =>
            trait.HasValue && _kinds.TryGetValue(type, out EnemyKind kind) ? kind.TraitColorOf(trait.Value) : Color.clear;

        public void Dispose()
        {
            for (int i = 0; i < _shapes.Length; i++)
            {
                Object.Destroy(_shapes[i]);
                Object.Destroy(_shapeTextures[i]);
            }

            Object.Destroy(_planetShape);
            Object.Destroy(_planetTexture);
            Object.Destroy(_starShape);
            Object.Destroy(_starTexture);
        }

        // 꼭짓점 안쪽을 흰색으로 채우고 가장자리 한 픽셀을 부드럽게 한다.
        private static Texture2D CreateShape(int shapeIndex)
        {
            var texture = new Texture2D(ShapePixels, ShapePixels, TextureFormat.RGBA32, false)
            {
                name = $"Enemy Polygon {shapeIndex + 1}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float center = (ShapePixels - 1) / 2f;
            Vector2[] vertices = new Vector2[ShapeVertices[shapeIndex].Length];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = new Vector2(center, center) + ShapeVertices[shapeIndex][i] * ShapePixels;

            var pixels = new Color32[ShapePixels * ShapePixels];

            for (int y = 0; y < ShapePixels; y++)
            {
                for (int x = 0; x < ShapePixels; x++)
                {
                    Vector2 point = new Vector2(x, y);
                    bool inside = false;
                    float minDistanceSquared = float.MaxValue;

                    for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
                    {
                        Vector2 start = vertices[j];
                        Vector2 end = vertices[i];
                        if ((start.y > y) != (end.y > y) && x < (end.x - start.x) * (y - start.y) / (end.y - start.y) + start.x)
                            inside = !inside;

                        Vector2 edge = end - start;
                        float projection = Mathf.Clamp01(Vector2.Dot(point - start, edge) / edge.sqrMagnitude);
                        minDistanceSquared = Mathf.Min(minDistanceSquared, (point - (start + projection * edge)).sqrMagnitude);
                    }

                    float distance = Mathf.Sqrt(minDistanceSquared) * (inside ? 1 : -1);
                    byte alpha = (byte)(Mathf.Clamp01(distance + 0.5f) * 255);
                    pixels[y * ShapePixels + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // 완전한 원(행성).
        private static Texture2D CreateCircle()
        {
            var texture = new Texture2D(ShapePixels, ShapePixels, TextureFormat.RGBA32, false)
            {
                name = "Enemy Circle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float center = (ShapePixels - 1) / 2f;
            float radius = ShapePixels * 0.49f;
            var pixels = new Color32[ShapePixels * ShapePixels];

            for (int y = 0; y < ShapePixels; y++)
            {
                for (int x = 0; x < ShapePixels; x++)
                {
                    float distance = radius - new Vector2(x - center, y - center).magnitude;
                    pixels[y * ShapePixels + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(distance + 0.5f) * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // 산등성이처럼 완만한 태양 모양(별). 10개 돌기, 골 깊이는 바깥 반지름의 20%.
        private static Texture2D CreateStarBurst()
        {
            const int Spikes = 10;
            const float InnerRatio = 0.8f;

            var texture = new Texture2D(ShapePixels, ShapePixels, TextureFormat.RGBA32, false)
            {
                name = "Enemy Star Burst",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float center = (ShapePixels - 1) / 2f;
            float outerRadius = ShapePixels * 0.49f;
            float innerRadius = outerRadius * InnerRatio;
            var pixels = new Color32[ShapePixels * ShapePixels];

            for (int y = 0; y < ShapePixels; y++)
            {
                for (int x = 0; x < ShapePixels; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    // 코사인 굴곡: 선형 삼각파와 달리 돌기 끝과 골 양쪽 다 꺾이는 모서리 없이 매끄럽게 이어진다.
                    float wave = (1f - Mathf.Cos(angle * Spikes)) * 0.5f; // 0(골) ~ 1(돌기 끝)
                    float edgeRadius = innerRadius + (outerRadius - innerRadius) * wave;

                    float distance = edgeRadius - r;
                    pixels[y * ShapePixels + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(distance + 0.5f) * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
