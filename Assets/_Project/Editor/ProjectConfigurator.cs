using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Overdraft.EditorTools
{
    /// <summary>
    /// Applies M0-T01: colour space, URP pipeline, timestep, quality, serialisation mode.
    /// <para>
    /// Doing this in code rather than by clicking through Project Settings means the
    /// configuration is reviewable in a diff and reproducible on a fresh clone — which matters
    /// on a project where a wrong colour space is discovered three days later by eye.
    /// </para>
    /// </summary>
    public static class ProjectConfigurator
    {
        public static void ApplyAll()
        {
            ApplyEditorSettings();
            ApplyPlayerSettings();
            ApplyRenderPipeline();
            ApplyTimeSettings();

            AssetDatabase.SaveAssets();
            Debug.Log("[Overdraft] Project settings applied.");
        }

        static void ApplyEditorSettings()
        {
            // Non-negotiable per CLAUDE.md §10: without these, merges become impossible.
            EditorSettings.serializationMode = SerializationMode.ForceText;

            try
            {
                VersionControlSettings.mode = "Visible Meta Files";
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Overdraft] Could not set visible meta files automatically: {e.Message}");
            }
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Thikka";
            PlayerSettings.productName = "OVERDRAFT";
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = false;
            PlayerSettings.visibleInBackground = false;

            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard);

            // Input System only (C6) — leaving the legacy manager enabled is how "why does my
            // gamepad double-fire" bugs happen. This is a ONE-TIME MANUAL STEP:
            // Edit > Project Settings > Player > Other Settings > Active Input Handling ->
            // "Input System Package (New)". Unity will prompt to restart; accept it.
            //
            // PlayerSettings.SetPropertyInt("activeInputHandler", ...) looks like the automatable
            // path but its property name is undocumented and not recognised on every 2022.3
            // patch (it logs "Unknown Property" as a native error that a C# try/catch cannot
            // intercept, rather than throwing). Given a restart is required either way, doing
            // this by hand is more reliable than a silently-broken automation.
            if (PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup.Standalone) != ApiCompatibilityLevel.NET_Standard)
            {
                Debug.LogWarning("[Overdraft] .NET Standard 2.1 API compatibility did not apply — check manually.");
            }

            Debug.Log(
                "[Overdraft] REMINDER: set Project Settings > Player > Active Input Handling to " +
                "'Input System Package (New)' if you have not already, then restart the editor when prompted.");
        }

        static void ApplyRenderPipeline()
        {
            const string assetPath = DataAssetFactory.ArtRoot + "/Rendering/URP_Overdraft.asset";
            const string rendererPath = DataAssetFactory.ArtRoot + "/Rendering/URP_Overdraft_Renderer.asset";

            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);

            if (pipeline == null)
            {
                UniversalRendererData rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                rendererData.name = "URP_Overdraft_Renderer";
                DataAssetFactory.SaveAsset(rendererData, rendererPath);

                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                pipeline.name = "URP_Overdraft";
                DataAssetFactory.SaveAsset(pipeline, assetPath);
            }

            // C2: unlit only. No realtime lights, no shadows, no lightmaps, no probes.
            TrySetBool(pipeline, "m_MainLightShadowsSupported", false);
            TrySetBool(pipeline, "m_AdditionalLightShadowsSupported", false);
            TrySetBool(pipeline, "m_SoftShadowsSupported", false);
            TrySetInt(pipeline, "m_AdditionalLightsRenderingMode", 0);   // Disabled

            pipeline.supportsHDR = true;      // bloom on a near-black scene needs HDR
            pipeline.msaaSampleCount = 4;
            pipeline.renderScale = 1.0f;      // capped at 1.0 even on Retina (docs/04 §3)
            pipeline.shadowDistance = 0f;

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;

            EditorUtility.SetDirty(pipeline);
        }

        static void ApplyTimeSettings()
        {
            // Bootstrap sets these at runtime too — that is the authoritative path. Writing the
            // asset as well just means the editor matches a build before Bootstrap runs.
            try
            {
                SerializedObject timeManager = new SerializedObject(
                    Unsupported.GetSerializedAssetInterfaceSingleton("TimeManager"));

                SetFloatIfPresent(timeManager, "Fixed Timestep", 1f / 60f);
                SetFloatIfPresent(timeManager, "Maximum Allowed Timestep", 0.1f);
                timeManager.ApplyModifiedPropertiesWithoutUndo();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(
                    "[Overdraft] Could not write TimeManager directly: " + e.Message +
                    "  Set Fixed Timestep to 0.01666667 and Maximum Allowed Timestep to 0.1 " +
                    "in Project Settings > Time. Bootstrap applies both at runtime regardless.");
            }
        }

        /// <summary>
        /// IL2CPP is separate because it needs the platform module installed and a missing
        /// module produces a confusing editor error in the middle of setup.
        /// </summary>
        [MenuItem("Overdraft/Setup/Apply Shipping Build Targets (IL2CPP)", false, 200)]
        public static void ApplyShippingBuildTargets()
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.stripEngineCode = true;

            // macOS Universal (Intel + Apple Silicon) is set in Build Settings > Architecture.
            // It lives behind the OSXStandalone editor module rather than in PlayerSettings,
            // so it stays a manual step until the M5 build script (M5-T09).

            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneOSX, new[] { GraphicsDeviceType.Metal });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX, false);

            Debug.Log("[Overdraft] Shipping build targets applied (IL2CPP, DX11 only, Metal only).");
        }

        static void SetFloatIfPresent(SerializedObject so, string propertyName, float value)
        {
            SerializedProperty p = so.FindProperty(propertyName);
            if (p != null) { p.floatValue = value; }
        }

        static void TrySetBool(Object target, string propertyName, bool value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(propertyName);
            if (p == null) { return; }
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void TrySetInt(Object target, string propertyName, int value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(propertyName);
            if (p == null) { return; }
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
