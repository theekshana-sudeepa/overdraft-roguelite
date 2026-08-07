using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Overdraft.Core;
using Overdraft.Enemies;
using Overdraft.Player;

namespace Overdraft.EditorTools
{
    /// <summary>Everything the generators need to hand to each other.</summary>
    public sealed class BuildContext
    {
        public TuningConfig Tuning;
        public EnemyDefinition DrifterDefinition;
        public EnemyDefinition SpitterDefinition;
        public EnemyDefinition SplitterDefinition;
        public InputActionAsset InputActions;

        public Mesh TriangleMesh;
        public Mesh QuadMesh;
        public Mesh DiscMesh;
        public Mesh DiamondMesh;
        public Mesh HollowSquareMesh;
        public Mesh BankRingMesh;
        public Mesh BoundaryMesh;
        public Mesh BarMesh;

        public Material PlayerMaterial;
        public Material EnemyMaterial;
        public Material ProjectileMaterial;
        public Material EnemyProjectileMaterial;
        public Material PuddleMaterial;
        public Material GridFloorMaterial;
        public Material VaultFloorMaterial;
        public Material BoundaryMaterial;
        public Material BankRingMaterial;
        public Material HazardMaterial;

        public GameObject DrifterPrefab;
        public GameObject SpitterPrefab;
        public GameObject SplitterPrefab;
        public GameObject ProjectilePrefab;
        public GameObject EnemyProjectilePrefab;
        public GameObject PuddlePrefab;
    }

    /// <summary>
    /// One command that generates the entire project: settings, meshes, materials, data assets,
    /// input actions, prefabs and all three scenes, fully wired.
    /// <para>
    /// It is idempotent — run it again after any change and the scenes are rebuilt from source.
    /// On a 21-day schedule, "regenerate" beats "debug a broken scene reference" every time.
    /// </para>
    /// </summary>
    public static class ProjectBuilder
    {
        public const string ScenesRoot = "Assets/_Project/Scenes";
        public const string PrefabsRoot = "Assets/_Project/Prefabs";
        public const string MeshesRoot = "Assets/_Project/Art/Meshes";

        public const string BootScenePath = ScenesRoot + "/00_Boot.unity";
        public const string MenuScenePath = ScenesRoot + "/01_Menu.unity";
        public const string ArenaScenePath = ScenesRoot + "/02_Arena.unity";

        [MenuItem("Overdraft/Build Project (M0 + M1)", false, 0)]
        public static void BuildAll()
        {
            if (!CheckTextMeshProResources()) { return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }

            try
            {
                EditorUtility.DisplayProgressBar("OVERDRAFT", "Applying project settings…", 0.05f);
                ProjectConfigurator.ApplyAll();

                EditorUtility.DisplayProgressBar("OVERDRAFT", "Creating data assets…", 0.25f);
                BuildContext ctx = new BuildContext
                {
                    Tuning = DataAssetFactory.CreateTuningConfig(),
                    DrifterDefinition = DataAssetFactory.CreateDrifterDefinition(),
                    SpitterDefinition = DataAssetFactory.CreateSpitterDefinition(),
                    SplitterDefinition = DataAssetFactory.CreateSplitterDefinition()
                };

                EditorUtility.DisplayProgressBar("OVERDRAFT", "Generating meshes…", 0.40f);
                CreateMeshes(ctx);

                EditorUtility.DisplayProgressBar("OVERDRAFT", "Creating materials…", 0.50f);
                CreateMaterials(ctx);

                EditorUtility.DisplayProgressBar("OVERDRAFT", "Generating input actions…", 0.60f);
                ctx.InputActions = DataAssetFactory.CreateInputActions();

                EditorUtility.DisplayProgressBar("OVERDRAFT", "Building prefabs…", 0.70f);
                CreatePrefabs(ctx);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayProgressBar("OVERDRAFT", "Building scenes…", 0.85f);
                ArenaSceneBuilder.Build(ctx);
                BuildMenuScene();
                BuildBootScene(ctx);

                RegisterScenes();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            Debug.Log(
                "[Overdraft] Build complete. Press Play from 00_Boot.\n" +
                "WASD/left stick to move, mouse/right stick to aim, auto-fire, " +
                "Shift or RMB or RB to dash, HOLD Space or LMB or A to bank.");
        }

        /// <summary>Batch entry point: Unity -batchmode -executeMethod Overdraft.EditorTools.ProjectBuilder.BuildAllBatch</summary>
        public static void BuildAllBatch()
        {
            BuildAll();
        }

        [MenuItem("Overdraft/Rebuild Scenes Only", false, 1)]
        public static void RebuildScenesOnly()
        {
            if (!CheckTextMeshProResources()) { return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }

            BuildContext ctx = LoadExistingContext();
            if (ctx == null) { return; }

            ArenaSceneBuilder.Build(ctx);
            BuildMenuScene();
            BuildBootScene(ctx);
            RegisterScenes();

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Debug.Log("[Overdraft] Scenes rebuilt.");
        }

        static bool CheckTextMeshProResources()
        {
            if (TMP_Settings.defaultFontAsset != null) { return true; }

            EditorUtility.DisplayDialog(
                "TextMeshPro resources missing",
                "OVERDRAFT's HUD is TextMeshPro.\n\n" +
                "Import them first:\n" +
                "Window > TextMeshPro > Import TMP Essential Resources\n\n" +
                "Then run Overdraft > Build Project again.",
                "OK");

            Debug.LogError("[Overdraft] TMP Essential Resources not imported — build aborted.");
            return false;
        }

        static void CreateMeshes(BuildContext ctx)
        {
            Directory.CreateDirectory(MeshesRoot);

            ctx.TriangleMesh = SaveMesh(PrimitiveMeshFactory.Triangle(), "M_Triangle");
            ctx.QuadMesh = SaveMesh(PrimitiveMeshFactory.Quad(), "M_Quad");
            ctx.DiscMesh = SaveMesh(PrimitiveMeshFactory.Disc(), "M_Disc");

            // Bank ring: a thick annulus so the fill sweep is legible at a glance.
            ctx.BankRingMesh = SaveMesh(PrimitiveMeshFactory.Annulus(0.82f), "M_BankRing");

            // Boundary: a thin annulus at the arena edge.
            ctx.BoundaryMesh = SaveMesh(PrimitiveMeshFactory.Annulus(0.985f), "M_BoundaryRing");

            ctx.DiamondMesh = SaveMesh(PrimitiveMeshFactory.Diamond(), "M_Diamond");             // Splitter
            ctx.HollowSquareMesh = SaveMesh(PrimitiveMeshFactory.HollowSquare(), "M_HollowSquare"); // Spitter
            ctx.BarMesh = SaveMesh(PrimitiveMeshFactory.Bar(), "M_Bar");                         // Vault hazard bars
        }

        static Mesh SaveMesh(Mesh mesh, string assetName)
        {
            string path = MeshesRoot + "/" + assetName + ".asset";
            DataAssetFactory.SaveAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        static void CreateMaterials(BuildContext ctx)
        {
            ctx.PlayerMaterial = DataAssetFactory.CreateMaterial("Overdraft/ActorUnlit", "M_Player", Palette.Ion, 2.2f);
            ctx.EnemyMaterial = DataAssetFactory.CreateMaterial("Overdraft/ActorUnlit", "M_Enemy", Palette.Threat, 1.6f);
            ctx.ProjectileMaterial = DataAssetFactory.CreateMaterial("Overdraft/Additive", "M_Projectile_Player", Palette.Ion, 3.0f);
            ctx.EnemyProjectileMaterial = DataAssetFactory.CreateMaterial("Overdraft/Additive", "M_Projectile_Enemy", Palette.Threat, 3.0f);
            ctx.PuddleMaterial = DataAssetFactory.CreateMaterial("Overdraft/Additive", "M_Puddle", Palette.Threat, 1.0f);
            ctx.BoundaryMaterial = DataAssetFactory.CreateMaterial("Overdraft/Additive", "M_Wall_Emissive", Palette.Ion, 1.2f);
            ctx.BankRingMaterial = DataAssetFactory.CreateMaterial("Overdraft/RadialFill", "M_BankRing", Palette.Bank, 3.0f);
            ctx.HazardMaterial = DataAssetFactory.CreateMaterial("Overdraft/Additive", "M_Hazard_Laser", Palette.Hazard, 2.5f);

            ctx.GridFloorMaterial = DataAssetFactory.CreateMaterial("Overdraft/GridFloor", "M_Grid_Floor", Palette.Void, 1f);
            if (ctx.GridFloorMaterial == null) { return; }

            ctx.GridFloorMaterial.SetColor(ShaderIds.GridColor, Palette.Grid);
            ctx.GridFloorMaterial.SetFloat(ShaderIds.GridSpacing, ctx.Tuning.Sim.gridCellSize);
            ctx.GridFloorMaterial.SetFloat(ShaderIds.GridLineWidth, 0.02f);
            ctx.GridFloorMaterial.SetFloat(ShaderIds.Radius, ctx.Tuning.Arena.floorRadius);
            EditorUtility.SetDirty(ctx.GridFloorMaterial);

            ctx.VaultFloorMaterial = DataAssetFactory.CreateMaterial("Overdraft/GridFloor", "M_Grid_Vault", Palette.VaultVoid, 1f);
            if (ctx.VaultFloorMaterial == null) { return; }

            ctx.VaultFloorMaterial.SetColor(ShaderIds.GridColor, Palette.VaultGrid);
            ctx.VaultFloorMaterial.SetFloat(ShaderIds.GridSpacing, ctx.Tuning.Sim.gridCellSize);
            ctx.VaultFloorMaterial.SetFloat(ShaderIds.GridLineWidth, 0.02f);
            ctx.VaultFloorMaterial.SetFloat(ShaderIds.Radius, ctx.Tuning.Arena.vaultRadius);
            EditorUtility.SetDirty(ctx.VaultFloorMaterial);
        }

        static void CreatePrefabs(BuildContext ctx)
        {
            Directory.CreateDirectory(PrefabsRoot);

            // --- Drifter ---
            GameObject drifter = new GameObject("P_Drifter");
            DrifterAI drifterAI = drifter.AddComponent<DrifterAI>();

            GameObject drifterVisual = EditorWiring.Child(drifter, "Visual", Vector3.zero);
            drifterVisual.transform.localScale = Vector3.one * 0.45f;
            MeshRenderer drifterRenderer = AddPrimitiveRenderer(drifterVisual, ctx.TriangleMesh, ctx.EnemyMaterial);

            EditorWiring.Set(drifterAI, "visual", drifterVisual.transform);
            EditorWiring.Set(drifterAI, "meshRenderer", drifterRenderer);

            ctx.DrifterPrefab = PrefabUtility.SaveAsPrefabAsset(drifter, PrefabsRoot + "/P_Drifter.prefab");
            Object.DestroyImmediate(drifter);

            // --- Spitter ---
            GameObject spitter = new GameObject("P_Spitter");
            SpitterAI spitterAI = spitter.AddComponent<SpitterAI>();

            GameObject spitterVisual = EditorWiring.Child(spitter, "Visual", Vector3.zero);
            spitterVisual.transform.localScale = Vector3.one * 0.45f;
            MeshRenderer spitterRenderer = AddPrimitiveRenderer(spitterVisual, ctx.HollowSquareMesh, ctx.EnemyMaterial);

            EditorWiring.Set(spitterAI, "visual", spitterVisual.transform);
            EditorWiring.Set(spitterAI, "meshRenderer", spitterRenderer);

            ctx.SpitterPrefab = PrefabUtility.SaveAsPrefabAsset(spitter, PrefabsRoot + "/P_Spitter.prefab");
            Object.DestroyImmediate(spitter);

            // --- Splitter ---
            GameObject splitter = new GameObject("P_Splitter");
            SplitterAI splitterAI = splitter.AddComponent<SplitterAI>();

            GameObject splitterVisual = EditorWiring.Child(splitter, "Visual", Vector3.zero);
            splitterVisual.transform.localScale = Vector3.one * 0.40f;
            MeshRenderer splitterRenderer = AddPrimitiveRenderer(splitterVisual, ctx.DiamondMesh, ctx.EnemyMaterial);

            EditorWiring.Set(splitterAI, "visual", splitterVisual.transform);
            EditorWiring.Set(splitterAI, "meshRenderer", splitterRenderer);

            ctx.SplitterPrefab = PrefabUtility.SaveAsPrefabAsset(splitter, PrefabsRoot + "/P_Splitter.prefab");
            Object.DestroyImmediate(splitter);

            // --- Player projectile ---
            GameObject projectile = new GameObject("P_PlayerProjectile");
            projectile.AddComponent<PlayerProjectile>();

            GameObject projectileVisual = EditorWiring.Child(projectile, "Visual", Vector3.zero);
            projectileVisual.transform.localScale = new Vector3(0.16f, 1f, 0.55f);
            AddPrimitiveRenderer(projectileVisual, ctx.QuadMesh, ctx.ProjectileMaterial);

            ctx.ProjectilePrefab = PrefabUtility.SaveAsPrefabAsset(projectile, PrefabsRoot + "/P_PlayerProjectile.prefab");
            Object.DestroyImmediate(projectile);

            // --- Enemy projectile ---
            GameObject enemyProjectile = new GameObject("P_EnemyProjectile");
            enemyProjectile.AddComponent<EnemyProjectile>();

            GameObject enemyProjectileVisual = EditorWiring.Child(enemyProjectile, "Visual", Vector3.zero);
            enemyProjectileVisual.transform.localScale = new Vector3(0.19f, 1f, 0.66f);
            AddPrimitiveRenderer(enemyProjectileVisual, ctx.QuadMesh, ctx.EnemyProjectileMaterial);

            ctx.EnemyProjectilePrefab = PrefabUtility.SaveAsPrefabAsset(enemyProjectile, PrefabsRoot + "/P_EnemyProjectile.prefab");
            Object.DestroyImmediate(enemyProjectile);

            // --- Puddle ---
            GameObject puddle = new GameObject("P_Puddle");
            Puddle puddleComponent = puddle.AddComponent<Puddle>();

            GameObject puddleVisual = EditorWiring.Child(puddle, "Visual", Vector3.zero);
            AddPrimitiveRenderer(puddleVisual, ctx.DiscMesh, ctx.PuddleMaterial);

            EditorWiring.Set(puddleComponent, "visual", puddleVisual.transform);

            ctx.PuddlePrefab = PrefabUtility.SaveAsPrefabAsset(puddle, PrefabsRoot + "/P_Puddle.prefab");
            Object.DestroyImmediate(puddle);
        }

        static MeshRenderer AddPrimitiveRenderer(GameObject go, Mesh mesh, Material material)
        {
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return renderer;
        }

        static void BuildBootScene(BuildContext ctx)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject bootGO = new GameObject("Bootstrap");
            Bootstrap bootstrap = bootGO.AddComponent<Bootstrap>();
            EditorWiring.Set(bootstrap, "tuning", ctx.Tuning);
            EditorWiring.SetString(bootstrap, "firstSceneName", "02_Arena");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, BootScenePath);
        }

        static void BuildMenuScene()
        {
            // Placeholder so the scene layout matches docs/01 §8 from day one. Menus are M3;
            // Bootstrap loads 02_Arena directly until then.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("MENU_PLACEHOLDER_M3");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        static void RegisterScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(ArenaScenePath, true)
            };
        }

        static BuildContext LoadExistingContext()
        {
            BuildContext ctx = new BuildContext
            {
                Tuning = AssetDatabase.LoadAssetAtPath<TuningConfig>(DataAssetFactory.DataRoot + "/TuningConfig.asset"),
                DrifterDefinition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DataAssetFactory.DataRoot + "/Enemies/Drifter.asset"),
                SpitterDefinition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DataAssetFactory.DataRoot + "/Enemies/Spitter.asset"),
                SplitterDefinition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DataAssetFactory.DataRoot + "/Enemies/Splitter.asset"),
                InputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DataAssetFactory.InputRoot + "/OverdraftControls.inputactions"),

                TriangleMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshesRoot + "/M_Triangle.asset"),
                QuadMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshesRoot + "/M_Quad.asset"),
                DiscMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshesRoot + "/M_Disc.asset"),
                DiamondMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshesRoot + "/M_Diamond.asset"),
                HollowSquareMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshesRoot + "/M_HollowSquare.asset"),
                BankRingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshesRoot + "/M_BankRing.asset"),
                BoundaryMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshesRoot + "/M_BoundaryRing.asset"),
                BarMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshesRoot + "/M_Bar.asset"),

                PlayerMaterial = LoadMaterial("M_Player"),
                EnemyMaterial = LoadMaterial("M_Enemy"),
                ProjectileMaterial = LoadMaterial("M_Projectile_Player"),
                EnemyProjectileMaterial = LoadMaterial("M_Projectile_Enemy"),
                PuddleMaterial = LoadMaterial("M_Puddle"),
                GridFloorMaterial = LoadMaterial("M_Grid_Floor"),
                VaultFloorMaterial = LoadMaterial("M_Grid_Vault"),
                BoundaryMaterial = LoadMaterial("M_Wall_Emissive"),
                BankRingMaterial = LoadMaterial("M_BankRing"),
                HazardMaterial = LoadMaterial("M_Hazard_Laser"),

                DrifterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsRoot + "/P_Drifter.prefab"),
                SpitterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsRoot + "/P_Spitter.prefab"),
                SplitterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsRoot + "/P_Splitter.prefab"),
                ProjectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsRoot + "/P_PlayerProjectile.prefab"),
                EnemyProjectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsRoot + "/P_EnemyProjectile.prefab"),
                PuddlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsRoot + "/P_Puddle.prefab")
            };

            if (ctx.Tuning != null && ctx.DrifterPrefab != null && ctx.InputActions != null) { return ctx; }

            Debug.LogError("[Overdraft] Missing generated assets. Run Overdraft > Build Project (M0 + M1) first.");
            return null;
        }

        static Material LoadMaterial(string assetName)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(DataAssetFactory.ArtRoot + "/Materials/" + assetName + ".mat");
        }
    }
}
