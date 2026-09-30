using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("컴포넌트 Reset버튼 누르면 됩니다"), SerializeField] private AudioSource _audioSource;
    [Header("SoundSO를 추가"), SerializeField] private UISoundSetup _soundSetup;

    private void Reset()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 사운드 재생
    /// </summary>
    /// <param name="clip">재생할 사운드 파일</param>
    private void Play(AudioClip clip)
    {
        if (clip != null) _audioSource.PlayOneShot(clip);
    }

    /// <summary>
    /// 클릭 전용 사운드 재생
    /// </summary>
    public void PlayClick()
    {
        Play(_soundSetup.Click);
    }
}
