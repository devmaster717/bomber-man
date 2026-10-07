using System;
using System.Collections.Generic;
using BombArena.Core.Net;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>A phone found while scanning.</summary>
public sealed class NearbyPhone
{
    public string Address;
    public string Name;
    public bool Paired;
    public bool RunsBombArena;
}

/// <summary>
/// The C# side of the BluetoothLink Java plugin (classic RFCOMM, ADR 0001). Implements the core's host transport
/// and hands out a guest transport once a join succeeds. Call <see cref="Poll"/> every frame.
/// </summary>
public sealed class BluetoothTransport : IHostTransport, IDisposable
{
    public event Action<int> PeerConnected;
    public event Action<int, byte[]> Received;
    public event Action<int> PeerDisconnected;

    /// <summary>Raised when a join attempt connects; the guest transport talks to the host.</summary>
    public event Action<IGuestTransport> JoinedHost;
    public event Action<string> Error;

    public List<NearbyPhone> Nearby { get; } = new List<NearbyPhone>();
    public bool Scanning { get; private set; }

    private readonly AndroidJavaObject _link;
    private Guest _guest;

    public BluetoothTransport()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
        _link = new AndroidJavaObject("com.bombarena.bluetooth.BluetoothLink", activity);
#endif
    }

    /// <summary>False in the editor and on phones without Bluetooth.</summary>
    public bool Available => _link != null && _link.Call<bool>("isAvailable");
    public bool Enabled => _link != null && _link.Call<bool>("isEnabled");

    public void RequestEnable() => _link?.Call("requestEnable");

    /// <summary>Asks Android to make this phone visible to nearby phones (a system prompt, up to 5 minutes).</summary>
    public void RequestDiscoverable() => _link?.Call("requestDiscoverable", 300);

    public bool StartHosting() => _link != null && _link.Call<bool>("startHosting");
    public void StopHosting() => _link?.Call("stopHosting");

    public void StartScan()
    {
        Nearby.Clear();
        Scanning = true;
        _link?.Call("startScan");
    }

    public void StopScan()
    {
        Scanning = false;
        _link?.Call("stopScan");
    }

    public void Connect(string address) => _link?.Call("connect", address);

    public void Send(int peer, byte[] message) => _link?.Call<bool>("send", peer, Convert.ToBase64String(message));
    public void Disconnect(int peer) => _link?.Call("close", peer);

    public void Dispose() => _link?.Call("shutdown");

    /// <summary>Delivers the plugin's queued events on the main thread.</summary>
    public void Poll()
    {
        if (_link == null) return;
        for (int guard = 0; guard < 500; guard++)
        {
            var e = _link.Call<string>("poll");
            if (e == null) return;
            var f = e.Split('\t');
            switch (f[0])
            {
                case "found":
                    if (!Nearby.Exists(p => p.Address == f[1]))
                        Nearby.Add(new NearbyPhone { Address = f[1], Name = f[2], Paired = f[3] == "1", RunsBombArena = f[4] == "1" });
                    break;
                case "game":
                    var phone = Nearby.Find(p => p.Address == f[1]);
                    if (phone != null) phone.RunsBombArena = true;
                    break;
                case "scanDone":
                    Scanning = false;
                    break;
                case "connected":
                    int id = int.Parse(f[1]);
                    if (f[4] == "1") PeerConnected?.Invoke(id);
                    else
                    {
                        _guest = new Guest(this, id);
                        JoinedHost?.Invoke(_guest);
                    }
                    break;
                case "data":
                    int from = int.Parse(f[1]);
                    var bytes = Convert.FromBase64String(f[2]);
                    if (_guest != null && _guest.Id == from) _guest.Deliver(bytes);
                    else Received?.Invoke(from, bytes);
                    break;
                case "closed":
                    int gone = int.Parse(f[1]);
                    if (_guest != null && _guest.Id == gone) { _guest.Closed(); _guest = null; }
                    else PeerDisconnected?.Invoke(gone);
                    break;
                case "error":
                    Error?.Invoke(f.Length > 1 ? f[1] : "Bluetooth error");
                    break;
            }
        }
    }

    /// <summary>
    /// Requests the runtime permissions Bluetooth needs: SCAN, CONNECT and ADVERTISE on Android 12+, fine location
    /// on Android 8–11 (for discovery). Calls back with whether everything was granted.
    /// </summary>
    public static void RequestPermissions(Action<bool> done)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var missing = MissingPermissions();
        if (missing.Length == 0) { done(true); return; }
        int answered = 0;
        bool all = true;
        var callbacks = new PermissionCallbacks();
        void Answer(bool granted) { all &= granted; if (++answered == missing.Length) done(all); }
        callbacks.PermissionGranted += _ => Answer(true);
        callbacks.PermissionDenied += _ => Answer(false);
        callbacks.PermissionDeniedAndDontAskAgain += _ => Answer(false);
        Permission.RequestUserPermissions(missing, callbacks);
#else
        done(false);
#endif
    }

    /// <summary>True once every permission Bluetooth needs has been granted.</summary>
    public static bool HasPermissions()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return MissingPermissions().Length == 0;
#else
        return false;
#endif
    }

    /// <summary>Opens this app's page in Android's settings, where a declined permission can be allowed after all.</summary>
    public static void OpenAppSettings()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
        using var uri = new AndroidJavaClass("android.net.Uri").CallStatic<AndroidJavaObject>("fromParts", "package", Application.identifier, null);
        using var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS", uri);
        activity.Call("startActivity", intent);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static string[] MissingPermissions()
    {
        int sdk;
        using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");
        var needed = sdk >= 31
            ? new[] { "android.permission.BLUETOOTH_SCAN", "android.permission.BLUETOOTH_CONNECT", "android.permission.BLUETOOTH_ADVERTISE" }
            : new[] { Permission.FineLocation };
        return Array.FindAll(needed, p => !Permission.HasUserAuthorizedPermission(p));
    }
#endif

    private sealed class Guest : IGuestTransport
    {
        private readonly BluetoothTransport _owner;
        public int Id { get; }
        public event Action<byte[]> Received;
        public event Action Disconnected;

        public Guest(BluetoothTransport owner, int id)
        {
            _owner = owner;
            Id = id;
        }

        public void Deliver(byte[] m) => Received?.Invoke(m);
        public void Closed() => Disconnected?.Invoke();
        public void Send(byte[] message) => _owner.Send(Id, message);
        public void Disconnect() => _owner.Disconnect(Id);
    }
}
