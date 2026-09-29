using UnityEngine;

internal sealed class SoundManager
{
    private readonly AudioSource _audioSource;
    private readonly UISoundSetup _soundSetup;

    public SoundManager(AudioSource source, UISoundSetup setup)
    {
        _audioSource = source;
        _soundSetup = setup;
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
