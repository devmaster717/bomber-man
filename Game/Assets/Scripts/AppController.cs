using BombArena.Core;
using UnityEngine;

/// <summary>
/// The app's screens and flow (spec section 10): home, a running stage attempt with its pause menu, and the
/// results. Owns the player's profile and wallet; gameplay itself runs in <see cref="GameView"/>.
/// </summary>
public sealed class AppController : MonoBehaviour
{
    private enum Page
    {
        Home,
        Playing,
        Paused,
        Results,
    }

    private PlayerProfile _profile;
    private IWallet _wallet;
    private Page _page = Page.Home;
    private GameView _view;
    private int _stage = 1;
    private int _attemptCounter;
    private string _notice;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        _profile = ProfileStore.Load();
        _wallet = new LocalWallet(_profile, ProfileStore.Now, () => ProfileStore.Save(_profile));
        if (_wallet.RecoverInterruptedAttempt())
            _notice = "Your last attempt was cut short, so it cost a life.";
        SetHomeCamera();
    }

    private void Update()
    {
        if (_page == Page.Playing && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)))
            Pause();
    }

    // Leaving the app (a call, the home button) pauses the attempt.
    private void OnApplicationPause(bool paused)
    {
        if (paused && _page == Page.Playing) Pause();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused && _page == Page.Playing) Pause();
    }

    private void StartAttempt(int stage)
    {
        if (!_wallet.TryStartAttempt())
        {
            _notice = "No lives left. Wait for one to regenerate.";
            GoHome();
            return;
        }

        _stage = stage;
        _attemptCounter++;
        ulong attemptSeed = (ulong)System.DateTime.UtcNow.Ticks ^ (ulong)_attemptCounter;
        _view = GameView.Begin(StageLibrary.Load(stage), attemptSeed, default);
        _view.Finished += OnFinished;
        _page = Page.Playing;
        _notice = null;
    }

    private void OnFinished(Game game)
    {
        _wallet.EndAttempt(cleared: game.Outcome == Outcome.Cleared);
        _page = Page.Results;
    }

    private void Pause()
    {
        _view.Paused = true;
        _page = Page.Paused;
    }

    private void Resume()
    {
        _view.Paused = false;
        _page = Page.Playing;
    }

    /// <summary>Restart and Quit end the running attempt as failed (one life).</summary>
    private void AbandonAttempt()
    {
        if (_view != null && _view.Game.Outcome == Outcome.Playing) _wallet.EndAttempt(cleared: false);
        CloseView();
    }

    private void CloseView()
    {
        if (_view != null) Destroy(_view.gameObject);
        _view = null;
    }

    private void GoHome()
    {
        CloseView();
        _page = Page.Home;
        SetHomeCamera();
    }

    private static void SetHomeCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.16f, 0.22f);
        cam.transform.position = new Vector3(0, 0, -10f);
    }

    private void OnGUI()
    {
        switch (_page)
        {
            case Page.Home: DrawHome(); break;
            case Page.Playing: DrawPauseButton(); break;
            case Page.Paused: DrawPauseMenu(); break;
            case Page.Results: DrawResults(); break;
        }
    }

    private void DrawWalletBar()
    {
        _wallet.Refresh();
        string lives = $"Lives {_wallet.Lives}";
        if (_wallet.SecondsToNextLife is long s) lives += $"  (next in {Ui.Clock(s)})";
        GUI.Label(new Rect(0, Ui.U, Screen.width, 7 * Ui.U), $"Jewels {_wallet.Jewels}      {lives}", Ui.Label);
    }

    private void DrawHome()
    {
        DrawWalletBar();
        var col = new Ui.Column(new Rect(Screen.width * 0.3f, Screen.height * 0.12f,
            Screen.width * 0.4f, Screen.height * 0.88f), 0f);
        GUI.Label(col.Next(14), "Bomb Arena", Ui.Title);
        if (GUI.Button(col.Next(11), $"Stage mode (stage {_stage})", Ui.Button)) StartAttempt(_stage);
        GUI.enabled = false;
        GUI.Button(col.Next(11), "Bluetooth (coming soon)", Ui.Button);
        GUI.Button(col.Next(11), "Shop (coming soon)", Ui.Button);
        GUI.Button(col.Next(11), "Settings (coming soon)", Ui.Button);
        GUI.enabled = true;
        if (_notice != null) GUI.Label(col.Next(10), _notice, Ui.Small);
    }

    private void DrawPauseButton()
    {
        float s = 10 * Ui.U;
        if (GUI.Button(new Rect(Screen.width - s - 2 * Ui.U, 2 * Ui.U, s, s), "II", Ui.Button)) Pause();
    }

    private void DrawPauseMenu()
    {
        var col = new Ui.Column(Ui.Panel(0.4f, 0.75f));
        GUI.Label(col.Next(10), "Paused", Ui.Title);
        if (GUI.Button(col.Next(11), "Resume", Ui.Button)) Resume();
        if (GUI.Button(col.Next(11), "Restart (costs a life)", Ui.Button))
        {
            AbandonAttempt();
            StartAttempt(_stage);
        }
        GUI.enabled = false;
        GUI.Button(col.Next(11), "Settings (coming soon)", Ui.Button);
        GUI.enabled = true;
        if (GUI.Button(col.Next(11), "Quit (costs a life)", Ui.Button))
        {
            AbandonAttempt();
            GoHome();
        }
    }

    private void DrawResults()
    {
        var game = _view.Game;
        var col = new Ui.Column(Ui.Panel(0.5f, 0.75f));
        if (game.Outcome == Outcome.Cleared)
        {
            GUI.Label(col.Next(10), "Stage clear!", Ui.Title);
            GUI.Label(col.Next(7), $"Stars {game.Stars}/3    Time {Ui.Clock(game.ClearedOnTick.GetValueOrDefault() / Units.TicksPerSecond)}", Ui.Label);
        }
        else
        {
            GUI.Label(col.Next(10), game.FailReason == FailReason.TimeUp ? "Time up!" : "You died", Ui.Title);
            GUI.Label(col.Next(7), $"Lives left: {_wallet.Lives}", Ui.Label);
        }

        if (GUI.Button(col.Next(11), "Play again", Ui.Button))
        {
            CloseView();
            StartAttempt(_stage);
        }
        if (GUI.Button(col.Next(11), "Home", Ui.Button)) GoHome();
    }
}
