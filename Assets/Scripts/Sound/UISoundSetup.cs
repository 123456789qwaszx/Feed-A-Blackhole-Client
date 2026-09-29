using UnityEngine;

// 사운드 파일 저장 SO
[CreateAssetMenu(fileName = "UISoundSetup", menuName = "Scriptable Objects/UISoundSetup")]
public class UISoundSetup : ScriptableObject
{
    
    [Header("사운드"), SerializeField] private AudioClip _audioClip;

    public AudioClip Click { get { return _audioClip; }}
}
