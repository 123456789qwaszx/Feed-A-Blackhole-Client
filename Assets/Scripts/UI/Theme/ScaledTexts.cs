using System.Collections.Generic;
using TMPro;

namespace BlackHole.Unity
{
    // 글자 크기 설정을 TextBindings가 닿지 않는 글자에 넣는다(설정 행, 결산 통계 칸, 안내 글자 등).
    // - 처음 넣을 때의 크기를 기준으로 기억하고 "기준 × 배율"로 맞춘다. 다시 넣어도 배율이 겹치지 않는다.
    // - 자동 크기(Auto Size) 글자는 크기 대신 최소·최대 크기를 같은 배율로 바꾼다.
    // - 틀(template)은 넣지 않는다. 복제본이 틀의 원래 크기를 물려받아 기준이 된다.
    // 복제해 만든 글자(행, 선택지)는 지울 때 Clear로 함께 비운다.
    internal sealed class ScaledTexts
    {
        private readonly Dictionary<TMP_Text, Base> _bases = new();
        private readonly List<TMP_Text> _gone = new();

        public float Scale { get; private set; } = 1f;

        public void Add(TMP_Text text)
        {
            if (text == null || _bases.ContainsKey(text))
                return;

            var source = new Base(text.fontSize, text.fontSizeMin, text.fontSizeMax);
            _bases.Add(text, source);
            Apply(text, source);
        }

        public void SetScale(float scale)
        {
            Scale = scale > 0f ? scale : 1f;

            foreach (KeyValuePair<TMP_Text, Base> pair in _bases)
            {
                // 지워진 글자(Unity의 가짜 null)는 건너뛰고 뒤에서 뺀다.
                if (pair.Key == null)
                    _gone.Add(pair.Key);
                else
                    Apply(pair.Key, pair.Value);
            }

            foreach (TMP_Text text in _gone)
                _bases.Remove(text);

            _gone.Clear();
        }

        public void Clear() => _bases.Clear();

        private void Apply(TMP_Text text, Base source)
        {
            if (text.enableAutoSizing)
            {
                text.fontSizeMin = source.Min * Scale;
                text.fontSizeMax = source.Max * Scale;
            }
            else
            {
                text.fontSize = source.Size * Scale;
            }
        }

        private readonly struct Base
        {
            public readonly float Size;
            public readonly float Min;
            public readonly float Max;

            public Base(float size, float min, float max)
            {
                Size = size;
                Min = min;
                Max = max;
            }
        }
    }
}
