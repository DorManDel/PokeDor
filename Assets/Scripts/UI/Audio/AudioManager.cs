using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    //Put this on a GameObject(AudioManager) - in Panel
    //and assign the bgmSource and sfxSource references in the Inspector.

    //Singleton AudioManager - Avoid Duplicating!
    public static AudioManager Instance { get; private set; }


    private bool _isMuted = false;
    public event Action OnMuteChanged;  // exposed to fix Singleton Destroy breakMuteButton
    public bool IsMuted => _isMuted;    // Expose for label Mute and Mute funcFix

    //public TextMeshProUGUI muteButtonLabel; // Assign this in Inspector
    public List<TextMeshProUGUI> muteButtonLabels;  // <- plural now - to work with multiple Btns

    public float Music01 => musicVolume;            // 0..1
    public void SetMusic01(float v01) => SetMusicVolume(v01 * 100f);
    public void SetSfx01(float v01) { SetSFXVolume(v01); }  // calls existing method

    [Header("Audio Sources")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;

    [Header("Volume")]
    [Range(0, 1)] public float musicVolume  = 1f;       //set default to 100%
    [Range(0, 1)] public float sfxVolume    = 1f;       //set default to 100%

    private void Awake()
    {
        // Singletone Safety
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        transform.SetParent(null);          // force to scene root
        DontDestroyOnLoad(gameObject);
        
        //make sure the GameObj is at SceneRoot -> then persists: (DontDestroyOnLoad only works on Rooted Objs)
        //transform.SetParent(null);      // <-- pulls out of Canvas so it becomes a root
        //if (transform.parent != null) transform.SetParent(null);  // Disabled to make Mute dont Destroy
        //DontDestroyOnLoad(gameObject);

        //try { DontDestroyOnLoad(gameObject); } catch { /* must be root GO */ }

        //ADDON :: Singleton Fix
        //var m = PlayerPrefs.GetInt("am_mute", 0) == 1;
        //SetMute(m);

        // Load mute state from prefs
        _isMuted = PlayerPrefs.GetInt("am_mute", 0) == 1;
        ApplyMute();        // <- helper (see below)

        ApplyVolumes();                 // the exsisting InitCall
        
        //PlayerPrefs Keep the Mute Working Solid:
        //SetMusicVolume(GamePrefs.Music);
        //SetSfxVolume(GamePrefs.Sfx);
        //DontDestroyOnLoad(gameObject); // make sure AudioManager is at scene root
        
        //float musicVol = PlayerPrefs.GetFloat("MusicVolume", 1f);
        //float sfxVol = PlayerPrefs.GetFloat("SfxVolume", 1f);

        //bgmSource.volume = musicVol;
        //sfxSource.volume = sfxVol;

    }
    // avoid ambiguity
    //public void ToggleMute() => SetMute(!bgmSource.mute);
    public void SetMute(bool on)
    {
        if (bgmSource) bgmSource.mute = on;
        if (sfxSource) sfxSource.mute = on;
        PlayerPrefs.SetInt("am_mute", on ? 1 : 0);
    }
    private void ApplyMute()
    {
        if (bgmSource) bgmSource.mute = _isMuted;
        if (sfxSource) sfxSource.mute = _isMuted;
        AudioListener.volume = _isMuted ? 0f : 1f;
    }

    private void Start()
    {
        // Save Prefs MusicVolume ::
        float saved = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        SetMusicVolume(saved);
        // Save Prefs For SFXMusic
        float saved1 = PlayerPrefs.GetFloat("SfxVolume", 0.5f);
        SetSFXVolume(saved1);   //  fix SFX Issues?

        LoadVolumes();
        ApplyVolumes();
        AudioListener.volume = _isMuted ? 0f : 1f;

        //Fix for Mute when start not init Music&&SFX
        int muteState = PlayerPrefs.GetInt("Muted", 0); // default 0
        bool isMuted = muteState == 1;

        AudioListener.pause = isMuted;
        Debug.Log("AudioManager: Mute state restored = " + isMuted);

    }
    // fixes the Ref get Destroyed an keep it alive - Mute button will work IG.
    private void OnEnable()
    {
        ApplyVolumes();  // ensures sliders + SFX volumes match current values
    }


    public void SetMusicVolume(float volumePercent)
    {
        Debug.Log($"SetMusicVolume CALLED with {volumePercent}");

        // Convert from 0–100% to 0–1.0 for AudioSource
        musicVolume = Mathf.Clamp01(volumePercent / 100f);

        if (bgmSource != null)
        {
            bgmSource.volume = musicVolume;
            Debug.Log($"Set bgmSource.volume to {musicVolume}");
            PlayerPrefs.SetFloat("MusicVolume", musicVolume);
            PlayerPrefs.Save();
        }
        else
        {
            Debug.LogWarning("bgmSource is NULL! Check the AudioManager in the Inspector.");
        }
    }


    public void SetSFXVolume(float volume)
    {
        sfxVolume = volume;
        if (sfxSource != null)
            sfxSource.volume = sfxVolume;
        PlayerPrefs.SetFloat("SfxVolume", volume);
        PlayerPrefs.Save();
    }

    private void ApplyVolumes()
    {
        SetMusicVolume(musicVolume);
        SetSFXVolume(sfxVolume);
    }
    //TestPurposes
    public void PlaySFX(AudioClip clip)
    {
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    public void ToggleMute()
    {
        SetMute(!_isMuted);         // Fix the MuteLabel Destroy and breakMuteButton
        _isMuted = !_isMuted;

        bgmSource.mute = _isMuted;
        sfxSource.mute = _isMuted;
        
        OnMuteChanged?.Invoke();    // for Singleton Label Safety (getting Destroyed)
        PruneDeadLabels();          // to fix labels of mute button that destroyed between scenes
        // Update all button labels
        foreach (TextMeshProUGUI label in muteButtonLabels)
        {
            if (label != null)
                label.text = _isMuted ? "Unmute" : "Mute";
                // 🔊 Unmute / 🔇 Mute
        }
    }

    //new
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || bgmSource == null) return;

        if (bgmSource.clip == clip && bgmSource.isPlaying)
            return; // already playing this track

        bgmSource.Stop();
        bgmSource.clip = clip;
        bgmSource.volume = musicVolume;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    internal void StopMusic()
    {
        throw new NotImplementedException();
    }

    //re-register label and button fix:
    void PruneDeadLabels()
    {
        if (muteButtonLabels == null) return;
        for (int i = muteButtonLabels.Count - 1; i >= 0; i--)
            if (!muteButtonLabels[i]) muteButtonLabels.RemoveAt(i);
    }

    //Addon to Fix BGM and SFX
    public void PlayBgm(string key, bool loop = true)
    {
        if (!bgmSource) return;
        var clip = Resources.Load<AudioClip>($"BGM/{key}");
        if (!clip) { Debug.LogWarning($"BGM not found: {key}"); return; }
        bgmSource.loop = loop;
        if (bgmSource.clip != clip) bgmSource.clip = clip;
        bgmSource.Play();
    }

    public void StopBgm() { if (bgmSource) bgmSource.Stop(); }

    public void PlaySfx(string key)
    {
        if (!sfxSource) return;   // guard against destroyed source
        var clip = Resources.Load<AudioClip>($"SFX/{key}");
        if (clip) sfxSource.PlayOneShot(clip, sfxVolume);
    }


    // PLayerPrefs for remembering the Mute and fixing it:
    public static class GamePrefs
    {
        const string KEY_MUSIC = "music_vol";
        const string KEY_SFX = "sfx_vol";

        public static float Music { get => PlayerPrefs.GetFloat(KEY_MUSIC, 0.6f); set { PlayerPrefs.SetFloat(KEY_MUSIC, value); PlayerPrefs.Save(); } }
        public static float Sfx { get => PlayerPrefs.GetFloat(KEY_SFX, 0.8f); set { PlayerPrefs.SetFloat(KEY_SFX, value); PlayerPrefs.Save(); } }
    }

    public void SaveVolumes()
    {
        PlayerPrefs.SetFloat("BGM_VOLUME", bgmSource.volume);
        PlayerPrefs.SetFloat("SFX_VOLUME", sfxSource.volume);
        PlayerPrefs.Save();
    }

    public void LoadVolumes()
    {
        float bgm = PlayerPrefs.GetFloat("BGM_VOLUME", 1f); // default full
        float sfx = PlayerPrefs.GetFloat("SFX_VOLUME", 1f);

        bgmSource.volume = bgm;
        sfxSource.volume = sfx;
    }

}
