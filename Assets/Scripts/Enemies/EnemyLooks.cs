using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 적 종류의 외형 조회: 종류 ID → 스프라이트, (종류 ID, 색 등급) → 색. 외형은 적 종류 에셋(EnemyKind)이 가진다.
    // 스프라이트가 없는 종류와 목록에 없는 종류는 임시 원(흰색)이다. 적 화면과 조종 콘솔이 같은 외형을 쓴다.
    internal sealed class EnemyLooks : IDisposable
    {
        private const int DiscPixels = 64;

        // 황금 천체의 속 색. 원작의 황금 소행성은 원래 색의 윤곽에 속이 노랗다(BATTLE_COMPOSITION_PLAN 2.1).
        public static readonly Color GoldenFill = new Color(1f, 0.82f, 0.2f);
        // 황금 천체에서 노란 속이 차지하는 크기(윤곽 대비). 나머지 테두리가 원래 색으로 보인다 [임시].
        public const float GoldenFillScale = 0.65f;

        private readonly Dictionary<string, EnemyKind> _kinds = new Dictionary<string, EnemyKind>(StringComparer.Ordinal);
        private readonly Texture2D _discTexture;
        private readonly Sprite _disc;

        public EnemyLooks(IReadOnlyList<EnemyKind> kinds)
        {
            foreach (EnemyKind kind in kinds)
            {
                if (kind != null && !string.IsNullOrEmpty(kind.Id))
                    _kinds[kind.Id] = kind;
            }

            _discTexture = CreateDisc();
            _disc = Sprite.Create(_discTexture, new Rect(0, 0, DiscPixels, DiscPixels), new Vector2(0.5f, 0.5f), DiscPixels);
        }

        public Sprite SpriteOf(string kindId) =>
            _kinds.TryGetValue(kindId, out EnemyKind kind) && kind.Sprite != null ? kind.Sprite : _disc;

        public Color ColorOf(string kindId, int tier) =>
            _kinds.TryGetValue(kindId, out EnemyKind kind) ? kind.ColorOf(tier) : Color.white;

        public void Dispose()
        {
            Object.Destroy(_disc);
            Object.Destroy(_discTexture);
        }

        // 지름 1 단위(DiscPixels 픽셀)의 흰 원. 가장자리 한 픽셀을 부드럽게 한다.
        private static Texture2D CreateDisc()
        {
            var texture = new Texture2D(DiscPixels, DiscPixels, TextureFormat.RGBA32, false)
            {
                name = "Enemy Disc",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float center = (DiscPixels - 1) / 2f;
            float radius = DiscPixels / 2f - 0.5f;
            var pixels = new Color32[DiscPixels * DiscPixels];

            for (int y = 0; y < DiscPixels; y++)
            {
                for (int x = 0; x < DiscPixels; x++)
                {
                    float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    byte alpha = (byte)(Mathf.Clamp01(radius - distance) * 255);
                    pixels[y * DiscPixels + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
