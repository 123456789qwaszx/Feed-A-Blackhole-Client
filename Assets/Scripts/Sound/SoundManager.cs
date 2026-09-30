using System.Collections;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("컴포넌트 Reset버튼 누르면 됩니다")]
    [SerializeField] private AudioSource _sfxSource;
    [SerializeField] private AudioSource _bgmSource;

    [Header("SoundSO를 직접 추가"), SerializeField] private UISoundSetup _soundSetup;

    private Coroutine _bgmRoutine;

    private void Reset()
    {
        AudioSource[] sources = GetComponentsInChildren<AudioSource>();
        if (sources.Length > 0) _sfxSource = sources[0];
        if (sources.Length > 1) _bgmSource = sources[1];
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

    private void Start()
    {
        StartBgm();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    #region SFX 재생

    /// <summary>
    /// 사운드 재생
    /// </summary>
    /// <param name="clip">재생할 sfx 파일</param>
    private void Play(AudioClip clip)
    {
        if (clip != null) _sfxSource.PlayOneShot(clip);
    }

    /// <summary>
    /// 클릭 전용 사운드 재생
    /// </summary>
    public void PlayClick()
    {
        Play(_soundSetup.Click);
    }

    /// <summary>
    ///  호버 전용 사운드 재생
    /// </summary>
    public void PlayHover()
    {
        Play(_soundSetup.Hover);
    }

    #endregion

    #region BGM 재생

    /// <summary>
    /// BGM 사운드 재생
    /// </summary>
    public void StartBgm()
    {
        if (_soundSetup.BgmList.Count == 0) return; // Bgm 리스트 비어있으면 실행 X

        if (_bgmRoutine != null) StopCoroutine(_bgmRoutine);
        _bgmRoutine = StartCoroutine(BgmLoop());
    }

    /// <summary>
    /// BGM 사운드 정지
    /// </summary>
    public void StopBgm()
    {
        if (_bgmRoutine != null) StopCoroutine(_bgmRoutine);
        _bgmRoutine = null;
        _bgmSource.Stop();
    }

    // BGM 반복 코루틴
    private IEnumerator BgmLoop()
    {
        int index = 0;

        while (true)
        {
            AudioClip clip = _soundSetup.BgmList[index];

            if (clip != null)
            {
                _bgmSource.clip = clip;
                _bgmSource.Play();
                yield return new WaitForSecondsRealtime(clip.length); // BGM길이만큼 대기
            }
            else yield return null; // null 칸이면 한 프레임 쉬고 다음 곡으로

            index = (index + 1) % _soundSetup.BgmList.Count;
        }
    }

    #endregion
}
