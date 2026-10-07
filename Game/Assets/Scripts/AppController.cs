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
        Shop,
        JewelPacks,
        Bluetooth,
        Privacy,
        HowToPlay,
    }

    private const int StagesPerPage = 20;

    private PlayerProfile _profile;
    private IWallet _wallet;
    private StageProgress _progress;
    private Shop _shop;
    private BattleScreens _battle;
    private string _shopMessage;
    private Texture2D _qr;
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
    private bool _confirmQuit;
    private readonly HowToPlay _howToPlay = new HowToPlay();
    private Page _shownPage;
    private float _pageShownAt;

    private void Awake()
    {
        ErrorLog.Install();
        Application.targetFrameRate = 60;
        _profile = ProfileStore.Load();
        if (_profile.HighGraphics == null)
        {
            _profile.HighGraphics = GraphicsQuality.SuitsDevice;
            ProfileStore.Save(_profile);
        }
        GraphicsQuality.Apply(_profile.HighGraphics.Value);
        _wallet = new LocalWallet(_profile, ProfileStore.Now, Save);
        _progress = new StageProgress(_profile, _wallet, Save);
        _shop = new Shop(_profile, _wallet, Save);
        _battle = new BattleScreens(_profile, _wallet);
        _battle.Exit += GoHome;
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
        // Music follows the screen: the hornpipe in stages, the allegro in battles, the minuet everywhere else.
        bool inStage = _page is Page.Playing or Page.Paused or Page.Results || (_page == Page.Settings && _settingsReturn == Page.Paused);
        _feedback.Music(inStage ? Feedback.Track.Stage : _page == Page.Bluetooth && _battle.InRound ? Feedback.Track.Battle : Feedback.Track.Menu);
        if (_page == Page.Bluetooth) _battle.Update();
        if (Input.GetKeyDown(KeyCode.Escape)) Back(); // Android's Back button
        else if (_page == Page.Playing && Input.GetKeyDown(KeyCode.P)) Pause();
    }

    /// <summary>The Android Back button: what the screen's own Back would do; in play it pauses.</summary>
    private void Back()
    {
        switch (_page)
        {
            case Page.Playing: Pause(); break;
            case Page.Paused: Resume(); break;
            case Page.Settings: LeaveSettings(); break;
            case Page.StageSelect:
            case Page.Shop: _page = Page.Home; break;
            case Page.JewelPacks: _page = Page.Shop; break;
            case Page.Privacy: _page = Page.Settings; break;
            case Page.HowToPlay: _page = Page.Home; break;
            case Page.Results:
                CloseView();
                _page = Page.StageSelect;
                SetHomeCamera();
                break;
            case Page.Home: _confirmQuit = !_confirmQuit; break;
            case Page.Bluetooth: _battle.Back(); break;
        }
    }

    // Leaving the app (a call, the home button) pauses the attempt and saves everything.
    private void OnApplicationPause(bool paused)
    {
        // Back from the background: credit the lives that came back meanwhile (they count by the clock).
        if (!paused)
        {
            _wallet.Refresh();
            return;
        }
        if (_page == Page.Playing) Pause();
        Save();
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
        cam.orthographic = true;
        cam.transform.SetPositionAndRotation(new Vector3(0, 0, -10f), Quaternion.identity);
    }

    // ---- screens ----

    private void OnGUI()
    {
        Ui.Begin();
        var e = Event.current;
        bool pressed = e.type == EventType.MouseDown;
        // Menus sit on the palace backdrop; in play (and its pause, results and settings) the arena shows instead.
        bool arenaShowing = _page is Page.Playing or Page.Paused or Page.Results
            || (_page == Page.Settings && _settingsReturn == Page.Paused)
            || (_page == Page.Bluetooth && _battle.InRound);
        if (!arenaShowing) Ui.Backdrop();
        switch (_page)
        {
            case Page.Setup: DrawSetup(); break;
            case Page.Home:
                // Behind the quit question Home is shown but can't be pressed (IMGUI gives a tap to the first control drawn).
                GUI.enabled = !_confirmQuit;
                DrawHome();
                GUI.enabled = true;
                break;
            case Page.StageSelect: DrawStageSelect(); break;
            case Page.Playing: DrawPauseButton(); break;
            case Page.Paused: DrawPauseMenu(); break;
            case Page.Results: DrawResults(); break;
            case Page.Settings: DrawSettings(); break;
            case Page.Shop: DrawShop(); break;
            case Page.JewelPacks: DrawJewelPacks(); break;
            case Page.Privacy: DrawPrivacy(); break;
            case Page.HowToPlay: if (_howToPlay.Draw()) _page = Page.Home; break;
            case Page.Bluetooth: _battle.OnGUI(); break;
        }
        if (_page == Page.Home && _confirmQuit) DrawQuitConfirm();
        else _confirmQuit = false;

        // Any control that took the press (button, slider, text field) clicks, with a light tap.
        if (pressed && e.type == EventType.Used) _feedback.Click();

        // Each new screen fades in from navy.
        if (_page != _shownPage)
        {
            _shownPage = _page;
            _pageShownAt = Time.unscaledTime;
        }
        float fade = 1f - (Time.unscaledTime - _pageShownAt) / 0.25f;
        if (fade > 0f) Ui.Fade(fade);
    }

    private void DrawQuitConfirm()
    {
        var col = new Ui.Column(Ui.Panel(0.45f, 0.45f));
        GUI.Label(col.Next(12), Text.QuitGame, Ui.Label);
        if (GUI.Button(col.Next(10), Text.Stay, Ui.Button)) _confirmQuit = false;
        if (GUI.Button(col.Next(10), Text.Leave, Ui.Button)) Application.Quit();
    }

    /// <summary>Jewels (a ruby) and lives (a heart, with the next-life countdown) across the top.</summary>
    private void DrawWalletBar()
    {
        _wallet.Refresh();
        string lives = _wallet.Lives.ToString();
        if (_wallet.SecondsToNextLife is long s) lives += "  " + Text.NextLifeIn(Ui.Clock(s));
        Ui.IconRow(new Rect(0, Ui.U, Ui.W, 7 * Ui.U), Ui.Numbers, (Ui.Jewel, _wallet.Jewels.ToString()), (Ui.Heart, lives));
    }

    /// <summary>First launch: choose a nickname and a free starting avatar.</summary>
    private void DrawSetup()
    {
        float w = Ui.W, u = Ui.U;
        GUI.Label(new Rect(0, 4 * u, w, 12 * u), Text.Welcome, Ui.Title);
        GUI.Label(new Rect(0, 17 * u, w, 7 * u), Text.ChooseNickname(PlayerProfile.MaxNicknameLength), Ui.Label);
        _nicknameDraft = GUI.TextField(new Rect(w * 0.3f, 25 * u, w * 0.4f, 10 * u), _nicknameDraft, PlayerProfile.MaxNicknameLength, Ui.Field);

        GUI.Label(new Rect(0, 38 * u, w, 7 * u), Text.PickLookFree, Ui.Label);
        DrawAvatarRow(46 * u, ref _setupAvatar, i => true);

        GUI.enabled = PlayerProfile.IsValidNickname(_nicknameDraft);
        if (GUI.Button(new Rect(w * 0.35f, 78 * u, w * 0.3f, 12 * u), Text.Start, Ui.Button))
        {
            _profile.CompleteSetup(_nicknameDraft, _setupAvatar);
            // A new player sees How to play once, straight after choosing a nickname.
            Save();
            _howToPlay.Reset();
            _page = Page.HowToPlay;
        }
        GUI.enabled = true;
    }

    /// <summary>A row of the 10 avatars; tapping an available one selects it.</summary>
    private static void DrawAvatarRow(float y, ref int selected, System.Func<int, bool> available)
    {
        float u = Ui.U, size = 14 * u, gap = 2 * u;
        float total = PlayerProfile.AvatarCount * size + (PlayerProfile.AvatarCount - 1) * gap;
        float x = (Ui.W - total) / 2;
        for (int i = 0; i < PlayerProfile.AvatarCount; i++)
        {
            var r = new Rect(x + i * (size + gap), y, size, size);
            var old = GUI.color;
            GUI.enabled = available(i);
            GUI.color = i == selected ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            if (GUI.Button(r, GUIContent.none, Ui.Button)) selected = i;
            GUI.DrawTexture(new Rect(r.x + size * 0.15f, r.y + size * 0.15f, size * 0.7f, size * 0.7f),
                PlayerAvatars.Portrait(i), ScaleMode.ScaleToFit);
            GUI.color = old;
            GUI.enabled = true;
        }
    }

    private void DrawHome()
    {
        DrawWalletBar();
        float u = Ui.U;
        GUI.DrawTexture(new Rect(3 * u, 2 * u, 10 * u, 10 * u), PlayerAvatars.Portrait(_profile.Avatar), ScaleMode.ScaleToFit);
        GUI.DrawTexture(new Rect(2.4f * u, 1.4f * u, 11.2f * u, 11.2f * u), Ui.AvatarRing, ScaleMode.ScaleToFit);
        GUI.Label(new Rect(14 * u, 2 * u, 40 * u, 10 * u), _profile.Nickname, Ui.Small);
        var col = new Ui.Column(new Rect(Ui.W * 0.3f, Ui.H * 0.12f, Ui.W * 0.4f, Ui.H * 0.88f), 0f);
        GUI.Label(col.Next(14), Text.GameTitle, Ui.Title);
        if (GUI.Button(col.Next(11), Text.StageMode, Ui.Button))
        {
            _selectPage = (_stage - 1) / StagesPerPage;
            _page = Page.StageSelect;
        }
        if (GUI.Button(col.Next(11), Text.Bluetooth, Ui.Button))
        {
            _battle.Open();
            _page = Page.Bluetooth;
        }
        if (GUI.Button(col.Next(11), Text.Shop, Ui.Button))
        {
            _shopMessage = null;
            _page = Page.Shop;
        }
        if (GUI.Button(col.Next(11), Text.Settings, Ui.Button)) OpenSettings(Page.Home);
        if (_notice != null) GUI.Label(col.Next(10), _notice, Ui.Small);
        if (GUI.Button(new Rect(Ui.W * 0.74f, 86 * u, Ui.W * 0.2f, 8 * u), Text.HowToPlay, Ui.SmallButton))
        {
            _howToPlay.Reset();
            _page = Page.HowToPlay;
        }
    }

    private void DrawStageSelect()
    {
        DrawWalletBar();
        float w = Ui.W, u = Ui.U;
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
            // The number above the best result (three stars, gold for each one earned) or a padlock.
            GUI.enabled = open;
            if (GUI.Button(r, n + System.Environment.NewLine + " ", Ui.NumberButton)) StartAttempt(n);
            GUI.enabled = true;
            if (open) DrawStars(r, _progress.BestStars(n));
            else DrawLock(r);
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

    // A padlock in the lower half of a locked stage's button, dimmed with it.
    private static void DrawLock(Rect button)
    {
        float size = button.height * 0.42f;
        var old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.75f);
        GUI.DrawTexture(new Rect(button.center.x - size / 2f, button.y + button.height * 0.48f, size, size), Ui.Lock, ScaleMode.ScaleToFit);
        GUI.color = old;
    }

    // Three stars in the lower half of a stage button.
    private static void DrawStars(Rect button, int earned)
    {
        float size = button.height * 0.36f, gap = size * 0.12f;
        float x = button.center.x - (3 * size + 2 * gap) / 2f, y = button.y + button.height * 0.52f;
        for (int i = 0; i < 3; i++)
            GUI.DrawTexture(new Rect(x + i * (size + gap), y, size, size), Ui.Star(i < earned), ScaleMode.ScaleToFit);
    }

    private void DrawPauseButton()
    {
        float s = 10 * Ui.U;
        if (GUI.Button(new Rect(Ui.W - s - 2 * Ui.U, 2 * Ui.U, s, s), Text.PauseButton, Ui.Button)) Pause();
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
        float w = Ui.W, u = Ui.U;
        GUI.Label(new Rect(0, 3 * u, w, 10 * u), Text.Settings, Ui.Title);

        // Left column: sound, vibration and controls (rows of 8, 1 apart).
        float lx = w * 0.04f, lw = w * 0.44f;
        Rect Row(float yU, float hU = 8) => new Rect(lx, yU * u, lw, hU * u);
        _profile.MusicOn = Toggle(Row(13), Text.Music, _profile.MusicOn);
        _profile.SoundOn = Toggle(Row(22), Text.SoundEffects, _profile.SoundOn);
        _profile.VibrationOn = Toggle(Row(31), Text.Vibration, _profile.VibrationOn);
        _profile.UseJoystick = Choice(Row(40), Text.Controls, Text.DPad, Text.Joystick, _profile.UseJoystick);
        GUI.Label(Row(49, 5), Text.ButtonSize(_profile.ButtonScalePercent), Ui.Small);
        _profile.ButtonScalePercent = Mathf.RoundToInt(GUI.HorizontalSlider(Row(55, 4), _profile.ButtonScalePercent, 75, 150) / 5f) * 5;
        _profile.PadOnRight = Choice(Row(60), Text.PadSide, Text.Left, Text.Right, _profile.PadOnRight);

        // Right column: nickname.
        float rx = w * 0.52f, rw = w * 0.44f;
        GUI.Label(new Rect(rx, 13 * u, rw, 7 * u), Text.Nickname, Ui.Label);
        _nicknameDraft = GUI.TextField(new Rect(rx, 22 * u, rw, 10 * u), _nicknameDraft, PlayerProfile.MaxNicknameLength, Ui.Field);
        if (PlayerProfile.IsValidNickname(_nicknameDraft)) _profile.Nickname = PlayerProfile.CleanNickname(_nicknameDraft);

        // The arena view: the new 3D one or the original flat 2D one.
        _profile.View3D = Choice(new Rect(rx, 40 * u, rw, 8 * u), Text.View, Text.View2D, Text.View3D, _profile.View3D);
        bool high = Choice(new Rect(rx, 49 * u, rw, 8 * u), Text.Graphics, Text.Low, Text.High, _profile.HighGraphics ?? true);
        if (high != _profile.HighGraphics)
        {
            _profile.HighGraphics = high;
            GraphicsQuality.Apply(high);
        }
        _profile.Arena = Cycle(new Rect(rx, 58 * u, rw, 8 * u), Text.Arena, ArenaTheme.All.Length, _profile.Arena, i => ArenaTheme.At(i).Name);

        // Bottom: avatars the player owns can be chosen (more come from the shop).
        GUI.Label(new Rect(0, 69 * u, w, 5 * u), Text.Avatar, Ui.Small);
        int avatar = _profile.Avatar;
        DrawAvatarRow(74 * u, ref avatar, i => _profile.OwnedAvatars.Contains(i));
        _profile.Avatar = avatar;

        _feedback.MusicOn = _profile.MusicOn;
        _feedback.SoundOn = _profile.SoundOn;
        _feedback.VibrationOn = _profile.VibrationOn;

        if (GUI.Button(new Rect(w * 0.4f, 89 * u, w * 0.2f, 9 * u), Text.Back, Ui.Button)) LeaveSettings();
        GUI.Label(new Rect(w * 0.04f, 90 * u, w * 0.25f, 7 * u), Text.VersionLabel(Application.version), Ui.Small);
        if (GUI.Button(new Rect(w * 0.74f, 90 * u, w * 0.2f, 7 * u), Text.PrivacyPolicy, Ui.SmallButton))
        {
            Save();
            _page = Page.Privacy;
        }
    }

    private Vector2 _privacyScroll;

    /// <summary>The privacy policy (the same text as docs/privacy-policy.md, for the store listing).</summary>
    private void DrawPrivacy()
    {
        if (_settingsReturn == Page.Paused) Ui.Panel(0.95f, 0.95f);
        float w = Ui.W, u = Ui.U;
        GUI.Label(new Rect(0, 3 * u, w, 10 * u), Text.PrivacyPolicy, Ui.Title);
        var view = new Rect(w * 0.1f, 15 * u, w * 0.8f, 70 * u);
        float textHeight = Ui.Small.CalcHeight(new GUIContent(Text.PrivacyText), view.width - 4 * u);
        var small = new GUIStyle(Ui.Small) { alignment = TextAnchor.UpperLeft };
        _privacyScroll = GUI.BeginScrollView(view, _privacyScroll, new Rect(0, 0, view.width - 4 * u, textHeight));
        GUI.Label(new Rect(0, 0, view.width - 4 * u, textHeight), Text.PrivacyText, small);
        GUI.EndScrollView();
        if (GUI.Button(new Rect(w * 0.4f, 88 * u, w * 0.2f, 9 * u), Text.Back, Ui.Button)) _page = Page.Settings;
    }

    private void LeaveSettings()
    {
        Save();
        if (_view != null) _view.ApplySettings(_profile);
        _page = _settingsReturn;
    }

    private void DrawShop()
    {
        DrawWalletBar();
        float w = Ui.W, u = Ui.U;
        GUI.Label(new Rect(0, 7 * u, w, 9 * u), Text.Shop, Ui.Title);
        float x = w * 0.08f, cw = w * 0.84f;

        // Lives
        if (GUI.Button(new Rect(x, 17 * u, cw * 0.48f, 9 * u), Text.LifePack(Shop.LifePackLives, Shop.LifePackPrice), Ui.Button))
            _shopMessage = Message(_shop.BuyLifePack());
        if (GUI.Button(new Rect(x + cw * 0.52f, 17 * u, cw * 0.48f, 9 * u), Text.JewelPacks, Ui.Button))
            _page = Page.JewelPacks;

        // Power-ups
        GUI.Label(new Rect(x, 28 * u, cw, 6 * u), Text.PowerUps, Ui.Small);
        var kinds = new[] { PowerUpKind.FireUp, PowerUpKind.BombUp, PowerUpKind.RemoteControl, PowerUpKind.SpeedUp };
        for (int i = 0; i < kinds.Length; i++)
        {
            var r = new Rect(x + i * cw / 4 + u, 35 * u, cw / 4 - 2 * u, 12 * u);
            GUI.enabled = _shop.CanBuy(kinds[i]);
            GUI.DrawTexture(new Rect(r.x + u, r.y + 2 * u, 8 * u, 8 * u), PalaceSprites.PowerUp(kinds[i]).texture, ScaleMode.ScaleToFit);
            if (GUI.Button(new Rect(r.x + 10 * u, r.y, r.width - 10 * u, r.height), Text.PriceTag(Text.PowerUpName(kinds[i]), Shop.PowerUpPrice(kinds[i])), Ui.Small))
                _shopMessage = Message(_shop.BuyPowerUp(kinds[i]));
            GUI.enabled = true;
        }
        GUI.Label(new Rect(x, 48 * u, cw, 6 * u), InventoryText(), Ui.Small);

        // Avatars
        GUI.Label(new Rect(x, 55 * u, cw, 6 * u), Text.Avatars, Ui.Small);
        float size = 12 * u, gap = (cw - PlayerProfile.AvatarCount * size) / (PlayerProfile.AvatarCount - 1);
        for (int i = 0; i < PlayerProfile.AvatarCount; i++)
        {
            var r = new Rect(x + i * (size + gap), 61 * u, size, size);
            bool owned = _shop.Owns(i);
            if (GUI.Button(r, GUIContent.none, Ui.Button) && !owned) _shopMessage = Message(_shop.BuyAvatar(i));
            GUI.DrawTexture(new Rect(r.x + size * 0.15f, r.y + size * 0.15f, size * 0.7f, size * 0.7f), PlayerAvatars.Portrait(i), ScaleMode.ScaleToFit);
            GUI.Label(new Rect(r.x - gap / 2, r.yMax, size + gap, 5 * u), owned ? Text.Owned : Shop.AvatarPrice.ToString(), Ui.Small);
        }

        if (_shopMessage != null) GUI.Label(new Rect(0, 79 * u, w, 6 * u), _shopMessage, Ui.Label);
        if (GUI.Button(new Rect(w * 0.4f, 87 * u, w * 0.2f, 10 * u), Text.Back, Ui.Button)) _page = Page.Home;
    }

    private string InventoryText()
    {
        var inv = _profile.Inventory;
        if (inv.IsEmpty) return Text.InventoryEmpty;
        var items = new System.Collections.Generic.List<string>();
        if (inv.FireUp) items.Add(Text.PowerUpName(PowerUpKind.FireUp));
        if (inv.BombUps > 0) items.Add(Text.PowerUpName(PowerUpKind.BombUp) + (inv.BombUps > 1 ? " x" + inv.BombUps : ""));
        if (inv.RemoteControl) items.Add(Text.PowerUpName(PowerUpKind.RemoteControl));
        if (inv.SpeedUpTicksLeft > 0) items.Add(Text.PowerUpName(PowerUpKind.SpeedUp));
        return Text.Inventory(string.Join(", ", items));
    }

    private static string Message(PurchaseResult r) => r switch
    {
        PurchaseResult.Bought => Text.Bought,
        PurchaseResult.NotEnoughJewels => Text.NotEnoughJewels,
        _ => Text.NotForSale,
    };

    /// <summary>QR payment placeholder: shows a dummy code and credits nothing (ADR 0002).</summary>
    private void DrawJewelPacks()
    {
        DrawWalletBar();
        float w = Ui.W, u = Ui.U;
        GUI.Label(new Rect(0, 7 * u, w, 9 * u), Text.QrTitle, Ui.Title);
        _qr ??= PlaceholderQr();
        float size = 46 * u;
        GUI.DrawTexture(new Rect((w - size) / 2, 18 * u, size, size), _qr, ScaleMode.ScaleToFit);
        GUI.Label(new Rect(w * 0.15f, 66 * u, w * 0.7f, 10 * u), Text.QrPlaceholder, Ui.Small);
        GUI.Label(new Rect(0, 77 * u, w, 6 * u), Text.QrPacks, Ui.Small);
        if (GUI.Button(new Rect(w * 0.4f, 87 * u, w * 0.2f, 10 * u), Text.Back, Ui.Button)) _page = Page.Shop;
    }

    private static Texture2D PlaceholderQr()
    {
        const int n = 25;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var rng = new System.Random(2026);
        bool Finder(int x, int y, int ox, int oy)
        {
            int dx = x - ox, dy = y - oy;
            if (dx < 0 || dy < 0 || dx > 6 || dy > 6) return false;
            return dx == 0 || dy == 0 || dx == 6 || dy == 6 || (dx >= 2 && dx <= 4 && dy >= 2 && dy <= 4);
        }
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            bool inFinderArea = (x < 8 && y < 8) || (x > n - 9 && y < 8) || (x < 8 && y > n - 9);
            bool dark = inFinderArea ? Finder(x, y, 0, 0) || Finder(x, y, n - 7, 0) || Finder(x, y, 0, n - 7) : rng.Next(2) == 0;
            t.SetPixel(x, y, dark ? Color.black : Color.white);
        }
        t.Apply();
        return t;
    }

    /// <summary>A label and two buttons; the chosen one is bright, the other dimmed. Returns true for the second.</summary>
    private static bool Choice(Rect r, string label, string first, string second, bool secondChosen)
    {
        GUI.Label(new Rect(r.x, r.y, r.width * 0.4f, r.height), label, Ui.Label);
        var old = GUI.color;
        GUI.color = secondChosen ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
        if (GUI.Button(new Rect(r.x + r.width * 0.4f, r.y, r.width * 0.3f, r.height), first, Ui.Button)) secondChosen = false;
        GUI.color = secondChosen ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        if (GUI.Button(new Rect(r.x + r.width * 0.7f, r.y, r.width * 0.3f, r.height), second, Ui.Button)) secondChosen = true;
        GUI.color = old;
        return secondChosen;
    }

    /// <summary>A label and the chosen option between back and forward arrows, which step through <paramref name="count"/> options.</summary>
    private static int Cycle(Rect r, string label, int count, int chosen, System.Func<int, string> name)
    {
        chosen = ((chosen % count) + count) % count;
        GUI.Label(new Rect(r.x, r.y, r.width * 0.4f, r.height), label, Ui.Label);
        if (GUI.Button(new Rect(r.x + r.width * 0.4f, r.y, r.width * 0.12f, r.height), "<", Ui.Button)) chosen = (chosen + count - 1) % count;
        GUI.Label(new Rect(r.x + r.width * 0.52f, r.y, r.width * 0.36f, r.height), name(chosen), Ui.Label);
        if (GUI.Button(new Rect(r.x + r.width * 0.88f, r.y, r.width * 0.12f, r.height), ">", Ui.Button)) chosen = (chosen + 1) % count;
        return chosen;
    }

    private static bool Toggle(Rect r, string label, bool value)
    {
        GUI.Label(new Rect(r.x, r.y, r.width * 0.6f, r.height), label, Ui.Label);
        return GUI.Button(new Rect(r.x + r.width * 0.6f, r.y, r.width * 0.4f, r.height), value ? Text.On : Text.Off, Ui.Button) ? !value : value;
    }
}
