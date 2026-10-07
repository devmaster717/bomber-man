using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Writes errors and exceptions to errors.log in the app's data folder, so problems on players' phones can be
/// looked at later. The file is capped at 256 KB: when full it becomes errors.1.log and a new one starts.
/// </summary>
public static class ErrorLog
{
    private const long MaxBytes = 256 * 1024;
    private static readonly object Lock = new object();
    private static string _path;

    public static void Install()
    {
        if (_path != null) return;
        _path = Path.Combine(Application.persistentDataPath, "errors.log");
        Application.logMessageReceivedThreaded += OnLog;
    }

    private static void OnLog(string message, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        lock (Lock)
        {
            try
            {
                var info = new FileInfo(_path);
                if (info.Exists && info.Length > MaxBytes)
                {
                    var old = Path.Combine(info.DirectoryName ?? "", "errors.1.log");
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(_path, old);
                }
                File.AppendAllText(_path, $"{DateTime.UtcNow:u} {type}: {message}\n{stackTrace}\n");
            }
            catch
            {
                // Never let logging itself break the game.
            }
        }
    }
}
