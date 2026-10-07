using UnityEngine;

/// <summary>
/// Sound effects, background music and vibration, honouring the player's settings. Sounds are generated in
/// code as placeholders (no downloaded assets), so they can be swapped for real audio later.
/// </summary>
public sealed class Feedback : MonoBehaviour
{
    public static Feedback Instance { get; private set; }

    public bool MusicOn { get => _musicOn; set { _musicOn = value; if (_music != null) _music.mute = !value; } }
    public bool SoundOn { get; set; } = true;
    public bool VibrationOn { get; set; } = true;

    private bool _musicOn = true;
    private AudioSource _sfx, _music;
    private AudioClip _place, _boom, _death, _pickup, _clear, _tune, _click;

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
        _music = gameObject.AddComponent<AudioSource>();
        _place = Tone("place", 0.06f, t => Square(t, 660f) * Fade(t, 0.06f));
        _boom = Tone("boom", 0.45f, t => Noise() * Fade(t, 0.45f) * 0.9f);
        _death = Tone("death", 0.6f, t => Square(t, Mathf.Lerp(440f, 110f, t / 0.6f)) * Fade(t, 0.6f));
        _pickup = Tone("pickup", 0.25f, t => Square(t, t < 0.08f ? 523f : t < 0.16f ? 659f : 784f) * 0.7f);
        _clear = Tone("clear", 0.8f, t => Square(t, new[] { 523f, 659f, 784f, 1047f }[Mathf.Min(3, (int)(t / 0.2f))]) * 0.6f);
        _tune = Tone("tune", 6.4f, Tune);
        _click = Tone("click", 0.03f, t => Square(t, 1400f) * Fade(t, 0.03f));
        _music.clip = _tune;
        _music.loop = true;
        _music.volume = 0.18f;
        _music.mute = !_musicOn;
        _music.Play();
    }

    public void BombPlaced() { Play(_place, 0.5f); Vibrate(20, 60); }
    public void Explosion(bool nearby) { Play(_boom, 0.8f); if (nearby) Vibrate(80, 200); }
    public void Died() { Play(_death, 0.8f); Vibrate(300, 255); }
    public void PowerUp() => Play(_pickup, 0.7f);
    public void StageClear() => Play(_clear, 0.7f);
    /// <summary>A menu control was pressed: a soft click and a very light tap.</summary>
    public void Click() { Play(_click, 0.35f); Vibrate(8, 40); }

    private void Play(AudioClip clip, float volume)
    {
        if (SoundOn) _sfx.PlayOneShot(clip, volume);
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

    // --- tiny synthesiser for placeholder sounds ---

    private const int Rate = 22050;
    private static readonly System.Random Rng = new System.Random(7);

    private static AudioClip Tone(string name, float seconds, System.Func<float, float> wave)
    {
        int n = (int)(seconds * Rate);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(wave(i / (float)Rate), -1f, 1f) * 0.5f;
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float Square(float t, float hz) => Mathf.Sin(2f * Mathf.PI * hz * t) >= 0 ? 0.5f : -0.5f;
    private static float Noise() => (float)Rng.NextDouble() * 2f - 1f;
    private static float Fade(float t, float length) => 1f - Mathf.Clamp01(t / length);

    // A short looping melody in C major, eight notes per bar.
    private static readonly float[] Melody = { 262, 330, 392, 330, 349, 440, 392, 330, 294, 349, 440, 349, 330, 392, 330, 262 };

    private static float Tune(float t)
    {
        const float step = 0.4f;
        int i = (int)(t / step) % Melody.Length;
        float local = t % step;
        float env = local < 0.3f ? 1f : Mathf.Clamp01((step - local) / 0.1f);
        float bass = Mathf.Sin(2f * Mathf.PI * Melody[(i / 4 * 4) % Melody.Length] / 2f * t) * 0.3f;
        return (Square(t, Melody[i]) * 0.5f + bass) * env;
    }
}
