using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures the Earth (A) and Earth (B) map textures are marked Read/Write Enabled
/// so runtime code can call GetPixels32() on them for the cloud layer.
/// </summary>
public sealed class TextureImportSettings : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        var path = assetPath;
        if (path.EndsWith("Earth (A).jpg") || path.EndsWith("Earth (B).jpg"))
        {
            var importer = (TextureImporter)assetImporter;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
