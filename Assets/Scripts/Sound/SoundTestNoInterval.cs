using UnityEngine;
using UnityEngine.InputSystem;

public class SoundTestNoInterval : MonoBehaviour
{
    [SerializeField] private AudioSource _source;
    [SerializeField] private UISoundSetup _soundSetup;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || _source == null || _soundSetup == null)
        {
            return;
        }

        if (keyboard.qKey.isPressed)
        {
            _source.PlayOneShot(_soundSetup.Hit);
        }

        if (keyboard.wKey.isPressed)
        {
            _source.PlayOneShot(_soundSetup.Destroyed);
        }

        if (keyboard.eKey.isPressed)
        {
            _source.PlayOneShot(_soundSetup.Lightning);
        }

        if (keyboard.rKey.isPressed)
        {
            _source.PlayOneShot(_soundSetup.Razer);
        }
    }
}
