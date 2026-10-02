using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class SoundTest : MonoBehaviour
{
    [Serializable]
    public class AutoEntry
    {
        public string name;
        public bool enabled;
        [Min(0.01f)] public float interval = 0.1f;
        [NonSerialized] public float nextTime;
    }

    [Header("키보드 테스트 (1~6 꾹 누르기)")]
    [SerializeField] private float _repeatInterval = 0.01f;

    [Header("자동 재생 (소리별 간격)")]
    [SerializeField]
    private AutoEntry[] _autoEntries = new AutoEntry[]
    {
        new AutoEntry { name = "Hit", enabled = true, interval = 0.1f },
        new AutoEntry { name = "Destroyed", interval = 0.1f },
        new AutoEntry { name = "Lightning", interval = 0.1f },
        new AutoEntry { name = "Razer", interval = 0.1f },
        new AutoEntry { name = "MoonComet", interval = 0.1f },
        new AutoEntry { name = "SuperNova", interval = 0.1f },
    };

    private float _nextTime;
    private bool _autoPlaying;

    public bool AutoPlaying
    {
        get { return _autoPlaying; }
    }

    public void StartAuto()
    {
        _autoPlaying = true;

        for (int i = 0; i < _autoEntries.Length; i++)
        {
            _autoEntries[i].nextTime = 0f;
        }
    }

    private void Start()
    {
        StartAuto();
    }

    public void StopAuto()
    {
        _autoPlaying = false;
    }

    private void Update()
    {
        if (SoundManager.Instance == null)
        {
            return;
        }

        UpdateKeyboard();
        UpdateAuto();
    }

    private void UpdateKeyboard()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (Time.unscaledTime < _nextTime)
        {
            return;
        }

        SoundManager sound = SoundManager.Instance;
        bool played = false;

        if (keyboard.digit1Key.isPressed)
        {
            sound.PlayHit();
            played = true;
        }

        if (keyboard.digit2Key.isPressed)
        {
            sound.PlayDestroyed();
            played = true;
        }

        if (keyboard.digit3Key.isPressed)
        {
            sound.PlayLightning();
            played = true;
        }

        if (keyboard.digit4Key.isPressed)
        {
            sound.PlayRazer();
            played = true;
        }

        if (keyboard.digit5Key.isPressed)
        {
            sound.PlayMoonComet();
            played = true;
        }

        if (keyboard.digit6Key.isPressed)
        {
            sound.PlaySuperNova();
            played = true;
        }

        if (played)
        {
            _nextTime = Time.unscaledTime + _repeatInterval;
        }
    }

    private void UpdateAuto()
    {
        if (!_autoPlaying)
        {
            return;
        }

        float now = Time.unscaledTime;

        for (int i = 0; i < _autoEntries.Length; i++)
        {
            AutoEntry entry = _autoEntries[i];

            if (!entry.enabled || now < entry.nextTime)
            {
                continue;
            }

            entry.nextTime = now + entry.interval;
            PlayByIndex(i);
        }
    }

    // 배열 순서: 0 Hit, 1 Destroyed, 2 Lightning, 3 Razer, 4 MoonComet, 5 SuperNova
    private void PlayByIndex(int index)
    {
        SoundManager sound = SoundManager.Instance;

        switch (index)
        {
            case 0: sound.PlayHit(); break;
            case 1: sound.PlayDestroyed(); break;
            case 2: sound.PlayLightning(); break;
            case 3: sound.PlayRazer(); break;
            case 4: sound.PlayMoonComet(); break;
            case 5: sound.PlaySuperNova(); break;
        }
    }
}
