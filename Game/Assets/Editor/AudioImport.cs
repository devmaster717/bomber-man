using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for Resources/Audio: music streams from storage (Vorbis, about 100 kbps) so it never sits whole in
/// memory; effects load decompressed for instant playback and are stored mono.
/// </summary>
public sealed class AudioImport : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        if (!assetPath.Contains("/Resources/Audio/")) return;
        var importer = (AudioImporter)assetImporter;
        bool music = System.IO.Path.GetFileName(assetPath).StartsWith("Music_");
        importer.forceToMono = !music;
        importer.loadInBackground = music;
        importer.defaultSampleSettings = new AudioImporterSampleSettings
        {
            loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad,
            compressionFormat = AudioCompressionFormat.Vorbis,
            quality = music ? 0.4f : 0.7f,
            sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate,
        };
    }
}
