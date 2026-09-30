using System.Collections.Generic;
using UnityEngine;

// 사운드 파일 저장 SO
[CreateAssetMenu(fileName = "UISoundSetup", menuName = "Scriptable Objects/UISoundSetup")]
public class UISoundSetup : ScriptableObject
{

    [Header("Click"), SerializeField] private AudioClip _clickSound;
    [Header("Hover"), SerializeField] private AudioClip _hoverSound;
    [Header("BGM"), SerializeField] private List<AudioClip> _bgmSound;

    public AudioClip Click { get { return _clickSound; } }
    public AudioClip Hover { get { return _hoverSound; } }
    public IReadOnlyList<AudioClip> BgmList { get { return _bgmSound; } }
}
