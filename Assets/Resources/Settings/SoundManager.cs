using UnityEngine;
using System.Collections.Generic;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("Music Clips")]
    public List<AudioClip> musicClips;

    [Header("SFX Clips")]
    public List<AudioClip> sfxClips;

    private Dictionary<string, AudioClip> musicDict;
    private Dictionary<string, AudioClip> sfxDict;

    // Tat ca volume la 0..1
    float masterVolume;
    float musicVolume;
    float sfxVolume;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitDictionaries();
            LoadVolume();
            ApplyVolume();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitDictionaries()
    {
        musicDict = new Dictionary<string, AudioClip>();
        sfxDict = new Dictionary<string, AudioClip>();

        foreach (AudioClip clip in musicClips)
        {
            if (clip != null && !musicDict.ContainsKey(clip.name))
                musicDict.Add(clip.name, clip);
        }

        foreach (AudioClip clip in sfxClips)
        {
            if (clip != null && !sfxDict.ContainsKey(clip.name))
                sfxDict.Add(clip.name, clip);
        }
    }

    void LoadVolume()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.5f);
    }

    void ApplyVolume()
    {
        // FIX: master chi ap 1 lan qua AudioListener.
        // Truoc day con nhan master vao music/sfx nen bi ap 2 lan.
        AudioListener.volume = masterVolume;

        musicSource.volume = musicVolume;
        sfxSource.volume = sfxVolume;
    }

    // ================= MUSIC =================

    public void PlayMusic(string name, bool loop = true)
    {
        if (!musicDict.ContainsKey(name))
        {
            Debug.LogWarning("Music not found: " + name);
            return;
        }

        // FIX: truoc day chi so sanh musicSource.clip == clip can phat,
        // nen neu clip DUNG nhung dang o trang thai DUNG PHAT (VD vua bi
        // StopMusic() goi tu scene khac - Stop() KHONG xoa .clip, chi
        // dung am thanh), ham nay tuong nham la "dang phat roi" roi
        // return som, khong goi lai Play() -> quay lai scene co nhac
        // nay se bi IM LANG vinh vien. Them dieu kien isPlaying de CHI
        // bo qua khi clip dung VA dang thuc su phat, con da dung thi
        // van phai Play() lai.
        if (musicSource.clip == musicDict[name] && musicSource.isPlaying)
            return;

        musicSource.clip = musicDict[name];
        musicSource.loop = loop;
        musicSource.Play();
    }

    public void StopMusic()
    {
        musicSource.Stop();
    }

    // ================= SFX =================

    public void PlaySFX(string name)
    {
        if (!sfxDict.ContainsKey(name))
        {
            Debug.LogWarning("SFX not found: " + name);
            return;
        }

        sfxSource.PlayOneShot(sfxDict[name]);
    }

    public void StopAllSFX()
    {
        sfxSource.Stop();
    }

    // ================= VOLUME =================

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        PlayerPrefs.Save();

        ApplyVolume();
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        PlayerPrefs.Save();

        ApplyVolume();
    }

    public void SetSFXVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
        PlayerPrefs.Save();

        ApplyVolume();
    }

    // ================= UTIL =================

    public float GetMasterVolume() => masterVolume;
    public float GetMusicVolume() => musicVolume;
    public float GetSFXVolume() => sfxVolume;
}