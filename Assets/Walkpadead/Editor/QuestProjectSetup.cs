using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace Walkpadead.Editor
{
    public static class QuestProjectSetup
    {
        public const string ScenePath = "Assets/Walkpadead/Scenes/QuestPrototype.unity";
        public const string ApplicationId = "dev.walkpadead.quest";

        [MenuItem("Walkpadead/Prepare Quest")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            ProjectSetup.Prepare();
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.Standalone })
            {
                var defines = PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                foreach (var define in new[] { "USE_INPUT_SYSTEM_POSE_CONTROL", "USE_STICK_CONTROL_THUMBSTICKS" })
                    if (!defines.Contains(define)) defines.Add(define);
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
            }
            const string path = "Assets/Walkpadead/Settings/XRGeneralSettings.asset";
            if (!EditorBuildSettings.TryGetConfigObject<XRGeneralSettingsPerBuildTarget>(XRGeneralSettings.k_SettingsKey, out var perTarget))
            {
                perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
                if (perTarget == null)
                {
                    perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(perTarget, path);
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android)) perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android)) perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            general.InitManagerOnStart = true;
            if (!XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android))
                throw new InvalidOperationException("Could not assign Android OpenXR loader.");
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            settings.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.None;
            settings.latencyOptimization = OpenXRSettings.LatencyOptimization.PrioritizeInputPolling;
            Enable<MetaQuestFeature>(settings);
            Enable<OculusTouchControllerProfile>(settings);
            Enable<MetaQuestTouchPlusControllerProfile>(settings);
            Enable<MetaQuestTouchProControllerProfile>(settings);
            var quest = settings.GetFeature<MetaQuestFeature>();
            quest.ForceRemoveInternetPermission = false;
            quest.AddTargetDevice("quest2", "Quest 2", true);
            quest.AddTargetDevice("cambria", "Quest Pro", true);
            quest.AddTargetDevice("eureka", "Quest 3", true);
            quest.AddTargetDevice("quest3s", "Quest 3S", true);
            var serializedQuest = new SerializedObject(quest);
            var devices = serializedQuest.FindProperty("targetDevices");
            for (var i = 0; i < devices.arraySize; i++)
            {
                var device = devices.GetArrayElementAtIndex(i);
                // Quest 1 cannot meet this Unity/OpenXR edition's Android API 32 minimum.
                device.FindPropertyRelative("enabled").boolValue = new[] { "quest2", "cambria", "eureka", "quest3s" }
                    .Contains(device.FindPropertyRelative("manifestName").stringValue);
            }
            serializedQuest.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(quest);
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(general); EditorUtility.SetDirty(general.Manager);
            EditorUtility.SetDirty(perTarget);

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.Android.blitType = AndroidBlitType.Never;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorSceneManager.CloseScene(scene, true);
            }
            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene(ProjectSetup.ScenePath, true), new EditorBuildSettingsScene(ScenePath, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log("Quest prepared: Android ARM64, OpenXR, Touch/Touch Plus/Touch Pro, gaze steering.");
        }

        private static void Enable<T>(OpenXRSettings settings) where T : OpenXRFeature
        {
            var feature = settings.GetFeature<T>();
            if (feature == null) throw new InvalidOperationException("Missing OpenXR feature " + typeof(T).Name);
            feature.enabled = true; EditorUtility.SetDirty(feature);
        }

        public static string Validate()
        {
            // Meta's rules inspect the selected group, even when Android is passed explicitly.
            if (EditorUserBuildSettings.selectedBuildTargetGroup != BuildTargetGroup.Android)
                return "Select the Android build target before running Quest validation.";
            var issues = new List<OpenXRFeature.ValidationRule>();
            UnityEditor.XR.OpenXR.OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
            return string.Join("\n", issues.ConvertAll(issue => (issue.error ? "ERROR: " : "NOTE: ") + issue.message));
        }

        [MenuItem("Walkpadead/Build Quest APK")]
        public static void Build()
        {
            Prepare();
            Directory.CreateDirectory("Builds/Quest");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, target = BuildTarget.Android,
                locationPathName = "Builds/Quest/Walkpadead.apk", options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Quest build failed: " + report.summary.result);
            Debug.Log("Quest APK built: Builds/Quest/Walkpadead.apk");
        }
    }
}
