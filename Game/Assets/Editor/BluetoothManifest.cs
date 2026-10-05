using System.IO;
using UnityEditor.Android;

/// <summary>
/// Adds the Bluetooth permissions to the generated Android manifest (spec section 11): the Android 12+ trio
/// (SCAN without location, CONNECT, ADVERTISE) and, for Android 8–11, the legacy BLUETOOTH/ADMIN and the fine
/// location that discovery needs there.
/// </summary>
public sealed class BluetoothManifest : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 0;

    private static readonly string[] Permissions =
    {
        "<uses-permission android:name=\"android.permission.BLUETOOTH\" android:maxSdkVersion=\"30\" />",
        "<uses-permission android:name=\"android.permission.BLUETOOTH_ADMIN\" android:maxSdkVersion=\"30\" />",
        "<uses-permission android:name=\"android.permission.ACCESS_FINE_LOCATION\" android:maxSdkVersion=\"30\" />",
        "<uses-permission android:name=\"android.permission.BLUETOOTH_SCAN\" android:usesPermissionFlags=\"neverForLocation\" tools:targetApi=\"s\" />",
        "<uses-permission android:name=\"android.permission.BLUETOOTH_CONNECT\" />",
        "<uses-permission android:name=\"android.permission.BLUETOOTH_ADVERTISE\" />",
        "<uses-feature android:name=\"android.hardware.bluetooth\" android:required=\"false\" />",
    };

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        var manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        var xml = File.ReadAllText(manifest);
        if (!xml.Contains("xmlns:tools="))
            xml = xml.Replace("<manifest ", "<manifest xmlns:tools=\"http://schemas.android.com/tools\" ");
        var insert = "";
        foreach (var p in Permissions)
        {
            var name = p.Substring(p.IndexOf("android:name=") + 14).Split('"')[0];
            if (!xml.Contains("\"" + name + "\"")) insert += "  " + p + "\n";
        }
        if (insert.Length > 0) xml = xml.Replace("</manifest>", insert + "</manifest>");
        File.WriteAllText(manifest, xml);
    }
}
