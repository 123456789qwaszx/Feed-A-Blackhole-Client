using UnityEngine;

// 사운드 파일 저장 SO
[CreateAssetMenu(fileName = "UISoundSetup", menuName = "Scriptable Objects/UISoundSetup")]
public class UISoundSetup : ScriptableObject
{
    
    [Header("클릭"), SerializeField] private AudioClip _clickSound;

    public AudioClip Click { get { return _clickSound; }}
}
