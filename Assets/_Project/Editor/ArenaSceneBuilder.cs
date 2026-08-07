using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Overdraft.Core;
using Overdraft.Economy;
using Overdraft.Enemies;
using Overdraft.Player;
using Overdraft.UI;
using Overdraft.Utility;

namespace Overdraft.EditorTools
{
    /// <summary>
    /// Builds 02_Arena and wires every reference. Regenerating the scene is a menu click, which
    /// means a broken scene is never something to debug by hand — it is something to rebuild.
    /// </summary>
    public static class ArenaSceneBuilder
    {
        public static void Build(BuildContext ctx)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject arenaRoot = BuildArena(ctx);
            GameObject vaultRoot = BuildVault(ctx);
            GameObject cameraRigGO = BuildCamera(ctx);

            GameObject systems = new GameObject("--- SYSTEMS ---");
            ChargeSystem charge = systems.AddComponent<ChargeSystem>();
            MultiplierSystem multiplier = systems.AddComponent<MultiplierSystem>();
            ScoreSystem score = systems.AddComponent<ScoreSystem>();
            BankSystem bank = systems.AddComponent<BankSystem>();
            Hitstop hitstop = systems.AddComponent<Hitstop>();
            RunController run = systems.AddComponent<RunController>();

            GameObject playerGO = BuildPlayer(ctx, bank, multiplier, charge);
            PlayerController playerController = playerGO.GetComponent<PlayerController>();
            PlayerHealth health = playerGO.GetComponent<PlayerHealth>();
            DashModule dash = playerGO.GetComponent<DashModule>();
            Weapon weapon = playerGO.GetComponent<Weapon>();
            PlayerInputRouter input = playerGO.GetComponent<PlayerInputRouter>();

            GameObject enemyRoot = new GameObject("--- ENEMIES ---");
            EnemyManager enemyManager = enemyRoot.AddComponent<EnemyManager>();
            GameObject enemyPoolParent = EditorWiring.Child(enemyRoot, "Pool", Vector3.zero);

            GameObject projectilePool = new GameObject("--- PROJECTILES ---");

            GameObject environmentGO = new GameObject("--- ENVIRONMENT ---");
            EnvironmentController environment = environmentGO.AddComponent<EnvironmentController>();
            HazardBar[] hazardBars = BuildHazardBars(ctx, environmentGO.transform);

            UpgradeSystem upgrades = systems.AddComponent<UpgradeSystem>();

            GameObject bankRingGO = BuildBankRing(ctx);
            HudRefs hud = BuildHud(ctx);
            UpgradeChoiceUI upgradeUI = BuildUpgradeChoiceUI(ctx);

            // --- Wiring ---------------------------------------------------------------

            EditorWiring.Set(multiplier, "tuning", ctx.Tuning);
            EditorWiring.Set(hitstop, "tuning", ctx.Tuning);

            EditorWiring.Set(bank, "tuning", ctx.Tuning);
            EditorWiring.Set(bank, "charge", charge);
            EditorWiring.Set(bank, "multiplier", multiplier);
            EditorWiring.Set(bank, "score", score);

            EditorWiring.Set(enemyManager, "tuning", ctx.Tuning);
            EditorWiring.Set(enemyManager, "drifterPrefab", ctx.DrifterPrefab.GetComponent<DrifterAI>());
            EditorWiring.Set(enemyManager, "spitterPrefab", ctx.SpitterPrefab.GetComponent<SpitterAI>());
            EditorWiring.Set(enemyManager, "splitterPrefab", ctx.SplitterPrefab.GetComponent<SplitterAI>());
            EditorWiring.SetArray(enemyManager, "definitions",
                new Object[] { ctx.DrifterDefinition, ctx.SpitterDefinition, ctx.SplitterDefinition });
            EditorWiring.Set(enemyManager, "projectilePrefab", ctx.EnemyProjectilePrefab.GetComponent<EnemyProjectile>());
            EditorWiring.Set(enemyManager, "puddlePrefab", ctx.PuddlePrefab.GetComponent<Puddle>());
            EditorWiring.Set(enemyManager, "poolParent", enemyPoolParent.transform);

            EditorWiring.Set(weapon, "tuning", ctx.Tuning);
            EditorWiring.Set(weapon, "projectilePrefab", ctx.ProjectilePrefab.GetComponent<PlayerProjectile>());
            EditorWiring.Set(weapon, "poolParent", projectilePool.transform);
            EditorWiring.Set(weapon, "enemies", enemyManager);

            CameraRig rig = cameraRigGO.GetComponent<CameraRig>();
            EditorWiring.Set(rig, "tuning", ctx.Tuning);
            EditorWiring.Set(rig, "target", playerController);

            EditorWiring.Set(environment, "tuning", ctx.Tuning);
            EditorWiring.Set(environment, "floorRoot", arenaRoot);
            EditorWiring.Set(environment, "vaultRoot", vaultRoot);
            EditorWiring.SetArray(environment, "hazardBars", hazardBars);
            EditorWiring.Set(environment, "cameraRig", rig);
            EditorWiring.Set(environment, "player", playerController);
            EditorWiring.Set(environment, "weapon", weapon);

            EditorWiring.Set(upgrades, "tuning", ctx.Tuning);
            EditorWiring.Set(upgrades, "weapon", weapon);
            EditorWiring.Set(upgrades, "bank", bank);
            EditorWiring.Set(upgrades, "multiplier", multiplier);
            EditorWiring.Set(upgrades, "health", health);
            EditorWiring.Set(upgrades, "enemies", enemyManager);

            EditorWiring.Set(run, "tuning", ctx.Tuning);
            EditorWiring.Set(run, "input", input);
            EditorWiring.Set(run, "player", playerController);
            EditorWiring.Set(run, "health", health);
            EditorWiring.Set(run, "dash", dash);
            EditorWiring.Set(run, "weapon", weapon);
            EditorWiring.Set(run, "charge", charge);
            EditorWiring.Set(run, "multiplier", multiplier);
            EditorWiring.Set(run, "score", score);
            EditorWiring.Set(run, "bank", bank);
            EditorWiring.Set(run, "upgrades", upgrades);
            EditorWiring.Set(run, "enemies", enemyManager);
            EditorWiring.Set(run, "environment", environment);
            EditorWiring.Set(run, "upgradeUI", upgradeUI);

            ScreenShake shake = cameraRigGO.GetComponentInChildren<ScreenShake>();
            EditorWiring.Set(shake, "tuning", ctx.Tuning);

            BankRing ring = bankRingGO.GetComponent<BankRing>();
            EditorWiring.Set(ring, "followTarget", playerGO.transform);
            EditorWiring.Set(ring, "ringRenderer", bankRingGO.GetComponentInChildren<MeshRenderer>());

            EditorWiring.Set(hud.Controller, "tuning", ctx.Tuning);
            EditorWiring.Set(hud.Controller, "chargeNumber", hud.ChargeNumber);
            EditorWiring.Set(hud.Controller, "chargePulseRoot", hud.ChargePulseRoot);
            EditorWiring.Set(hud.Controller, "multiplierText", hud.MultiplierText);
            EditorWiring.Set(hud.Controller, "decayBarFill", hud.DecayBarFill);
            EditorWiring.Set(hud.Controller, "decayBarImage", hud.DecayBarImage);
            EditorWiring.Set(hud.Controller, "scoreNumber", hud.ScoreNumber);
            EditorWiring.Set(hud.Controller, "waveText", hud.WaveText);
            EditorWiring.Set(hud.Controller, "stateBanner", hud.StateBanner);
            EditorWiring.SetArray(hud.Controller, "hpPips", hud.HpPips);
            EditorWiring.SetArray(hud.Controller, "dashPips", hud.DashPips);

            // Keep the hierarchy readable — Day 3 involves a lot of staring at this scene.
            arenaRoot.transform.SetSiblingIndex(0);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ProjectBuilder.ArenaScenePath);
        }

        static GameObject BuildArena(BuildContext ctx)
        {
            GameObject root = new GameObject("--- ARENA ---");
            float radius = ctx.Tuning.Arena.floorRadius;

            GameObject floor = EditorWiring.Child(root, "Floor", Vector3.zero);
            floor.transform.localScale = new Vector3(radius, 1f, radius);
            AddMesh(floor, ctx.DiscMesh, ctx.GridFloorMaterial);

            // A thin emissive annulus rather than a wall mesh: additive on near-black reads as a
            // hard boundary and costs two triangles per segment.
            GameObject boundary = EditorWiring.Child(root, "BoundaryRing", new Vector3(0f, 0.01f, 0f));
            boundary.transform.localScale = new Vector3(radius, 1f, radius);
            AddMesh(boundary, ctx.BoundaryMesh, ctx.BoundaryMaterial);

            return root;
        }

        /// <summary>
        /// THE VAULT (FR-451, FR-457): a second arena root in the SAME scene, inactive until
        /// wave 4. Warm-toned floor, smaller radius, no boundary ring of its own — the hazard
        /// bars are the Vault's danger tell instead.
        /// </summary>
        static GameObject BuildVault(BuildContext ctx)
        {
            GameObject root = new GameObject("--- VAULT ---");
            float radius = ctx.Tuning.Arena.vaultRadius;

            GameObject floor = EditorWiring.Child(root, "Floor", Vector3.zero);
            floor.transform.localScale = new Vector3(radius, 1f, radius);
            AddMesh(floor, ctx.DiscMesh, ctx.VaultFloorMaterial);

            GameObject boundary = EditorWiring.Child(root, "BoundaryRing", new Vector3(0f, 0.01f, 0f));
            boundary.transform.localScale = new Vector3(radius, 1f, radius);
            AddMesh(boundary, ctx.BoundaryMesh, ctx.BoundaryMaterial);

            root.SetActive(false);
            return root;
        }

        static HazardBar[] BuildHazardBars(BuildContext ctx, Transform parent)
        {
            int count = ctx.Tuning.Arena.hazardBarCount;
            HazardBar[] bars = new HazardBar[count];
            float cycle = ctx.Tuning.Arena.hazardCycleDuration;

            for (int i = 0; i < count; i++)
            {
                GameObject barGO = EditorWiring.Child(parent.gameObject, "HazardBar" + i, Vector3.zero);
                HazardBar bar = barGO.AddComponent<HazardBar>();

                GameObject visual = EditorWiring.Child(barGO, "Visual", Vector3.zero);
                MeshRenderer renderer = AddMesh(visual, ctx.BarMesh, ctx.HazardMaterial);

                EditorWiring.Set(bar, "tuning", ctx.Tuning);
                EditorWiring.Set(bar, "visual", visual.transform);
                EditorWiring.Set(bar, "meshRenderer", renderer);

                // Evenly spaced phase offsets so the three bars don't all telegraph in sync —
                // "3 rotating... bars", not one bar shown three times.
                float phaseOffset = (cycle / count) * i;
                bar.Configure(phaseOffset);

                bars[i] = bar;
            }

            return bars;
        }

        static GameObject BuildCamera(BuildContext ctx)
        {
            GameObject rigGO = new GameObject("--- CAMERA RIG ---");
            rigGO.transform.position = new Vector3(0f, 30f, 0f);
            CameraRig rig = rigGO.AddComponent<CameraRig>();

            // Shake sits between the follow transform and the camera so its offset can never
            // feed back into the follow lerp (docs/04 §5).
            GameObject shakeGO = EditorWiring.Child(rigGO, "ShakeChild", Vector3.zero);
            shakeGO.AddComponent<ScreenShake>();

            GameObject camGO = EditorWiring.Child(shakeGO, "Main Camera", Vector3.zero);
            camGO.tag = "MainCamera";
            camGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Camera cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = ctx.Tuning.Arena.floorOrthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Void;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.allowMSAA = true;
            cam.allowHDR = true;

            EditorWiring.Set(rig, "cam", cam);
            return rigGO;
        }

        static GameObject BuildPlayer(BuildContext ctx, BankSystem bank, MultiplierSystem multiplier, ChargeSystem charge)
        {
            GameObject root = new GameObject("--- PLAYER ---");
            root.transform.position = Vector3.zero;

            PlayerController controller = root.AddComponent<PlayerController>();
            PlayerHealth health = root.AddComponent<PlayerHealth>();
            DashModule dash = root.AddComponent<DashModule>();
            Weapon weapon = root.AddComponent<Weapon>();
            PlayerInputRouter input = root.AddComponent<PlayerInputRouter>();

            GameObject visual = EditorWiring.Child(root, "Visual", Vector3.zero);
            visual.transform.localScale = Vector3.one * ctx.Tuning.Player.visualRadius;
            MeshRenderer renderer = AddMesh(visual, ctx.TriangleMesh, ctx.PlayerMaterial);

            EditorWiring.Set(controller, "tuning", ctx.Tuning);
            EditorWiring.Set(controller, "input", input);
            EditorWiring.Set(controller, "bank", bank);
            EditorWiring.Set(controller, "dash", dash);
            EditorWiring.Set(controller, "visual", visual.transform);

            EditorWiring.Set(health, "tuning", ctx.Tuning);
            EditorWiring.Set(health, "charge", charge);
            EditorWiring.Set(health, "multiplier", multiplier);
            EditorWiring.Set(health, "bank", bank);
            EditorWiring.Set(health, "dash", dash);
            EditorWiring.SetArray(health, "blinkRenderers", new Object[] { renderer });

            EditorWiring.Set(dash, "tuning", ctx.Tuning);
            EditorWiring.Set(dash, "bank", bank);
            EditorWiring.Set(dash, "multiplier", multiplier);

            EditorWiring.Set(weapon, "player", controller);
            EditorWiring.Set(weapon, "bank", bank);

            EditorWiring.Set(input, "actions", ctx.InputActions);

            return root;
        }

        static GameObject BuildBankRing(BuildContext ctx)
        {
            GameObject root = new GameObject("--- BANK RING ---");
            root.AddComponent<BankRing>();

            GameObject ring = EditorWiring.Child(root, "Ring", Vector3.zero);
            ring.transform.localScale = Vector3.one * BankRingWorldRadius;
            AddMesh(ring, ctx.BankRingMesh, ctx.BankRingMaterial);

            return root;
        }

        static HudRefs BuildHud(BuildContext ctx)
        {
            GameObject canvasGO = new GameObject("--- HUD ---", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            HUDController controller = canvasGO.AddComponent<HUDController>();

            HudRefs refs = new HudRefs { Controller = controller };

            // CHARGE — the largest element on screen, and the only gold thing (docs/04 §1).
            RectTransform chargeGroup = NewRect(canvasGO.transform, "ChargeGroup",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(1200f, 220f));
            refs.ChargePulseRoot = chargeGroup;

            TMP_Text chargeText = NewText(chargeGroup, "ChargeValue", "0", 128f, Palette.Bank, TextAlignmentOptions.Center);
            SetFull(chargeText.rectTransform);
            refs.ChargeNumber = chargeText.gameObject.AddComponent<FastNumberText>();
            EditorWiring.Set(refs.ChargeNumber, "text", chargeText);

            TMP_Text unbanked = NewText(canvasGO.transform, "UnbankedLabel", "U N B A N K E D", 26f,
                new Color(Palette.Bank.r, Palette.Bank.g, Palette.Bank.b, 0.55f), TextAlignmentOptions.Center);
            Place(unbanked.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -270f), new Vector2(900f, 40f));

            // MULT — top left, Ion.
            TMP_Text multText = NewText(canvasGO.transform, "MultValue", "x1.0", 52f, Palette.Ion, TextAlignmentOptions.Left);
            Place(multText.rectTransform, new Vector2(0f, 1f), new Vector2(60f, -60f), new Vector2(400f, 70f), new Vector2(0f, 1f));
            refs.MultiplierText = multText;

            RectTransform decayTrack = NewRect(canvasGO.transform, "DecayTrack",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -120f), new Vector2(260f, 10f));
            decayTrack.pivot = new Vector2(0f, 0.5f);

            RectTransform decayFill = NewRect(decayTrack, "DecayFill",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(260f, 10f));
            decayFill.pivot = new Vector2(0f, 0.5f);
            Image decayImage = decayFill.gameObject.AddComponent<Image>();
            decayImage.color = Palette.Ion;
            refs.DecayBarFill = decayFill;
            refs.DecayBarImage = decayImage;

            // SCORE — top right, white, deliberately quieter than CHARGE.
            TMP_Text scoreText = NewText(canvasGO.transform, "ScoreValue", "0", 44f, Palette.Banked, TextAlignmentOptions.Right);
            Place(scoreText.rectTransform, new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(500f, 70f), new Vector2(1f, 1f));
            refs.ScoreNumber = scoreText.gameObject.AddComponent<FastNumberText>();
            EditorWiring.Set(refs.ScoreNumber, "text", scoreText);

            // WAVE — below SCORE, quieter still (docs/04 §6 layout).
            TMP_Text waveText = NewText(canvasGO.transform, "WaveValue", "WAVE 1/6", 28f, Palette.Banked, TextAlignmentOptions.Right);
            Place(waveText.rectTransform, new Vector2(1f, 1f), new Vector2(-60f, -130f), new Vector2(500f, 40f), new Vector2(1f, 1f));
            refs.WaveText = waveText;

            // HP pips and dash charges — bottom left, Ion.
            refs.HpPips = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform pip = NewRect(canvasGO.transform, "HpPip" + i,
                    new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f + i * 42f, 100f), new Vector2(30f, 30f));
                Image image = pip.gameObject.AddComponent<Image>();
                image.color = Palette.Ion;
                refs.HpPips[i] = image;
            }

            refs.DashPips = new Object[2];
            for (int i = 0; i < 2; i++)
            {
                RectTransform pip = NewRect(canvasGO.transform, "DashPip" + i,
                    new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f + i * 42f, 60f), new Vector2(30f, 12f));
                Image image = pip.gameObject.AddComponent<Image>();
                image.color = Palette.Ion;
                refs.DashPips[i] = image;
            }

            TMP_Text banner = NewText(canvasGO.transform, "StateBanner", "", 56f, Palette.Warn, TextAlignmentOptions.Center);
            Place(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(1200f, 200f));
            banner.gameObject.SetActive(false);
            refs.StateBanner = banner;

            return refs;
        }

        /// <summary>
        /// The M2 upgrade choice screen (FR-471, M2-T09). A separate overlay canvas, on top of
        /// the HUD, with three side-by-side cards. Functional and ugly by design — see
        /// UpgradeChoiceUI's own doc comment.
        /// </summary>
        static UpgradeChoiceUI BuildUpgradeChoiceUI(BuildContext ctx)
        {
            GameObject canvasGO = new GameObject("--- UPGRADE CHOICE ---", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;   // above the HUD canvas

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject dim = EditorWiring.Child(canvasGO, "Dim", Vector3.zero);
            RectTransform dimRect = dim.AddComponent<RectTransform>();
            SetFull(dimRect);
            Image dimImage = dim.AddComponent<Image>();
            dimImage.color = new Color(Palette.Void.r, Palette.Void.g, Palette.Void.b, 0.80f);

            TMP_Text title = NewText(canvasGO.transform, "Title", "CHOOSE AN UPGRADE", 44f, Palette.Banked, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(1000f, 60f));

            const int cardCount = 3;
            const float cardWidth = 460f;
            const float cardHeight = 320f;
            const float gap = 40f;
            float totalWidth = cardCount * cardWidth + (cardCount - 1) * gap;
            float startX = -totalWidth * 0.5f + cardWidth * 0.5f;

            RectTransform[] cardRects = new RectTransform[cardCount];
            Object[] cardTexts = new Object[cardCount];
            Object[] cardHighlights = new Object[cardCount];

            for (int i = 0; i < cardCount; i++)
            {
                float x = startX + i * (cardWidth + gap);

                RectTransform card = NewRect(canvasGO.transform, "Card" + i,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(cardWidth, cardHeight));

                Image cardBg = card.gameObject.AddComponent<Image>();
                cardBg.color = new Color(Palette.Grid.r, Palette.Grid.g, Palette.Grid.b, 0.9f);

                RectTransform border = NewRect(card, "Highlight", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                SetFull(border);
                Image highlight = border.gameObject.AddComponent<Image>();
                highlight.color = Palette.Grid;
                highlight.raycastTarget = false;

                TMP_Text cardText = NewText(card, "Text", "", 28f, Palette.Banked, TextAlignmentOptions.Center);
                SetFull(cardText.rectTransform);
                cardText.enableWordWrapping = true;

                cardRects[i] = card;
                cardTexts[i] = cardText;
                cardHighlights[i] = highlight;
            }

            UpgradeChoiceUI ui = canvasGO.AddComponent<UpgradeChoiceUI>();
            EditorWiring.Set(ui, "tuning", ctx.Tuning);
            EditorWiring.Set(ui, "root", canvasGO);
            EditorWiring.Set(ui, "canvas", canvas);
            EditorWiring.SetArray(ui, "cardRects", cardRects);
            EditorWiring.SetArray(ui, "cardTexts", cardTexts);
            EditorWiring.SetArray(ui, "cardHighlights", cardHighlights);

            return ui;
        }

        static MeshRenderer AddMesh(GameObject go, Mesh mesh, Material material)
        {
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            // C2: no shadows anywhere, and no light probe or reflection probe sampling.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            return renderer;
        }

        static RectTransform NewRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static TMP_Text NewText(Transform parent, string name, string content, float size, Color color, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void SetFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>World radius of the bank ring. Sized to sit just outside the player silhouette.</summary>
        const float BankRingWorldRadius = 1.1f;

        public sealed class HudRefs
        {
            public HUDController Controller;
            public FastNumberText ChargeNumber;
            public RectTransform ChargePulseRoot;
            public TMP_Text MultiplierText;
            public RectTransform DecayBarFill;
            public Image DecayBarImage;
            public FastNumberText ScoreNumber;
            public TMP_Text WaveText;
            public TMP_Text StateBanner;
            public Object[] HpPips;
            public Object[] DashPips;
        }
    }
}
