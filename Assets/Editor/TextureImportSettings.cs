using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports the planet maps with settings the runtime relies on:
/// - EarthClouds is read on the CPU (white-on-black cloud map turned into an alpha
///   coverage mask), so it must be readable and uncompressed.
/// - All three are 2048x1024 equirectangular maps tiled around a sphere, so they need
///   mipmaps (to stop shimmering from orbit) and anisotropic filtering.
/// </summary>
public sealed class TextureImportSettings : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        var path = assetPath;
        var file = System.IO.Path.GetFileName(path);

        bool isCloudMap = file == "EarthClouds.jpg";
        bool isPlanetMap = isCloudMap || file == "EarthSurface.jpg" || file == "MoonSurface.jpg";
        if (!isPlanetMap) return;

        var importer = (TextureImporter)assetImporter;

        // The surface map is CPU-read to cut the launch-site ground out of the real
        // coast; the cloud map is CPU-read to turn its luminance into coverage.
        importer.isReadable = isCloudMap || file == "EarthSurface.jpg";
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.wrapMode = TextureWrapMode.Repeat;     // wrap around the globe
        importer.maxTextureSize = 2048;
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.None;

        importer.SaveAndReimport();
    }
}
