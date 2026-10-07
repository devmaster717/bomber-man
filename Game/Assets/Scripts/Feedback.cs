using UnityEngine;

/// <summary>
/// Sound effects, background music and vibration, honouring the player's settings. The audio is in
/// Resources/Audio (see CREDITS.txt there): Handel's Water Music by the US Marine Band (public domain) for menus,
/// stages and battles, and CC0 effects from Kenney.
/// </summary>
public sealed class Feedback : MonoBehaviour
{
    public enum Track { Menu, Stage, Battle }

    public static Feedback Instance { get; private set; }

    public bool MusicOn { get => _musicOn; set { _musicOn = value; ApplyMusicVolume(); } }
    public bool SoundOn { get; set; } = true;
    public bool VibrationOn { get; set; } = true;

    private const float MusicVolume = 0.32f, CrossfadeSeconds = 1.2f;

    private bool _musicOn = true;
    private AudioSource _sfx;
    // Two music players, so one track can fade out while the next fades in.
    private AudioSource _musicNow, _musicOld;
    private float _fade = 1f;
    private Track? _track;
    private AudioClip _place, _boomNear, _boomFar, _crate, _pickup, _death, _clear, _failed, _click;

    public static Feedback Create(bool music, bool sound, bool vibration)
    {
        var f = new GameObject("Feedback").AddComponent<Feedback>();
        DontDestroyOnLoad(f.gameObject);
        f.SoundOn = sound;
        f.VibrationOn = vibration;
        f.MusicOn = music;
        Instance = f;
        return f;
    }

    private void Awake()
    {
        _sfx = gameObject.AddComponent<AudioSource>();
        _musicNow = MusicPlayer();
        _musicOld = MusicPlayer();
        _place = Clip("Sfx_BombPlace");
        _boomNear = Clip("Sfx_ExplosionNear");
        _boomFar = Clip("Sfx_ExplosionFar");
        _crate = Clip("Sfx_CrateBreak");
        _pickup = Clip("Sfx_PowerUp");
        _death = Clip("Sfx_Death");
        _clear = Clip("Sfx_StageClear");
        _failed = Clip("Sfx_StageFailed");
        _click = Clip("Sfx_Click");
        Music(Track.Menu);
    }

    private AudioSource MusicPlayer()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.loop = true;
        s.playOnAwake = false;
        s.volume = 0f;
        return s;
    }

    private static AudioClip Clip(string name)
    {
        var clip = Resources.Load<AudioClip>("Audio/" + name);
        if (clip == null) Debug.LogError("Sound missing: " + name);
        return clip;
    }

    /// <summary>Switches the background music, crossfading from the current track.</summary>
    public void Music(Track track)
    {
        if (_track == track) return;
        _track = track;
        var clip = Clip(track switch { Track.Stage => "Music_Stage", Track.Battle => "Music_Battle", _ => "Music_Menu" });
        (_musicNow, _musicOld) = (_musicOld, _musicNow);
        _musicNow.clip = clip;
        _musicNow.time = 0f;
        if (clip != null) _musicNow.Play();
        _fade = 0f;
    }

    private void Update()
    {
        if (_fade >= 1f) return;
        _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / CrossfadeSeconds);
        ApplyMusicVolume();
        if (_fade >= 1f) _musicOld.Stop();
    }

    private void ApplyMusicVolume()
    {
        if (_musicNow == null) return;
        float on = _musicOn ? MusicVolume : 0f;
        _musicNow.volume = on * _fade;
        _musicOld.volume = on * (1f - _fade);
    }

    /// <summary>A bomb was placed: yours with a tap, others' quieter.</summary>
    public void BombPlaced(bool mine)
    {
        Play(_place, mine ? 0.6f : 0.3f);
        if (mine) Vibrate(20, 60);
    }

    public void Explosion(bool nearby)
    {
        if (nearby)
        {
            Play(_boomNear, 0.9f);
            Vibrate(80, 200);
        }
        else Play(_boomFar, 0.45f);
    }

    public void CrateBroke() => Play(_crate, 0.55f);

    /// <summary>A bomber died: you, with a long buzz, or another player, quieter.</summary>
    public void Died(bool mine)
    {
        Play(_death, mine ? 0.9f : 0.5f);
        if (mine) Vibrate(300, 255);
    }

    public void PowerUp() => Play(_pickup, 0.8f);
    public void StageClear() => Play(_clear, 0.8f);
    public void StageFailed() => Play(_failed, 0.7f);

    /// <summary>A menu control was pressed: a soft click and a very light tap.</summary>
    public void Click() { Play(_click, 0.4f); Vibrate(8, 40); }

    private void Play(AudioClip clip, float volume)
    {
        if (SoundOn && clip != null) _sfx.PlayOneShot(clip, volume);
    }

    /// <summary>A pulse of <paramref name="ms"/> milliseconds at an amplitude of 1–255 (Android 8.0+ supports amplitude).</summary>
    private void Vibrate(long ms, int amplitude)
    {
        if (!VibrationOn) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            using var effect = new AndroidJavaClass("android.os.VibrationEffect");
            using var oneShot = effect.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude);
            vibrator.Call("vibrate", oneShot);
        }
        catch (System.Exception)
        {
            Handheld.Vibrate(); // also makes Unity add the VIBRATE permission
        }
#endif
    }
}
