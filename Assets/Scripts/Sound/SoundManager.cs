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

    public float BgmVolume { get { return _bgmSource.volume; } }
    public float SfxVolume { get { return _sfxSource.volume; } }

    private const string BgmKey = "BgmVolume";
    private const string SfxKey = "SfxVolume";

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
        _bgmSource.volume = PlayerPrefs.GetFloat(BgmKey, 0.5f); // 저장된 값 없으면 0.5로 설정
        _bgmSource.volume = PlayerPrefs.GetFloat(SfxKey, 0.5f);
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
    /// 클릭 사운드 재생
    /// </summary>
    public void PlayClick()
    {
        Play(_soundSetup.Click);
    }

    /// <summary>
    ///  호버 사운드 재생
    /// </summary>
    public void PlayHover()
    {
        Play(_soundSetup.Hover);
    }

    /// <summary>
    /// 타격 사운드 재생
    /// </summary>
    public void PlayHit()
    {
        Play(_soundSetup.Hit);
    }

    /// <summary>
    /// 타격 못했을 때 사운드 재생
    /// </summary>
    public void PlayWhiff()
    {
        Play(_soundSetup.Whiff);
    }

    /// <summary>
    /// 행성 파괴 사운드 재생
    /// </summary>
    public void PlayDestroyed()
    {
        Play(_soundSetup.Destroyed);
    }

    /// <summary>
    /// 블랙홀 레벨업 사운드 재생
    /// </summary>
    public void PlayLevelUp()
    {
        Play(_soundSetup.LevelUp);
    }

    /// <summary>
    /// 번개 효과 사운드 재생
    /// </summary>
    public void PlayLightning()
    {
        Play(_soundSetup.Lightning);
    }

    /// <summary>
    /// 레이저 효과 사운드 재생
    /// </summary>
    public void PlayRazer()
    {
        Play(_soundSetup.Razer);
    }

    /// <summary>
    /// 달, 혜성 획득 사운드 재생
    /// </summary>
    public void PlayMoonComet()
    {
        Play(_soundSetup.MoonComet);
    }

    /// <summary>
    /// 결산에서 블랙홀 사이즈 슬라이더 사운드 재생
    /// </summary>
    public void PlaySlider()
    {
        Play(_soundSetup.Slider);
    }

    /// <summary>
    /// 결산 완료되었을 때 사운드 재생
    /// </summary>
    public void PlayClosing()
    {
        Play(_soundSetup.Closing);
    }

    /// <summary>
    /// 노드 업그레이드 사운드 재생
    /// </summary>
    public void PlayNodeUpgrade()
    {
        Play(_soundSetup.NodeUpgrade);
    }

    /// <summary>
    /// 화면 전환 사운드 재생
    /// </summary>
    public void PlaySwitchingScreens()
    {
        Play(_soundSetup.SwitchingScreens);
    }

    /// <summary>
    /// 슈퍼노바 사운드 재생
    /// </summary>
    public void PlaySuperNova()
    {
        Play(_soundSetup.SuperNova);
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
                yield return new WaitForSecondsRealtime(clip.length + 1f); // BGM길이 + 1초 텀 만큼 대기
            }
            else yield return null; // null 칸이면 한 프레임 쉬고 다음 곡으로

            index = (index + 1) % _soundSetup.BgmList.Count;
        }
    }

    #endregion

    #region 사운드 조절

    /// <summary>
    /// BGM 크기 조절
    /// </summary>
    /// <param name="value"></param>
    public void SetBgmVolume(float value)
    {
        _bgmSource.volume = Mathf.Clamp01(value); // 값 설정
        PlayerPrefs.SetFloat(BgmKey, _bgmSource.volume); // 저장
    }

    /// <summary>
    /// SFX 크기 조절
    /// </summary>
    /// <param name="value"></param>
    public void SetSfxVolume(float value)
    {
        _bgmSource.volume = Mathf.Clamp01(value); // 값 설정
        PlayerPrefs.SetFloat(SfxKey, _sfxSource.volume); // 저장
    }

    #endregion
}
