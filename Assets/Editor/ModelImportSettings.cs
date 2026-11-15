using UnityEditor;

/// <summary>
/// Ensures model files under Assets/Resources/Models are imported with
/// Read/Write Enabled so runtime code can sample mesh vertices
/// (Mission3DView.WideEndIsUp orientation heuristic).
/// </summary>
public sealed class ModelImportSettings : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        string path = assetPath.Replace('\\', '/').ToLowerInvariant();
        if (path.EndsWith(".fbx") && path.Contains("assets/resources/models"))
        {
            var importer = (ModelImporter)assetImporter;
            if (!importer.isReadable) importer.isReadable = true;
            // Uncompressed mesh data keeps vertex access cheap at runtime.
            importer.optimizeMeshVertices = false;
        }
    }
}
