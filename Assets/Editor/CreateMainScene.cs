using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpaceMissionDesign.UI;

namespace SpaceMissionDesign.Editor
{
    /// <summary>
    /// One-time setup: creates the main scene with GameBootstrap and registers it in build settings.
    /// Run via menu: Space Mission Design > Create Main Scene
    /// </summary>
    public static class CreateMainScene
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Space Mission Design/Create Main Scene")]
        public static void DoIt()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Game Bootstrap");
            go.AddComponent<GameBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            var buildScenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.scenes = buildScenes;
            Debug.Log("Main scene created at " + ScenePath + " and added to build settings. Press Play.");
        }
    }
}
