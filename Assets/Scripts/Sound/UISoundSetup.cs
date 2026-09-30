using System.Collections.Generic;
using UnityEngine;

// 사운드 파일 저장 SO
[CreateAssetMenu(fileName = "UISoundSetup", menuName = "Scriptable Objects/UISoundSetup")]
public class UISoundSetup : ScriptableObject
{

    [Header("BGM"), SerializeField] private List<AudioClip> _bgmSound;
    [Header("Click"), SerializeField] private AudioClip _clickSound;
    [Header("Hover"), SerializeField] private AudioClip _hoverSound;
    [Header("Hit"), SerializeField] private AudioClip _hitSound;
    [Header("Destroy"), SerializeField] private AudioClip _destroySound;
    [Header("LevelUp"), SerializeField] private AudioClip _levelUpSound;
    [Header("Lightning"), SerializeField] private AudioClip _lightningSound;
    [Header("Razer"), SerializeField] private AudioClip _razerSound;
    [Header("Slider"), SerializeField] private AudioClip _sliderSound;
    [Header("Closing"), SerializeField] private AudioClip _closingSound;

    public IReadOnlyList<AudioClip> BgmList { get { return _bgmSound; } }
    public AudioClip Click { get { return _clickSound; } }
    public AudioClip Hover { get { return _hoverSound; } }
    public AudioClip Hit { get { return _hitSound; } }
    public AudioClip Destroyed { get { return _destroySound; } }
    public AudioClip LevelUp { get { return _levelUpSound; } }
    public AudioClip Lightning { get { return _lightningSound; } }
    public AudioClip Razer { get { return _razerSound; } }
    public AudioClip Slider { get { return _sliderSound; } }
    public AudioClip Closing { get { return _closingSound; } }
}
