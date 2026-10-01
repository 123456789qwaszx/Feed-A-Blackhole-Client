using UnityEngine;
using UnityEngine.InputSystem;

public class SoundTest : MonoBehaviour
{
    [SerializeField] private float _repeatInterval = 0.01f;

    private float _nextTime;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || SoundManager.Instance == null)
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
}
