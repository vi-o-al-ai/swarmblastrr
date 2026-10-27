using System;
using UnityEngine;
using Swarmblastrr.Controls;
using Swarmblastrr.Crowd;
using Swarmblastrr.Level;
using Swarmblastrr.Meta;
using Swarmblastrr.UI;

namespace Swarmblastrr.Core
{
    /// <summary>
    /// Composition root and the only component a scene needs. In Awake it creates the camera,
    /// light, managers, player crowd and UI from code, then initialises <see cref="GameManager"/>
    /// and opens the shop. Safe to run with absolutely nothing else in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameBootstrap : MonoBehaviour
    {
        /// <summary>Optional config asset. When null, <see cref="GameConfig.CreateDefault"/> is used.</summary>
        public GameConfig ConfigOverride;

        private void Awake()
        {
            if (GameManager.Instance != null)
            {
                Debug.LogWarning("[Swarmblastrr] GameBootstrap found an existing GameManager; skipping bootstrap.", this);
                return;
            }

            // 1. Config
            GameConfig config = ConfigOverride != null ? ConfigOverride : GameConfig.CreateDefault();

            // 2. Main camera + follow
            CameraFollow cameraFollow = EnsureMainCamera();

            // 3. Directional light
            EnsureDirectionalLight();

            // 4. Managers
            GameObject managers = new GameObject("Managers");
            GameManager game = managers.AddComponent<GameManager>();
            UpgradeManager upgrades = managers.AddComponent<UpgradeManager>();
            LevelBuilder builder = managers.AddComponent<LevelBuilder>();
            LevelRunner runner = managers.AddComponent<LevelRunner>();
            SwipeInput input = managers.AddComponent<SwipeInput>();

            // 5. Player crowd (renderer first so the controller can find it in Awake)
            GameObject playerGo = new GameObject("Player");
            playerGo.AddComponent<CrowdRenderer>();
            CrowdController player = playerGo.AddComponent<CrowdController>();
            player.Configure(config, input);

            // 6. UI (each screen lives on its own stretched child so hiding one never hides the canvas)
            Canvas canvas = UiFactory.CreateRootCanvas();
            HUD hud = CreateUiHost(canvas, "HUD").AddComponent<HUD>();
            RunSafely(() => hud.Initialize(canvas, game), hud);
            UpgradeShopUI shop = CreateUiHost(canvas, "UpgradeShop").AddComponent<UpgradeShopUI>();
            RunSafely(() => shop.Initialize(canvas, game), shop);
            ResultScreen resultScreen = CreateUiHost(canvas, "ResultScreen").AddComponent<ResultScreen>();
            RunSafely(() => resultScreen.Initialize(canvas, game), resultScreen);
            RunSafely(() => FloatingText.Ensure(), this);

            // 7. Wire the manager and open the shop
            game.Initialize(config, player, builder, runner, upgrades, cameraFollow);
            game.EnterShop();
        }

        private static CameraFollow EnsureMainCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = FindFirstObjectByType<Camera>();
                if (cam != null && !cam.CompareTag("MainCamera"))
                {
                    cam.tag = "MainCamera";
                }
            }

            if (cam == null)
            {
                GameObject go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.56f, 0.76f, 0.95f);
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 400f;
                cam.fieldOfView = 60f;
                go.transform.position = new Vector3(0f, 9f, -11f);
                go.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            }

            if (FindFirstObjectByType<AudioListener>() == null)
            {
                cam.gameObject.AddComponent<AudioListener>();
            }

            CameraFollow follow = cam.GetComponent<CameraFollow>();
            if (follow == null)
            {
                follow = cam.gameObject.AddComponent<CameraFollow>();
            }

            return follow;
        }

        private static void EnsureDirectionalLight()
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    return;
                }
            }

            GameObject go = new GameObject("Directional Light");
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // No authored lighting in the scene: give the half-Lambert shaders a sane ambient term.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.46f, 0.55f);
        }

        private static GameObject CreateUiHost(Canvas root, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(root.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            return go;
        }

        private static void RunSafely(Action action, UnityEngine.Object context)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogException(e, context);
            }
        }
    }
}
