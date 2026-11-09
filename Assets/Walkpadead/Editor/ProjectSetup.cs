using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Walkpadead.Editor
{
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Walkpadead/Scenes/Prototype.unity";
        private const string ResourcePath = "Assets/Walkpadead/Resources/Walkpadead";
        private const string SettingsPath = "Assets/Walkpadead/Settings";

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            if (!File.Exists(ScenePath) || !File.Exists(SettingsPath + "/DesktopPipeline.asset"))
                EditorApplication.delayCall += Prepare;
        }

        [MenuItem("Walkpadead/Prepare project")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(ResourcePath);
            Directory.CreateDirectory(SettingsPath);
            Directory.CreateDirectory("Assets/Walkpadead/Scenes");
            AssetDatabase.Refresh();
            CreateMaterial("Asphalt", new Color(0.07f, 0.105f, 0.12f));
            CreateMaterial("Concrete", new Color(0.23f, 0.30f, 0.31f));
            CreateMaterial("Building", new Color(0.14f, 0.22f, 0.24f));
            CreateMaterial("Paint", new Color(0.6f, 0.62f, 0.41f));
            CreateMaterial("Window", new Color(0.09f, 0.24f, 0.29f));
            CreateMaterial("Metal", new Color(0.065f, 0.09f, 0.10f));
            CreateMaterial("Gun", new Color(0.29f, 0.34f, 0.35f));
            CreateMaterial("Coat", new Color(0.29f, 0.19f, 0.14f));
            CreateMaterial("Skin", new Color(0.45f, 0.56f, 0.36f));
            CreateMaterial("WarmLight", new Color(0.91f, 0.54f, 0.22f), true);
            CreateMaterial("Flash", new Color(1.0f, 0.82f, 0.38f), true);
            CreateMaterial("Eyes", new Color(0.98f, 0.29f, 0.12f), true);
            CreateMaterial("BonusUp", new Color(0.48f, 0.90f, 0.28f), true);
            CreateMaterial("BonusDown", new Color(0.98f, 0.55f, 0.20f), true);
            var pipelinePath = SettingsPath + "/DesktopPipeline.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, SettingsPath + "/DesktopRenderer.asset");
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.msaaSampleCount = 4;
                pipeline.shadowDistance = 50;
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            QualitySettings.vSyncCount = 1;
            PlayerSettings.companyName = "Walkpadead";
            PlayerSettings.productName = "Walkpadead";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "dev.walkpadead.prototype");
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
#if UNITY_EDITOR_OSX
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = OSArchitecture.ARM64;
#endif
            var serializedSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var inputHandler = serializedSettings.FindProperty("activeInputHandler");
            if (inputHandler != null) inputHandler.intValue = 1;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            if (!File.Exists(ScenePath))
            {
                // Unity cannot add a scene alongside the unsaved Untitled scene
                // present on a project's first launch. Preserve any user edits
                // before replacing it; use additive creation for saved scenes.
                var additive = true;
                for (var i = 0; i < SceneManager.sceneCount; i++)
                    if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)) additive = false;
                if (!additive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    additive ? NewSceneMode.Additive : NewSceneMode.Single);
                scene.name = "Prototype";
                EditorSceneManager.SaveScene(scene, ScenePath);
                if (additive) EditorSceneManager.CloseScene(scene, true);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Walkpadead project prepared. Open Prototype and press Play with the WalkingPad bridge running.");
        }

        [MenuItem("Walkpadead/Open prototype")]
        public static void OpenPrototype()
        {
            Prepare();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Walkpadead/Build macOS ARM64")]
        public static void BuildMac()
        {
            Prepare();
            Directory.CreateDirectory("Builds/macOS");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/macOS/Walkpadead.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Mac build failed: " + report.summary.result);
            Debug.Log("Walkpadead ARM64 build succeeded.");
        }

        private static void CreateMaterial(string name, Color color, bool emissive = false)
        {
            var path = ResourcePath + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.22f);
            material.enableInstancing = true;
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2);
            }
            AssetDatabase.CreateAsset(material, path);
        }
    }
}
