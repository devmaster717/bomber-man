/// <summary>
/// Every piece of on-screen text, in one table so the game can be translated later (English only for now).
/// </summary>
public static class Text
{
    public const string GameTitle = "Bomb Arena";

    // First launch
    public const string Welcome = "Welcome to Bomb Arena!";
    public static string ChooseNickname(int max) => $"Choose a nickname (up to {max} characters)";
    public const string PickLookFree = "Pick your look (free)";
    public const string Start = "Start";

    // Home
    public const string StageMode = "Stage mode";
    public const string Bluetooth = "Bluetooth battle";
    public const string Shop = "Shop";
    public const string Settings = "Settings";
    public const string ComingSoon = " (coming soon)";
    public static string NextLifeIn(string clock) => $"({clock})";
    public const string AttemptCutShort = "Your last attempt was cut short, so it cost a life.";
    public const string NoLives = "No lives left. Wait for one to regenerate.";

    // Stage select
    public const string ChooseStage = "Choose a stage";
    public const string Prev = "< Prev";
    public const string Next = "Next >";
    public const string Home = "Home";
    public static string StageRange(int first, int last) => $"Stages {first}-{last}";

    // In play
    public static string Hud(int stage, int enemies, string time) => $"Stage {stage}    Enemies: {enemies}    Time {time}";
    public static string ThreeStarsUnder(string clock) => $"    3 stars under {clock}";
    public const string HeldFireUp = "Fire Up";
    public static string HeldBombs(int n) => $"Bombs {n}";
    public const string HeldRemote = "Remote";
    public static string HeldSpeed(int seconds) => $"Speed {seconds}s";
    public const string PauseButton = "II";
    public const string BombButton = "BOMB";
    public const string DetonateButton = "DETONATE";

    // Pause
    public const string Paused = "Paused";
    public const string Resume = "Resume";
    public const string Restart = "Restart (costs a life)";
    public const string Quit = "Quit (costs a life)";

    // Results
    public const string StageClear = "Stage clear!";
    public static string ClearTime(string time) => $"Time {time}";
    public static string JewelBreakdown(long clear, long bonus, bool first3) =>
        $"{clear} for the clear + {bonus} {(first3 ? "for your first 3 stars" : "for the stars")}";
    public const string TimeUp = "Time up!";
    public const string YouDied = "You died";
    public static string LivesLeft(int n) => $"Lives left: {n}";
    public const string NextStage = "Next stage";
    public const string PlayAgain = "Play again";
    public const string StageSelect = "Stage select";

    // Shop
    public static string LifePack(int lives, long price) => $"{lives} lives - {price}";
    public const string Buy = "Buy";
    public const string PowerUps = "Power-ups (used at the start of your next stage or round)";
    public static string PowerUpName(BombArena.Core.PowerUpKind k) => k switch
    {
        BombArena.Core.PowerUpKind.FireUp => "Fire Up",
        BombArena.Core.PowerUpKind.BombUp => "Bomb Up",
        BombArena.Core.PowerUpKind.RemoteControl => "Remote",
        _ => "Speed Up",
    };
    public static string PriceTag(string name, long price) => $"{name}  {price}";
    public static string Inventory(string items) => $"In your inventory: {items}";
    public const string InventoryEmpty = "Your inventory is empty.";
    public const string Avatars = "Avatars";
    public const string Owned = "owned";
    public const string JewelPacks = "Jewel packs (QR payment)";
    public const string Bought = "Bought!";
    public const string NotEnoughJewels = "Not enough jewels.";
    public const string NotForSale = "You already have that.";
    public const string QrTitle = "Buy jewels";
    public const string QrPlaceholder = "Payments are coming soon. This QR code is a placeholder and does not credit any jewels.";
    public const string QrPacks = "Pack sizes and prices: to be decided";

    // Bluetooth
    public const string BluetoothUnavailable = "Bluetooth battles need an Android phone with Bluetooth.";
    public const string BluetoothOff = "Bluetooth is off.";
    public const string TurnOnBluetooth = "Turn on Bluetooth";
    public const string PermissionWhy =
        "Bluetooth battles find and connect to phones near you. For that, Android will ask to allow Nearby devices " +
        "(on older phones, Location). It is only used while finding and connecting for a battle; your location is never read or stored.";
    public const string Continue = "Continue";
    public const string OpenAppSettings = "Open app settings";
    public const string BluetoothPermissionsNeeded = "Bomb Arena needs the Nearby devices permission to find other phones.";
    public const string HostRoom = "Host a room";
    public const string JoinRoom = "Join a room";
    public const string CouldNotHost = "Could not open a room. Is Bluetooth on?";
    public const string WaitingForPlayers = "Waiting for players to join...";
    public const string LookingForRooms = "Looking for nearby rooms...";
    public const string NearbyPhones = "Nearby phones";
    public const string NoRoomsFound = "No rooms found. Ask the host to open a room, then scan again.";
    public const string ScanAgain = "Scan again";
    public const string Paired = "paired";
    public const string Connecting = "Connecting...";
    public const string ConnectionLost = "The connection was lost.";
    public const string YourRoom = "Your room";
    public const string InRoom = "In the room";
    public const string HostLabel = "host";
    public static string PlayersInRoom(int n, int max) => $"{n} of {max} players";
    public const string Leave = "Leave";
    public const string Out = "out";
    public static string ArenaSize(int w, int h) => $"Arena {w} x {h}";
    public static string RoundTime(int seconds) => seconds == 0 ? "Time: unlimited" : $"Time: {seconds / 60} min";
    public const string StartRound = "Start round";
    public const string NeedAnotherPlayer = "Waiting for at least one more player.";
    public static string Winner(string nickname) => $"{nickname} wins!";
    public const string YouWin = "You win!";
    public const string Draw = "Draw";
    public const string HostLeft = "The host's connection was lost. The round has ended.";
    public const string BackToMenu = "Back";
    public static string EntryFee(long fee) => $"Fee {fee}";
    public static string Enemies(bool on) => on ? "Enemies on" : "Enemies off";
    public const string ForfeitButton = "Forfeit";
    public static string WaitingFor(string nickname, int seconds) => $"Waiting for {nickname}... {seconds}s";
    public const string CountAsForfeit = "Count them as a forfeit";
    public const string LinkLostTitle = "Connection lost";
    public const string ReconnectHint = "Reconnect to carry on, or leave (your fee stays in the pot).";
    public const string Reconnect = "Reconnect";
    public const string Reconnecting = "Reconnecting...";
    public const string SomeoneCannotPay = "Everyone needs at least the entry fee in jewels.";
    public static string PotWon(long pot) => $"Pot: {pot} jewels";
    public static string JewelChange(long change) => change > 0 ? $"You get {change} jewels" : change == 0 ? "No jewels back" : $"{change} jewels";
    public const string FeesRefunded = "Every fee is refunded.";
    public const string NoRefund = "Nobody gets their fee back.";

    // Settings
    public const string Music = "Music";
    public const string SoundEffects = "Sound effects";
    public const string Vibration = "Vibration";
    public const string On = "On";
    public const string Off = "Off";
    public const string Controls = "Controls";
    public const string DPad = "D-pad";
    public const string Joystick = "Joystick";
    public static string ButtonSize(int percent) => $"Button size {percent}%";
    public const string PadSide = "Pad side";
    public const string Left = "Left";
    public const string Right = "Right";
    public const string View = "View";
    public const string View2D = "2D";
    public const string View3D = "3D";
    public const string Graphics = "Graphics";
    public const string Low = "Low";
    public const string High = "High";
    public const string Arena = "Arena";
    public const string Nickname = "Nickname";
    public const string Avatar = "Avatar";
    public const string Back = "Back";
    public const string QuitGame = "Leave Bomb Arena?";
    public const string PrivacyPolicy = "Privacy policy";
    public static string VersionLabel(string version) => $"Version {version}";

    // How to play
    public const string HowToPlay = "How to play";
    public const string Done = "Done";
    public const string Skip = "Skip";
    public const string HowMoveTitle = "Move and bomb";
    public const string HowMove = "Move with the pad (or joystick). Tap BOMB to drop a bomb: after 2.5 seconds it bursts into fire along its row and column. Fire stops at walls and pillars. Get out of the way!";
    public const string HowExitTitle = "Crates and the exit";
    public const string HowExit = "Fire breaks wooden crates. One crate in every stage hides the exit. Once every enemy is gone it opens: walk in to clear the stage. Careful: fire on the exit lets Runners out.";
    public const string HowPowerTitle = "Power-ups";
    public const string HowPower = "One crate per attempt hides a power-up. Fire Up: longer fire. Bomb Up: one more bomb at a time. Remote: your bombs wait until you tap DETONATE. Speed Up: faster for 60 seconds.";
    public const string HowEnemiesTitle = "Enemies";
    public const string HowEnemies = "Touching an enemy ends the attempt. Walkers are slower than you. Runners are twice as fast. Phantoms fade out and reappear elsewhere. Wall-passers glide through crates, where fire can't reach them.";
    public const string HowLivesTitle = "Lives and stars";
    public const string HowLives = "Each attempt needs a life, and only a failed one uses it up. Lives come back over time, or buy more in the Shop. Clear a stage fast for up to three stars and more jewels; finishing unlocks the next stage.";
    public const string HowBattleTitle = "Bluetooth battles";
    public const string HowBattle = "Two or three friends on nearby phones. One hosts a room, the others join. Everyone pays the entry fee; the last bomber standing wins the whole pot.";
    public const string PrivacyText =
        "Bomb Arena does not collect, send or sell any personal information.\n\n" +
        "What is stored, and where\nYour nickname, avatar, jewels, lives, stage progress and settings are saved only on your phone, " +
        "in the app's own storage. An error log, used only to look into problems, stays on the phone too. Uninstalling the app deletes all of it.\n\n" +
        "Bluetooth battles\nBluetooth is used only to find nearby phones and play the battles you start. Nicknames, avatars and game " +
        "moves travel directly between the phones in the room. Nothing is sent to the internet.\n\n" +
        "Permissions\nNearby devices (Android 12 and later), or Location (Android 8 to 11, where Android requires it for Bluetooth " +
        "discovery): used only while finding and connecting to other phones for a battle. Your location is never read or stored. " +
        "Vibration: for game feedback, which you can turn off in Settings.\n\n" +
        "What Bomb Arena does not do\nNo accounts, advertising, analytics or tracking.\n\n" +
        "Purchases\nBuying jewels is not available yet. This policy will be updated before it is.";
    public const string Stay = "Stay";
}
