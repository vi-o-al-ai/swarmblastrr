using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Swarmblastrr.Core;
using Swarmblastrr.Meta;

namespace Swarmblastrr.Editor
{
    /// <summary>
    /// Editor menu items for the scaffold. "Swarmblastrr/Create Demo Scene" regenerates
    /// <c>Assets/Scenes/Main.unity</c> from code (an empty scene plus one <see cref="GameBootstrap"/>
    /// GameObject, which builds everything else at runtime) and registers it in Build Settings.
    /// "Swarmblastrr/Reset Save Data" wipes coins, level progress and upgrades through
    /// <see cref="SaveSystem.Reset"/>.
    /// </summary>
    public static class DemoSceneBuilder
    {
        /// <summary>Project-relative path of the generated demo scene.</summary>
        public const string ScenePath = "Assets/Scenes/Main.unity";

        private const string SceneFolderParent = "Assets";
        private const string SceneFolderName = "Scenes";
        private const string BootstrapObjectName = "Bootstrap";
        private const string LogPrefix = "[Swarmblastrr] ";

        /// <summary>
        /// Creates a new empty scene containing a single "Bootstrap" GameObject with a
        /// <see cref="GameBootstrap"/> component, saves it to <see cref="ScenePath"/> (creating the folder
        /// if needed), adds it to Build Settings if missing and leaves it open as the active scene.
        /// </summary>
        [MenuItem("Swarmblastrr/Create Demo Scene", false, 0)]
        public static void CreateDemoScene()
        {
            // NewScene(Single) would silently discard unsaved work in the currently open scenes.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(LogPrefix + "Create Demo Scene cancelled; the open scene was left untouched.");
                return;
            }

            EnsureSceneFolder();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject bootstrap = new GameObject(BootstrapObjectName);
            bootstrap.AddComponent<GameBootstrap>();
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError(LogPrefix + "Failed to save the demo scene to " + ScenePath + ".");
                return;
            }

            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            // SaveScene leaves the scene open at ScenePath; only re-open if something unexpected happened.
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            GameObject created = GameObject.Find(BootstrapObjectName);
            if (created != null)
            {
                Selection.activeGameObject = created;
            }

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset != null)
            {
                EditorGUIUtility.PingObject(sceneAsset);
            }

            Debug.Log(LogPrefix + "Demo scene saved to " + ScenePath + " and added to Build Settings. Press Play.");
        }

        [MenuItem("Swarmblastrr/Create Demo Scene", true)]
        private static bool ValidateCreateDemoScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>
        /// Deletes the persistent save file (coins, level index, upgrade levels) via
        /// <see cref="SaveSystem.Reset"/>. Asks for confirmation unless running in batch mode.
        /// </summary>
        [MenuItem("Swarmblastrr/Reset Save Data", false, 100)]
        public static void ResetSaveData()
        {
            if (!Application.isBatchMode)
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Reset Swarmblastrr save data?",
                    "This deletes coins, level progress and upgrades stored at:\n\n" + FileSaveStore.DefaultPath +
                    "\n\nIf the game is running, the change applies the next time it starts.",
                    "Reset",
                    "Cancel");
                if (!confirmed)
                {
                    return;
                }
            }

            SaveSystem.Reset();
            Debug.Log(LogPrefix + "Save data reset (" + FileSaveStore.DefaultPath + ").");
        }

        private static void EnsureSceneFolder()
        {
            string folder = SceneFolderParent + "/" + SceneFolderName;
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string guid = AssetDatabase.CreateFolder(SceneFolderParent, SceneFolderName);
                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogWarning(LogPrefix + "Could not create folder " + folder + "; SaveScene will try anyway.");
                }
            }
        }

        private static void AddToBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i] == null)
                {
                    continue;
                }

                if (string.Equals(scenes[i].path, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    if (!scenes[i].enabled)
                    {
                        scenes[i].enabled = true;
                        EditorBuildSettings.scenes = scenes;
                    }

                    return;
                }
            }

            List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(scenes.Length + 1);
            list.AddRange(scenes);
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
