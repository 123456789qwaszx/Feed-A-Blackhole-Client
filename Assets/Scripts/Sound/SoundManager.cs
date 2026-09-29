using UnityEngine;

internal sealed class SoundManager
{
    private readonly AudioSource _audioSource;

    public SoundManager(AudioSource source)
    {
        _audioSource = source;
    }

    /// <summary>
    /// 사운드 재생
    /// </summary>
    /// <param name="clip">재생할 사운드 파일</param>
    public void Play(AudioClip clip)
    {
        if (clip != null) _audioSource.PlayOneShot(clip);
    }
}
