using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports the planet and ground maps with settings the runtime relies on:
/// - EarthClouds is read on the CPU (white-on-black cloud map turned into an alpha
///   coverage mask), so it must be readable and uncompressed.
/// - The Earth/Moon maps are 2048x1024 equirectangular images wrapped around a
///   sphere, so they need mipmaps (to stop shimmering from orbit) and anisotropy.
/// - PadGround is 1536x1536 satellite imagery laid flat under the launch pad.
/// </summary>
public sealed class TextureImportSettings : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        var file = System.IO.Path.GetFileName(assetPath);

        if (file == "EarthClouds.jpg" || file == "EarthSurface.jpg" || file == "MoonSurface.jpg")
        {
            var importer = (TextureImporter)assetImporter;
            importer.isReadable = file == "EarthClouds.jpg";
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.wrapMode = TextureWrapMode.Repeat;     // wrap around the globe
            importer.maxTextureSize = 2048;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
            return;
        }

        if (file == "PadGround.jpg")
        {
            var importer = (TextureImporter)assetImporter;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;                        // seen at grazing angles
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
        }
    }
}
