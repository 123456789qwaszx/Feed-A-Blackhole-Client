using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BlackHole.Unity
{
    // 플레이어 설정. 판 규칙과 관계없는 이 기기의 설정이라 Core가 아니라 여기에 두고, PlayerPrefs에 저장한다.
    // 설정마다 정의(Definition: ID·이름·고치는 방법·기본값·선택지)가 있고, 값은 종류와 관계없이 수 하나로 둔다:
    // 켜기/끄기는 0·1, 막대는 0 ~ 1, 선택지는 번호.
    //
    // 지금 게임에 반영하는 설정은 화면 모드뿐이다. 나머지는 값만 저장한다 —
    // 그 기능(소리, 화면 흔들림 등)을 만들 때 IsOn·LevelOf·IndexOf·PercentOf로 값을 읽고, 바뀜은 Changed로 받는다.
    public sealed class GameSettings
    {
        // 설정을 고치는 방법(설정 창의 행 모양).
        public enum Kind
        {
            Toggle,     // 켜기/끄기
            Slider,     // 0 ~ 1 막대
            Choice,     // 선택지 중 하나(펼침 목록)
            Stepper,    // 선택지 중 하나(◄ ►로 한 칸씩)
        }

        public sealed class Definition
        {
            public string Id { get; }
            public string Label { get; }
            public Kind Kind { get; }
            // 기본값. 켜기/끄기는 0·1, 막대는 0 ~ 1, 선택지는 번호.
            public float Default { get; }
            // 선택지(Choice·Stepper). 다른 종류는 비어 있다.
            public IReadOnlyList<string> Options { get; }

            public Definition(string id, string label, Kind kind, float defaultValue, IReadOnlyList<string> options)
            {
                Id = id;
                Label = label;
                Kind = kind;
                Default = defaultValue;
                Options = options;
            }
        }

        // 설정 ID. PlayerPrefs 키이기도 하다 — 바꾸면 저장된 값을 잃는다.
        public const string DarkMode = "DarkMode";
        public const string RunTimer = "RunTimer";
        public const string MasterVolume = "MasterVolume";
        public const string EffectsVolume = "EffectsVolume";
        public const string MusicVolume = "MusicVolume";
        public const string ShuffleMusic = "ShuffleMusic";
        public const string Language = "Language";
        public const string ScreenMode = "ScreenMode";
        public const string FpsLimit = "FpsLimit";
        public const string VSync = "VSync";
        public const string TextSize = "TextSize";
        public const string ControllerSensitivity = "ControllerSensitivity";
        public const string ShowMoneyText = "ShowMoneyText";
        public const string ShowDamage = "ShowDamage";
        public const string ScreenShake = "ScreenShake";
        public const string BlackHolePulse = "BlackHolePulse";
        public const string MatterParticles = "MatterParticles";
        public const string ObjectRotation = "ObjectRotation";

        private const string KeyPrefix = "Settings.";

        // 선택지는 번호로 저장한다. 순서를 바꾸면 저장된 값의 뜻이 바뀌므로 끝에만 더한다.
        private static readonly string[] NoOptions = Array.Empty<string>();
        private static readonly string[] LanguageOptions = { "English", "Korean" };
        private static readonly string[] ScreenModeOptions = { "Fullscreen", "Exclusive Fullscreen", "Windowed" };
        private static readonly FullScreenMode[] ScreenModeValues =
            { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed };
        private static readonly string[] FpsLimitOptions = { "30", "60", "120", "144", "240", "Unlimited" };
        // 퍼센트 단계: 50% ~ 200%, 10%씩. 100%가 기본.
        private const int PercentMin = 50;
        private const int PercentMax = 200;
        private const int PercentStep = 10;
        private static readonly string[] PercentOptions = Percents();
        private const int DefaultPercentIndex = (100 - PercentMin) / PercentStep;

        // 설정 창에 보이는 순서.
        public static IReadOnlyList<Definition> Definitions { get; } = new[]
        {
            Toggle(DarkMode, "Dark Mode", false),
            Toggle(RunTimer, "Run Timer", false),
            Slider(MasterVolume, "Master Volume", 0.5f),
            Slider(EffectsVolume, "Effects Volume", 0.5f),
            Slider(MusicVolume, "Music Volume", 0.25f),
            Toggle(ShuffleMusic, "Shuffle Music", false),
            Options(Kind.Choice, Language, "Language", LanguageOptions, 0),
            Options(Kind.Choice, ScreenMode, "Screen Mode", ScreenModeOptions, 0),
            Options(Kind.Choice, FpsLimit, "FPS Limit", FpsLimitOptions, FpsLimitOptions.Length - 1),
            Toggle(VSync, "VSync", true),
            Options(Kind.Stepper, TextSize, "Text Size", PercentOptions, DefaultPercentIndex),
            Options(Kind.Stepper, ControllerSensitivity, "Controller Sensitivity", PercentOptions, DefaultPercentIndex),
            Toggle(ShowMoneyText, "Show Money Text", true),
            Toggle(ShowDamage, "Show Damage", true),
            Toggle(ScreenShake, "Screen Shake", true),
            Toggle(BlackHolePulse, "Black Hole Pulse", true),
            Toggle(MatterParticles, "Matter Particles", true),
            Toggle(ObjectRotation, "Object Rotation", true),
        };

        private static readonly Dictionary<string, Definition> ById = IndexById();

        private readonly Dictionary<string, float> _values = new Dictionary<string, float>(StringComparer.Ordinal);

        // 값이 바뀐 설정의 ID. 설정 창에서 고칠 때마다 온다.
        public event Action<string> Changed;

        private GameSettings() { }

        // 저장된 값을 읽는다. 없으면 기본값.
        // 화면 모드만은 저장값 대신 지금 화면 모드를 읽는다 — Unity가 실행 사이에 화면 모드를 기억하므로 그쪽이 사실이다.
        public static GameSettings Load()
        {
            var settings = new GameSettings();

            foreach (Definition definition in Definitions)
                settings._values[definition.Id] = Normalize(definition, PlayerPrefs.GetFloat(KeyPrefix + definition.Id, definition.Default));

            settings._values[ScreenMode] = ScreenModeIndexOf(Screen.fullScreenMode);
            return settings;
        }

        public float ValueOf(string id) => _values[Require(id).Id];
        public bool IsOn(string id) => ValueOf(id) >= 0.5f;
        public float LevelOf(string id) => ValueOf(id);
        public int IndexOf(string id) => Mathf.RoundToInt(ValueOf(id));

        // 퍼센트 단계(Text Size·Controller Sensitivity)의 배율. 100%면 1이다.
        public float PercentOf(string id) => (PercentMin + IndexOf(id) * PercentStep) / 100f;

        public void SetOn(string id, bool on) => Set(id, on ? 1 : 0);
        public void SetLevel(string id, float level) => Set(id, level);
        public void SetIndex(string id, int index) => Set(id, index);

        // PlayerPrefs를 디스크에 쓴다. 설정 창을 닫을 때 부른다(앱을 끌 때는 Unity가 쓴다).
        public void Save() => PlayerPrefs.Save();

        // 종류에 맞게 값을 고른 뒤 저장하고, 게임에 반영할 설정이면 반영한다.
        private void Set(string id, float value)
        {
            Definition definition = Require(id);
            value = Normalize(definition, value);

            if (_values[id] == value)
                return;

            _values[id] = value;
            PlayerPrefs.SetFloat(KeyPrefix + id, value);

            if (id == ScreenMode)
                ApplyScreenMode(ScreenModeValues[IndexOf(ScreenMode)]);

            Changed?.Invoke(id);
        }

        // 전체 화면은 모니터 해상도로, 창 모드는 모니터의 3/4 크기 창으로 바꾼다.
        private static void ApplyScreenMode(FullScreenMode mode)
        {
            int width = Display.main.systemWidth;
            int height = Display.main.systemHeight;

            if (mode == FullScreenMode.Windowed)
            {
                width = width * 3 / 4;
                height = height * 3 / 4;
            }

            Screen.SetResolution(width, height, mode);
        }

        private static int ScreenModeIndexOf(FullScreenMode mode)
        {
            switch (mode)
            {
                case FullScreenMode.ExclusiveFullScreen: return 1;
                case FullScreenMode.MaximizedWindow:
                case FullScreenMode.Windowed: return 2;
                default: return 0;
            }
        }

        private static float Normalize(Definition definition, float value)
        {
            switch (definition.Kind)
            {
                case Kind.Toggle: return value >= 0.5f ? 1 : 0;
                case Kind.Slider: return Mathf.Clamp01(value);
                default: return Mathf.Clamp(Mathf.Round(value), 0, definition.Options.Count - 1);
            }
        }

        private static Definition Require(string id)
        {
            if (id != null && ById.TryGetValue(id, out Definition definition))
                return definition;

            throw new ArgumentException($"알 수 없는 설정 ID '{id}'.", nameof(id));
        }

        private static Definition Toggle(string id, string label, bool on) =>
            new Definition(id, label, Kind.Toggle, on ? 1 : 0, NoOptions);

        private static Definition Slider(string id, string label, float level) =>
            new Definition(id, label, Kind.Slider, level, NoOptions);

        private static Definition Options(Kind kind, string id, string label, string[] options, int index) =>
            new Definition(id, label, kind, index, options);

        private static string[] Percents()
        {
            var options = new string[(PercentMax - PercentMin) / PercentStep + 1];

            for (int i = 0; i < options.Length; i++)
                options[i] = (PercentMin + i * PercentStep).ToString(CultureInfo.InvariantCulture) + "%";

            return options;
        }

        private static Dictionary<string, Definition> IndexById()
        {
            var byId = new Dictionary<string, Definition>(StringComparer.Ordinal);

            foreach (Definition definition in Definitions)
                byId.Add(definition.Id, definition);

            return byId;
        }
    }
}
