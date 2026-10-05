using BombArena.Core;
using UnityEngine;

/// <summary>
/// The app's screens and flow (spec section 10): first launch, home, stage select, a running stage attempt with
/// its pause menu, results and settings. Owns the player's profile and wallet; gameplay runs in <see cref="GameView"/>.
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
        Settings,
    }

    private const int StagesPerPage = 20;

    private PlayerProfile _profile;
    private IWallet _wallet;
    private StageProgress _progress;
    private Feedback _feedback;
    private ClearReward? _lastReward;
    private Page _page = Page.Home;
    private Page _settingsReturn = Page.Home;
    private GameView _view;
    private int _stage = 1;
    private int _selectPage;
    private int _attemptCounter;
    private string _notice;
    private string _nicknameDraft = "";
    private int _setupAvatar;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        _profile = ProfileStore.Load();
        _wallet = new LocalWallet(_profile, ProfileStore.Now, Save);
        _progress = new StageProgress(_profile, _wallet, Save);
        _feedback = Feedback.Create(_profile.MusicOn, _profile.SoundOn, _profile.VibrationOn);
        _stage = _progress.HighestUnlocked;
        _selectPage = (_stage - 1) / StagesPerPage;
        if (_wallet.RecoverInterruptedAttempt()) _notice = Text.AttemptCutShort;
        if (_profile.NeedsSetup) _page = Page.Setup;
        SetHomeCamera();
    }

    private void Save() => ProfileStore.Save(_profile);

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

    // ---- flow ----

    private void StartAttempt(int stage)
    {
        if (!_wallet.TryStartAttempt())
        {
            _notice = Text.NoLives;
            GoHome();
            return;
        }

        _stage = stage;
        _attemptCounter++;
        _lastReward = null;
        ulong attemptSeed = (ulong)System.DateTime.UtcNow.Ticks ^ (ulong)_attemptCounter;
        _view = GameView.Begin(StageLibrary.Load(stage), attemptSeed, _progress.TakeStartingLoadout(), _profile);
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

    private void OpenSettings(Page returnTo)
    {
        _settingsReturn = returnTo;
        _nicknameDraft = _profile.Nickname;
        _page = Page.Settings;
    }

    private static void SetHomeCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.16f, 0.22f);
        cam.transform.position = new Vector3(0, 0, -10f);
    }

    // ---- screens ----

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
            case Page.Settings: DrawSettings(); break;
        }
    }

    private void DrawWalletBar()
    {
        _wallet.Refresh();
        string lives = Text.Lives(_wallet.Lives);
        if (_wallet.SecondsToNextLife is long s) lives += "  " + Text.NextLifeIn(Ui.Clock(s));
        GUI.Label(new Rect(0, Ui.U, Screen.width, 7 * Ui.U), Text.Jewels(_wallet.Jewels) + "      " + lives, Ui.Label);
    }

    /// <summary>First launch: choose a nickname and a free starting avatar.</summary>
    private void DrawSetup()
    {
        float w = Screen.width, u = Ui.U;
        GUI.Label(new Rect(0, 4 * u, w, 12 * u), Text.Welcome, Ui.Title);
        GUI.Label(new Rect(0, 17 * u, w, 7 * u), Text.ChooseNickname(PlayerProfile.MaxNicknameLength), Ui.Label);
        _nicknameDraft = GUI.TextField(new Rect(w * 0.3f, 25 * u, w * 0.4f, 10 * u), _nicknameDraft, PlayerProfile.MaxNicknameLength, Ui.Button);

        GUI.Label(new Rect(0, 38 * u, w, 7 * u), Text.PickLookFree, Ui.Label);
        DrawAvatarRow(46 * u, ref _setupAvatar, i => true);

        GUI.enabled = PlayerProfile.IsValidNickname(_nicknameDraft);
        if (GUI.Button(new Rect(w * 0.35f, 78 * u, w * 0.3f, 12 * u), Text.Start, Ui.Button))
        {
            _profile.CompleteSetup(_nicknameDraft, _setupAvatar);
            Save();
            _page = Page.Home;
        }
        GUI.enabled = true;
    }

    /// <summary>A row of the 10 avatars; tapping an available one selects it.</summary>
    private static void DrawAvatarRow(float y, ref int selected, System.Func<int, bool> available)
    {
        float u = Ui.U, size = 14 * u, gap = 2 * u;
        float total = PlayerProfile.AvatarCount * size + (PlayerProfile.AvatarCount - 1) * gap;
        float x = (Screen.width - total) / 2;
        for (int i = 0; i < PlayerProfile.AvatarCount; i++)
        {
            var r = new Rect(x + i * (size + gap), y, size, size);
            var old = GUI.color;
            GUI.enabled = available(i);
            GUI.color = i == selected ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            if (GUI.Button(r, GUIContent.none, Ui.Button)) selected = i;
            GUI.DrawTexture(new Rect(r.x + size * 0.15f, r.y + size * 0.15f, size * 0.7f, size * 0.7f),
                PlaceholderSprites.BomberAvatar(i).texture, ScaleMode.ScaleToFit);
            GUI.color = old;
            GUI.enabled = true;
        }
    }

    private void DrawHome()
    {
        DrawWalletBar();
        float u = Ui.U;
        GUI.DrawTexture(new Rect(3 * u, 2 * u, 10 * u, 10 * u), PlaceholderSprites.BomberAvatar(_profile.Avatar).texture, ScaleMode.ScaleToFit);
        GUI.Label(new Rect(14 * u, 2 * u, 40 * u, 10 * u), _profile.Nickname, Ui.Small);
        var col = new Ui.Column(new Rect(Screen.width * 0.3f, Screen.height * 0.12f, Screen.width * 0.4f, Screen.height * 0.88f), 0f);
        GUI.Label(col.Next(14), Text.GameTitle, Ui.Title);
        if (GUI.Button(col.Next(11), Text.StageMode, Ui.Button))
        {
            _selectPage = (_stage - 1) / StagesPerPage;
            _page = Page.StageSelect;
        }
        GUI.enabled = false;
        GUI.Button(col.Next(11), Text.Bluetooth + Text.ComingSoon, Ui.Button);
        GUI.Button(col.Next(11), Text.Shop + Text.ComingSoon, Ui.Button);
        GUI.enabled = true;
        if (GUI.Button(col.Next(11), Text.Settings, Ui.Button)) OpenSettings(Page.Home);
        if (_notice != null) GUI.Label(col.Next(10), _notice, Ui.Small);
    }

    private void DrawStageSelect()
    {
        DrawWalletBar();
        float w = Screen.width, u = Ui.U;
        GUI.Label(new Rect(0, 8 * u, w, 9 * u), Text.ChooseStage, Ui.Label);

        const int cols = 5, rows = 4;
        float gridW = w * 0.7f, cellW = gridW / cols, cellH = 15 * u, top = 18 * u, left = (w - gridW) / 2;
        int first = _selectPage * StagesPerPage + 1;
        for (int i = 0; i < StagesPerPage; i++)
        {
            int n = first + i;
            if (n > StageLibrary.Count) break;
            var r = new Rect(left + (i % cols) * cellW + u, top + (i / cols) * (cellH + u), cellW - 2 * u, cellH);
            bool open = _progress.IsUnlocked(n);
            string label = n + System.Environment.NewLine + (open ? StarText(_progress.BestStars(n)) : Text.Locked);
            GUI.enabled = open;
            if (GUI.Button(r, label, Ui.Button)) StartAttempt(n);
            GUI.enabled = true;
        }

        float by = top + rows * (cellH + u) + 2 * u, bw = 22 * u;
        GUI.enabled = _selectPage > 0;
        if (GUI.Button(new Rect(left, by, bw, 10 * u), Text.Prev, Ui.Button)) _selectPage--;
        GUI.enabled = (_selectPage + 1) * StagesPerPage < StageLibrary.Count;
        if (GUI.Button(new Rect(left + gridW - bw, by, bw, 10 * u), Text.Next, Ui.Button)) _selectPage++;
        GUI.enabled = true;
        if (GUI.Button(new Rect((w - bw) / 2, by, bw, 10 * u), Text.Home, Ui.Button)) _page = Page.Home;
        GUI.Label(new Rect(0, by + 10 * u, w, 6 * u), Text.StageRange(first, Mathf.Min(first + StagesPerPage - 1, StageLibrary.Count)), Ui.Small);
    }

    private static string StarText(int stars) => stars == 0 ? "- - -" : new string('*', stars) + new string('-', 3 - stars);

    private void DrawPauseButton()
    {
        float s = 10 * Ui.U;
        if (GUI.Button(new Rect(Screen.width - s - 2 * Ui.U, 2 * Ui.U, s, s), Text.PauseButton, Ui.Button)) Pause();
    }

    private void DrawPauseMenu()
    {
        var col = new Ui.Column(Ui.Panel(0.4f, 0.75f));
        GUI.Label(col.Next(10), Text.Paused, Ui.Title);
        if (GUI.Button(col.Next(11), Text.Resume, Ui.Button)) Resume();
        if (GUI.Button(col.Next(11), Text.Restart, Ui.Button))
        {
            AbandonAttempt();
            StartAttempt(_stage);
            return;
        }
        if (GUI.Button(col.Next(11), Text.Settings, Ui.Button)) OpenSettings(Page.Paused);
        if (GUI.Button(col.Next(11), Text.Quit, Ui.Button))
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
            GUI.Label(col.Next(10), Text.StageClear, Ui.Title);
            GUI.Label(col.Next(7), Text.StarsAndTime(game.Stars, Ui.Clock(game.ClearedOnTick.GetValueOrDefault() / Units.TicksPerSecond)), Ui.Label);
            if (_lastReward is ClearReward r)
                GUI.Label(col.Next(7), Text.JewelsEarned(r.Total, r.ClearJewels, r.StarBonus, r.FirstThreeStars), Ui.Small);
        }
        else
        {
            GUI.Label(col.Next(10), game.FailReason == FailReason.TimeUp ? Text.TimeUp : Text.YouDied, Ui.Title);
            GUI.Label(col.Next(7), Text.LivesLeft(_wallet.Lives), Ui.Label);
        }

        if (game.Outcome == Outcome.Cleared && _stage < StageLibrary.Count && GUI.Button(col.Next(10), Text.NextStage, Ui.Button))
        {
            CloseView();
            StartAttempt(_stage + 1);
            return;
        }
        if (GUI.Button(col.Next(10), Text.PlayAgain, Ui.Button))
        {
            CloseView();
            StartAttempt(_stage);
            return;
        }
        if (GUI.Button(col.Next(10), Text.StageSelect, Ui.Button))
        {
            CloseView();
            _page = Page.StageSelect;
            SetHomeCamera();
        }
    }

    private void DrawSettings()
    {
        if (_settingsReturn == Page.Paused) Ui.Panel(0.95f, 0.95f);
        float w = Screen.width, u = Ui.U;
        GUI.Label(new Rect(0, 3 * u, w, 10 * u), Text.Settings, Ui.Title);

        // Left column: sound, vibration and controls (rows of 8, 1 apart).
        float lx = w * 0.04f, lw = w * 0.44f;
        Rect Row(float yU, float hU = 8) => new Rect(lx, yU * u, lw, hU * u);
        _profile.MusicOn = Toggle(Row(13), Text.Music, _profile.MusicOn);
        _profile.SoundOn = Toggle(Row(22), Text.SoundEffects, _profile.SoundOn);
        _profile.VibrationOn = Toggle(Row(31), Text.Vibration, _profile.VibrationOn);
        var row = Row(40);
        GUI.Label(new Rect(row.x, row.y, row.width * 0.4f, row.height), Text.Controls, Ui.Label);
        // The chosen layout is bright, the other dimmed.
        var oldColour = GUI.color;
        GUI.color = _profile.UseJoystick ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
        if (GUI.Button(new Rect(row.x + row.width * 0.4f, row.y, row.width * 0.3f, row.height), Text.DPad, Ui.Button)) _profile.UseJoystick = false;
        GUI.color = _profile.UseJoystick ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        if (GUI.Button(new Rect(row.x + row.width * 0.7f, row.y, row.width * 0.3f, row.height), Text.Joystick, Ui.Button)) _profile.UseJoystick = true;
        GUI.color = oldColour;
        GUI.Label(Row(49, 5), Text.ButtonSize(_profile.ButtonScalePercent), Ui.Small);
        _profile.ButtonScalePercent = Mathf.RoundToInt(GUI.HorizontalSlider(Row(55, 4), _profile.ButtonScalePercent, 75, 150) / 5f) * 5;
        _profile.LeftHanded = Toggle(Row(60), Text.LeftHanded, _profile.LeftHanded);

        // Right column: nickname.
        float rx = w * 0.52f, rw = w * 0.44f;
        GUI.Label(new Rect(rx, 13 * u, rw, 7 * u), Text.Nickname, Ui.Label);
        _nicknameDraft = GUI.TextField(new Rect(rx, 22 * u, rw, 10 * u), _nicknameDraft, PlayerProfile.MaxNicknameLength, Ui.Button);
        if (PlayerProfile.IsValidNickname(_nicknameDraft)) _profile.Nickname = PlayerProfile.CleanNickname(_nicknameDraft);

        // Bottom: avatars the player owns can be chosen (more come from the shop).
        GUI.Label(new Rect(0, 69 * u, w, 5 * u), Text.Avatar, Ui.Small);
        int avatar = _profile.Avatar;
        DrawAvatarRow(74 * u, ref avatar, i => _profile.OwnedAvatars.Contains(i));
        _profile.Avatar = avatar;

        _feedback.MusicOn = _profile.MusicOn;
        _feedback.SoundOn = _profile.SoundOn;
        _feedback.VibrationOn = _profile.VibrationOn;

        if (GUI.Button(new Rect(w * 0.4f, 89 * u, w * 0.2f, 9 * u), Text.Back, Ui.Button))
        {
            Save();
            if (_view != null) _view.ApplySettings(_profile);
            _page = _settingsReturn;
        }
    }

    private static bool Toggle(Rect r, string label, bool value)
    {
        GUI.Label(new Rect(r.x, r.y, r.width * 0.6f, r.height), label, Ui.Label);
        return GUI.Button(new Rect(r.x + r.width * 0.6f, r.y, r.width * 0.4f, r.height), value ? Text.On : Text.Off, Ui.Button) ? !value : value;
    }
}
