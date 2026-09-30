using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 적 종류의 외형 조회: 종류 ID → 스프라이트, (종류 ID, 색 등급) → 색. 외형은 적 종류 에셋(EnemyKind)이 가진다.
    // 스프라이트가 없는 종류와 목록에 없는 종류는 임시 다각형(흰색)이다. 적 화면과 조종 콘솔이 같은 외형을 쓴다.
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

        // 황금 천체의 속 색. 원작의 황금 소행성은 원래 색의 윤곽에 속이 노랗다(BATTLE_COMPOSITION_PLAN 2.1).
        public static readonly Color GoldenFill = new Color(1f, 0.82f, 0.2f);
        // 황금 천체에서 노란 속이 차지하는 크기(윤곽 대비). 나머지 테두리가 원래 색으로 보인다 [임시].
        public const float GoldenFillScale = 0.65f;

        private readonly Dictionary<string, EnemyKind> _kinds = new Dictionary<string, EnemyKind>(StringComparer.Ordinal);
        private readonly Texture2D[] _shapeTextures = new Texture2D[ShapeVertices.Length];
        private readonly Sprite[] _shapes = new Sprite[ShapeVertices.Length];

        public EnemyLooks(IReadOnlyList<EnemyKind> kinds)
        {
            foreach (EnemyKind kind in kinds)
            {
                if (kind != null && !string.IsNullOrEmpty(kind.Id))
                    _kinds[kind.Id] = kind;
            }

            for (int i = 0; i < ShapeVertices.Length; i++)
            {
                _shapeTextures[i] = CreateShape(i);
                _shapes[i] = Sprite.Create(_shapeTextures[i], new Rect(0, 0, ShapePixels, ShapePixels), new Vector2(0.5f, 0.5f), ShapePixels);
            }
        }

        public Sprite SpriteOf(string kindId, int enemyId) =>
            _kinds.TryGetValue(kindId, out EnemyKind kind) && kind.Sprite != null
                ? kind.Sprite
                : _shapes[(enemyId - 1) % _shapes.Length];

        public Color ColorOf(string kindId, int tier) =>
            _kinds.TryGetValue(kindId, out EnemyKind kind) ? kind.ColorOf(tier) : Color.white;

        public void Dispose()
        {
            for (int i = 0; i < _shapes.Length; i++)
            {
                Object.Destroy(_shapes[i]);
                Object.Destroy(_shapeTextures[i]);
            }
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
    }
}
