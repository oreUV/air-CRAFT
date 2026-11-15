using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Batch-mode Windows build. The project ships no scenes (GameBootstrap spawns itself via
/// RuntimeInitializeOnLoadMethod), so an empty startup scene is generated and registered
/// before the player is built.
/// </summary>
public static class BuildWindows
{
    [MenuItem("Build/Windows x64")]
    public static void Build()
    {
        const string scenePath = "Assets/Scenes/Main.unity";
        Directory.CreateDirectory("Assets/Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        AssetDatabase.SaveAssets();

        Directory.CreateDirectory("Builds");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = "Builds/SpaceMissionDesign.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });

        var summary = report.summary;
        Debug.Log($"BUILD RESULT: {summary.result}  errors={summary.totalErrors}  size={summary.totalSize} bytes");
        if (summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
