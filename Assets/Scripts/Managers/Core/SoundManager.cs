using System.Collections.Generic;
using UnityEngine;

public class SoundManager
{
    // MP3 Player  > AudioSource
    // MP3 음원     > AudioClip
    // 관객         > AudioListener
    AudioSource[] _audioSources = new AudioSource[(int)Define.Sound.MaxCount];
    readonly Dictionary<string, AudioClip> _audioClips = new();
    readonly Dictionary<AudioSource, float> _effectAudioSources = new();
    const string OneShotRootName = "@OneShotSound";
    Transform _root;
    float _masterVolume = 1f;
    float _bgmVolume = 1f;
    float _sfxVolume = 1f;
    float _bgmVolumeScale = 1f;

    public void Init()
    {
        GameObject root = GameObject.Find("@Sound");
        if (root == null)
        {
            root = new GameObject { name = "@Sound" };
            Object.DontDestroyOnLoad(root);

            string[] soundNames = System.Enum.GetNames(typeof(Define.Sound));
            for (int i = 0; i < soundNames.Length - 1; i++)
            {
                GameObject go = new() { name = soundNames[i] };
                _audioSources[i] = go.AddComponent<AudioSource>();
                go.transform.parent = root.transform;
            }
            _audioSources[(int)Define.Sound.Bgm].loop = true;
        }

        _root = root.transform;
        ApplyUserSettings(Managers.Data.UserSettings);
    }

    public void ApplyUserSettings(Data.UserSettingsData settings)
    {
        _masterVolume = Mathf.Clamp01(settings.masterVolume);
        _bgmVolume = Mathf.Clamp01(settings.bgmVolume);
        _sfxVolume = Mathf.Clamp01(settings.sfxVolume);

        AudioSource bgmSource = _audioSources[(int)Define.Sound.Bgm];
        if (bgmSource != null)
            bgmSource.volume = GetEffectiveVolume(Define.Sound.Bgm, _bgmVolumeScale);

        foreach ((AudioSource audioSource, float volumeScale) in _effectAudioSources)
        {
            if (audioSource == null)
                continue;

            audioSource.volume = GetEffectiveVolume(Define.Sound.Effect, volumeScale);
        }
    }

    public void Clear()
    {
        foreach (AudioSource audioSource in _audioSources)
        {
            if (audioSource == null)
                continue;

            audioSource.clip = null;
            audioSource.Stop();
        }

        foreach (AudioSource audioSource in _effectAudioSources.Keys)
        {
            if (audioSource == null)
                continue;

            audioSource.Stop();
            Object.Destroy(audioSource.gameObject);
        }

        _effectAudioSources.Clear();
        _audioClips.Clear();
    }

    public void Play(string path, Define.Sound type = Define.Sound.Effect, float pitch = 1.0f)
    {
        Play(GetorAddAudioClip(path, type), type, pitch);
    }

    public void Play(string path, Define.Sound type, float pitch, float volumeScale)
    {
        Play(GetorAddAudioClip(path, type), type, pitch, volumeScale);
    }

    public void PlayEffect(string path, float pitch = 1.0f, float volumeScale = 1.0f)
    {
        Play(GetorAddAudioClip(path, Define.Sound.Effect), Define.Sound.Effect, pitch, volumeScale);
    }

    public void PlayOneShotPersistently(string path, float pitch = 1.0f)
    {
        ApplyUserSettings(Managers.Data.UserSettings);

        AudioClip audioClip = GetorAddAudioClip(path, Define.Sound.Effect);
        if (audioClip == null)
            return;

        GameObject go = new() { name = OneShotRootName };
        Object.DontDestroyOnLoad(go);

        AudioSource audioSource = go.AddComponent<AudioSource>();
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(audioClip, GetEffectiveVolume(Define.Sound.Effect));

        Object.Destroy(go, audioClip.length / Mathf.Max(0.01f, Mathf.Abs(pitch)) + 0.1f);
    }

    public AudioSource PlayEffectForDuration(string path, float duration, float pitch = 1.0f)
    {
        return PlayControlledEffect(path, pitch, false, Mathf.Max(0f, duration));
    }

    public AudioSource PlayControlledEffect(string path, float pitch = 1.0f, bool loop = false, float duration = 0f, float volumeScale = 1.0f)
    {
        ApplyUserSettings(Managers.Data.UserSettings);

        AudioClip audioClip = GetorAddAudioClip(path, Define.Sound.Effect);
        if (audioClip == null)
            return null;

        GameObject go = new() { name = $"@Effect_{audioClip.name}" };
        if (_root != null)
            go.transform.parent = _root;

        AudioSource audioSource = go.AddComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.pitch = pitch;
        audioSource.volume = GetEffectiveVolume(Define.Sound.Effect, volumeScale);
        audioSource.loop = loop;
        audioSource.Play();
        _effectAudioSources.Add(audioSource, volumeScale);

        if (!loop || duration > 0f)
        {
            float lifetime = duration > 0f
                ? duration
                : audioClip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
            Object.Destroy(go, lifetime);
        }

        return audioSource;
    }

    public void StopEffect(AudioSource audioSource)
    {
        if (audioSource == null)
            return;

        _effectAudioSources.Remove(audioSource);
        audioSource.Stop();
        Object.Destroy(audioSource.gameObject);
    }

    public void Play(AudioClip audioClip, Define.Sound type = Define.Sound.Effect, float pitch = 1.0f)
    {
        Play(audioClip, type, pitch, 1.0f);
    }

    public void Play(AudioClip audioClip, Define.Sound type = Define.Sound.Effect, float pitch = 1.0f, float volumeScale = 1.0f)
    {
        if (audioClip == null)
            return;

        ApplyUserSettings(Managers.Data.UserSettings);

        if (type == Define.Sound.Bgm)
        {
            AudioSource audioSource = _audioSources[(int)Define.Sound.Bgm];
            if (audioSource.isPlaying)
                audioSource.Stop();

            audioSource.pitch = pitch;
            _bgmVolumeScale = volumeScale;
            audioSource.volume = GetEffectiveVolume(Define.Sound.Bgm, volumeScale);
            audioSource.clip = audioClip;
            audioSource.Play();

        }
        else
        {
            AudioSource audioSource = _audioSources[(int)Define.Sound.Effect];
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(audioClip, GetEffectiveVolume(Define.Sound.Effect, volumeScale));
        }
    }

    float GetEffectiveVolume(Define.Sound type, float volumeScale = 1f)
    {
        float typeVolume = type == Define.Sound.Bgm ? _bgmVolume : _sfxVolume;
        return _masterVolume * typeVolume * volumeScale;
    }

    AudioClip GetorAddAudioClip(string path, Define.Sound type = Define.Sound.Effect)
    {
        if (!path.Contains("Sounds/"))
            path = $"Sounds/{path}";

        AudioClip audioClip;
        if (type == Define.Sound.Bgm)
        {
            audioClip = Managers.Resource.Load<AudioClip>(path);

        }
        else
        {
            if (!_audioClips.TryGetValue(path, out audioClip))
            {
                audioClip = Managers.Resource.Load<AudioClip>(path);
                _audioClips.Add(path, audioClip);
            }
        }
        if (audioClip == null)
            Debug.Log($"Audio Clip Missing - path: {path}");

        return audioClip;
    }
}
