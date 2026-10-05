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
        Setup,
        Home,
        StageSelect,
        Playing,
        Paused,
        Results,
    }

    private PlayerProfile _profile;
    private IWallet _wallet;
    private StageProgress _progress;
    private ClearReward? _lastReward;
    private int _selectPage;
    private const int StagesPerPage = 20;
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
        _progress = new StageProgress(_profile, _wallet, () => ProfileStore.Save(_profile));
        _stage = _progress.HighestUnlocked;
        _selectPage = (_stage - 1) / StagesPerPage;
        if (_wallet.RecoverInterruptedAttempt())
            _notice = "Your last attempt was cut short, so it cost a life.";
        if (_profile.NeedsSetup) _page = Page.Setup;
        SetHomeCamera();
    }

    private string _nicknameDraft = "";
    private int _setupAvatar;

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
        _lastReward = null;
        ulong attemptSeed = (ulong)System.DateTime.UtcNow.Ticks ^ (ulong)_attemptCounter;
        _view = GameView.Begin(StageLibrary.Load(stage), attemptSeed, _progress.TakeStartingLoadout(), _profile.Avatar);
        _view.Finished += OnFinished;
        _page = Page.Playing;
        _notice = null;
    }

    private void OnFinished(Game game)
    {
        bool cleared = game.Outcome == Outcome.Cleared;
        _wallet.EndAttempt(cleared);
        if (cleared) _lastReward = _progress.RecordClear(_stage, game.Stars, game.Bomber.Loadout);
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
            case Page.Setup: DrawSetup(); break;
            case Page.Home: DrawHome(); break;
            case Page.StageSelect: DrawStageSelect(); break;
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

    /// <summary>First launch: choose a nickname and a free starting avatar.</summary>
    private void DrawSetup()
    {
        float w = Screen.width, u = Ui.U;
        GUI.Label(new Rect(0, 4 * u, w, 12 * u), "Welcome to Bomb Arena!", Ui.Title);
        GUI.Label(new Rect(0, 17 * u, w, 7 * u), $"Choose a nickname (up to {PlayerProfile.MaxNicknameLength} characters)", Ui.Label);
        _nicknameDraft = GUI.TextField(new Rect(w * 0.3f, 25 * u, w * 0.4f, 10 * u), _nicknameDraft, PlayerProfile.MaxNicknameLength, Ui.Button);

        GUI.Label(new Rect(0, 38 * u, w, 7 * u), "Pick your look (free)", Ui.Label);
        DrawAvatarRow(46 * u, ref _setupAvatar);

        GUI.enabled = PlayerProfile.IsValidNickname(_nicknameDraft);
        if (GUI.Button(new Rect(w * 0.35f, 78 * u, w * 0.3f, 12 * u), "Start", Ui.Button))
        {
            _profile.CompleteSetup(_nicknameDraft, _setupAvatar);
            ProfileStore.Save(_profile);
            _page = Page.Home;
        }
        GUI.enabled = true;
    }

    /// <summary>A row of the 10 avatars; tapping one selects it.</summary>
    private static void DrawAvatarRow(float y, ref int selected)
    {
        float u = Ui.U, size = 14 * u, gap = 2 * u;
        float total = PlayerProfile.AvatarCount * size + (PlayerProfile.AvatarCount - 1) * gap;
        float x = (Screen.width - total) / 2;
        for (int i = 0; i < PlayerProfile.AvatarCount; i++)
        {
            var r = new Rect(x + i * (size + gap), y, size, size);
            var old = GUI.color;
            GUI.color = i == selected ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            if (GUI.Button(r, GUIContent.none, Ui.Button)) selected = i;
            GUI.DrawTexture(new Rect(r.x + size * 0.15f, r.y + size * 0.15f, size * 0.7f, size * 0.7f),
                PlaceholderSprites.BomberAvatar(i).texture, ScaleMode.ScaleToFit);
            GUI.color = old;
        }
    }

    private void DrawHome()
    {
        DrawWalletBar();
        float u = Ui.U;
        GUI.DrawTexture(new Rect(3 * u, 2 * u, 10 * u, 10 * u), PlaceholderSprites.BomberAvatar(_profile.Avatar).texture, ScaleMode.ScaleToFit);
        GUI.Label(new Rect(14 * u, 2 * u, 40 * u, 10 * u), _profile.Nickname, Ui.Small);
        var col = new Ui.Column(new Rect(Screen.width * 0.3f, Screen.height * 0.12f,
            Screen.width * 0.4f, Screen.height * 0.88f), 0f);
        GUI.Label(col.Next(14), "Bomb Arena", Ui.Title);
        if (GUI.Button(col.Next(11), "Stage mode", Ui.Button))
        {
            _selectPage = (_stage - 1) / StagesPerPage;
            _page = Page.StageSelect;
        }
        GUI.enabled = false;
        GUI.Button(col.Next(11), "Bluetooth (coming soon)", Ui.Button);
        GUI.Button(col.Next(11), "Shop (coming soon)", Ui.Button);
        GUI.Button(col.Next(11), "Settings (coming soon)", Ui.Button);
        GUI.enabled = true;
        if (_notice != null) GUI.Label(col.Next(10), _notice, Ui.Small);
    }

    private void DrawStageSelect()
    {
        DrawWalletBar();
        float w = Screen.width, h = Screen.height, u = Ui.U;
        GUI.Label(new Rect(0, 8 * u, w, 9 * u), "Choose a stage", Ui.Label);

        const int cols = 5, rows = 4;
        float gridW = w * 0.7f, cellW = gridW / cols, cellH = 15 * u, top = 18 * u, left = (w - gridW) / 2;
        int first = _selectPage * StagesPerPage + 1;
        for (int i = 0; i < StagesPerPage; i++)
        {
            int n = first + i;
            if (n > StageLibrary.Count) break;
            var r = new Rect(left + (i % cols) * cellW + u, top + (i / cols) * (cellH + u), cellW - 2 * u, cellH);
            bool open = _progress.IsUnlocked(n);
            int stars = _progress.BestStars(n);
            string label = n + System.Environment.NewLine + (open ? StarText(stars) : "locked");
            GUI.enabled = open;
            if (GUI.Button(r, label, Ui.Button)) StartAttempt(n);
            GUI.enabled = true;
        }

        float by = top + rows * (cellH + u) + 2 * u, bw = 22 * u;
        GUI.enabled = _selectPage > 0;
        if (GUI.Button(new Rect(left, by, bw, 10 * u), "< Prev", Ui.Button)) _selectPage--;
        GUI.enabled = (_selectPage + 1) * StagesPerPage < StageLibrary.Count;
        if (GUI.Button(new Rect(left + gridW - bw, by, bw, 10 * u), "Next >", Ui.Button)) _selectPage++;
        GUI.enabled = true;
        if (GUI.Button(new Rect((w - bw) / 2, by, bw, 10 * u), "Home", Ui.Button)) _page = Page.Home;
        GUI.Label(new Rect(0, by + 10 * u, w, 6 * u), $"Stages {first}-{Mathf.Min(first + StagesPerPage - 1, StageLibrary.Count)}", Ui.Small);
    }

    private static string StarText(int stars) => stars == 0 ? "- - -" : new string('*', stars) + new string('-', 3 - stars);

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
        var col = new Ui.Column(Ui.Panel(0.55f, 0.85f));
        if (game.Outcome == Outcome.Cleared)
        {
            GUI.Label(col.Next(10), "Stage clear!", Ui.Title);
            GUI.Label(col.Next(7), $"Stars {game.Stars}/3    Time {Ui.Clock(game.ClearedOnTick.GetValueOrDefault() / Units.TicksPerSecond)}", Ui.Label);
            if (_lastReward is ClearReward r)
                GUI.Label(col.Next(7), $"+{r.Total} jewels  ({r.ClearJewels} clear + {r.StarBonus} {(r.FirstThreeStars ? "first 3 stars" : "stars")})", Ui.Small);
        }
        else
        {
            GUI.Label(col.Next(10), game.FailReason == FailReason.TimeUp ? "Time up!" : "You died", Ui.Title);
            GUI.Label(col.Next(7), $"Lives left: {_wallet.Lives}", Ui.Label);
        }

        if (game.Outcome == Outcome.Cleared && _stage < StageLibrary.Count && GUI.Button(col.Next(10), "Next stage", Ui.Button))
        {
            CloseView();
            StartAttempt(_stage + 1);
            return;
        }
        if (GUI.Button(col.Next(10), "Play again", Ui.Button))
        {
            CloseView();
            StartAttempt(_stage);
            return;
        }
        if (GUI.Button(col.Next(10), "Stage select", Ui.Button))
        {
            CloseView();
            _page = Page.StageSelect;
            SetHomeCamera();
        }
    }
}
