using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace BuildingLevelDisplay
{
    [BepInPlugin("com.kp.buildingleveldisplay", "Building Level Display", "1.0.3")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource? Log;
        private Harmony? _harmony;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo("Building Level Display Mod is initializing...");

            try
            {
                _harmony = new Harmony("com.kp.buildingleveldisplay");
                _harmony.PatchAll(typeof(PlayerBuildingPatches));
                Log.LogInfo("Building Level Display patches applied successfully!");
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to apply patches inside Building Level Display: " + ex);
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }

    public static class PlayerBuildingPatches
    {
        [HarmonyPatch(typeof(PlayerBuilding), "OnNetworkSpawn")]
        [HarmonyPostfix]
        public static void OnNetworkSpawn_Postfix(PlayerBuilding __instance)
        {
            EnsureComponentAttached(__instance);
        }

        [HarmonyPatch(typeof(BuildingSpot), "OnEnable")]
        [HarmonyPostfix]
        public static void BuildingSpot_OnEnable_Postfix(BuildingSpot __instance)
        {
            if (__instance != null && __instance.PlayerBuilding != null)
            {
                EnsureComponentAttached(__instance.PlayerBuilding);
            }
        }

        [HarmonyPatch(typeof(BuildingSpot), "Build", new Type[0])]
        [HarmonyPostfix]
        public static void BuildingSpot_Build_Postfix(BuildingSpot __instance)
        {
            if (__instance != null && __instance.PlayerBuilding != null)
            {
                EnsureComponentAttached(__instance.PlayerBuilding);
            }
        }

        private static void EnsureComponentAttached(PlayerBuilding building)
        {
            if (building == null) return;

            try
            {
                if (building.gameObject.GetComponent<BuildingLevelDisplayComponent>() == null)
                {
                    building.gameObject.AddComponent<BuildingLevelDisplayComponent>();
                    Plugin.Log?.LogInfo($"Attached BuildingLevelDisplayComponent to building of type {building.HouseType}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error attaching component to building: {ex}");
            }
        }
    }

    public class BuildingLevelDisplayComponent : MonoBehaviour
    {
        private PlayerBuilding? _building;
        private LevelModule? _levelModule;
        private Camera? _cam;

        private Canvas? _canvas;
        private RectTransform? _canvasRT;
        private Text? _labelText;
        private Image? _bgImage;
        private Image? _borderImage;

        private bool _uiInitialized = false;
        private HouseType _lastHouseType = HouseType.None;
        private int _lastLevel = -1;
        private float _cachedHeight = 3.0f;

        private void Start()
        {
            _building = GetComponent<PlayerBuilding>();
            _cam = Camera.main;
            BuildUI();
        }

        private void BuildUI()
        {
            if (_uiInitialized) return;

            try
            {
                // Destroy old canvas if existing
                var oldCanvas = transform.Find("BuildingLevelCanvas");
                if (oldCanvas != null)
                {
                    DestroyImmediate(oldCanvas.gameObject);
                }

                // 1. Create Canvas
                GameObject canvasGo = new GameObject("BuildingLevelCanvas");
                canvasGo.transform.SetParent(transform, false);

                _canvas = canvasGo.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.WorldSpace;
                _canvas.sortingOrder = 250;

                _canvasRT = canvasGo.GetComponent<RectTransform>();
                _canvasRT.sizeDelta = new Vector2(100f, 40f);
                _canvasRT.localScale = Vector3.one * 0.018f;
                _canvasRT.localPosition = new Vector3(0f, 3.5f, 0f);

                // 2. Create Border
                GameObject borderGo = new GameObject("Border");
                borderGo.transform.SetParent(canvasGo.transform, false);
                _borderImage = borderGo.AddComponent<Image>();
                _borderImage.color = new Color(0.35f, 0.35f, 0.4f, 0.85f);
                RectTransform borderRT = borderGo.GetComponent<RectTransform>();
                borderRT.anchorMin = new Vector2(0.5f, 0.5f);
                borderRT.anchorMax = new Vector2(0.5f, 0.5f);
                borderRT.pivot = new Vector2(0.5f, 0.5f);
                borderRT.sizeDelta = new Vector2(94f, 34f);
                borderRT.anchoredPosition = Vector2.zero;

                // 3. Create Background
                GameObject bgGo = new GameObject("Background");
                bgGo.transform.SetParent(canvasGo.transform, false);
                _bgImage = bgGo.AddComponent<Image>();
                _bgImage.color = new Color(0.06f, 0.06f, 0.08f, 0.92f);
                RectTransform bgRT = bgGo.GetComponent<RectTransform>();
                bgRT.anchorMin = new Vector2(0.5f, 0.5f);
                bgRT.anchorMax = new Vector2(0.5f, 0.5f);
                bgRT.pivot = new Vector2(0.5f, 0.5f);
                bgRT.sizeDelta = new Vector2(90f, 30f);
                bgRT.anchoredPosition = Vector2.zero;

                // 4. Create Text Label
                GameObject textGo = new GameObject("LevelText");
                textGo.transform.SetParent(canvasGo.transform, false);
                _labelText = textGo.AddComponent<Text>();
                _labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _labelText.fontSize = 15;
                _labelText.fontStyle = FontStyle.Bold;
                _labelText.alignment = TextAnchor.MiddleCenter;
                _labelText.color = Color.white;
                _labelText.text = "Lvl 1";

                RectTransform textRT = textGo.GetComponent<RectTransform>();
                textRT.anchorMin = new Vector2(0.5f, 0.5f);
                textRT.anchorMax = new Vector2(0.5f, 0.5f);
                textRT.pivot = new Vector2(0.5f, 0.5f);
                textRT.sizeDelta = new Vector2(90f, 30f);
                textRT.anchoredPosition = Vector2.zero;

                _uiInitialized = true;
                UpdateLevelText();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Failed to build UI for building: {ex}");
            }
        }

        private void Update()
        {
            if (_building == null)
            {
                _building = GetComponent<PlayerBuilding>();
                if (_building == null) return;
            }

            if (!_uiInitialized)
            {
                BuildUI();
            }

            if (_levelModule == null)
            {
                _levelModule = _building.LevelModule;
            }

            int currentLevel = _levelModule != null ? _levelModule.Level : 1;

            if (_building.HouseType != _lastHouseType || currentLevel != _lastLevel)
            {
                _lastHouseType = _building.HouseType;
                _lastLevel = currentLevel;
                _cachedHeight = CalculateBuildingHeight();
                UpdateLevelText();
            }

            // Position and billboard canvas
            if (_canvasRT != null)
            {
                PlayerInteractable? interactable = GetInteractable();
                if (interactable != null)
                {
                    Vector3 worldInteractPos = interactable.transform.position + interactable.CostPanelOffset;
                    Vector3 localPos = transform.InverseTransformPoint(worldInteractPos);
                    _canvasRT.localPosition = localPos + new Vector3(0f, 1.2f, 0f);
                }
                else
                {
                    _canvasRT.localPosition = new Vector3(0f, _cachedHeight + 0.3f, 0f);
                }

                if (_cam == null || !_cam.gameObject.activeInHierarchy)
                {
                    _cam = Camera.main;
                }

                if (_cam != null)
                {
                    _canvasRT.rotation = Quaternion.LookRotation(_canvasRT.position - _cam.transform.position);
                }
            }
        }

        private void UpdateLevelText()
        {
            if (_labelText == null) return;

            int level = 1;
            int maxLevel = 0;
            bool isMax = false;

            if (_levelModule != null)
            {
                level = _levelModule.Level;
                maxLevel = _levelModule.MaxLevel;
                isMax = _levelModule.IsMaxLevel || (maxLevel > 0 && level >= maxLevel);
            }

            if (maxLevel > 0)
            {
                if (isMax)
                {
                    _labelText.text = $"Lvl {level}/{maxLevel}";
                    _labelText.color = new Color(1.0f, 0.82f, 0.0f, 0.95f);
                    if (_borderImage != null)
                        _borderImage.color = new Color(1.0f, 0.82f, 0.0f, 0.85f);
                }
                else
                {
                    _labelText.text = $"Lvl {level}/{maxLevel}";
                    _labelText.color = new Color(0.85f, 0.9f, 1.0f, 0.95f);
                    if (_borderImage != null)
                        _borderImage.color = new Color(0.35f, 0.35f, 0.4f, 0.85f);
                }
            }
            else
            {
                _labelText.text = $"Lvl {level}";
                _labelText.color = new Color(0.85f, 0.9f, 1.0f, 0.95f);
                if (_borderImage != null)
                    _borderImage.color = new Color(0.35f, 0.35f, 0.4f, 0.85f);
            }
        }

        private float CalculateBuildingHeight()
        {
            try
            {
                Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
                if (renderers != null && renderers.Length > 0)
                {
                    float maxHeight = 0f;
                    float baseHeight = transform.position.y;
                    bool foundValidRenderer = false;

                    foreach (Renderer r in renderers)
                    {
                        if (r != null && r.enabled && r.gameObject.activeInHierarchy)
                        {
                            string nameLower = r.gameObject.name.ToLower();
                            if (nameLower.Contains("shadow") || 
                                nameLower.Contains("decal") || 
                                nameLower.Contains("ui") || 
                                nameLower.Contains("outline") ||
                                nameLower.Contains("range") ||
                                nameLower.Contains("selection") ||
                                nameLower.Contains("arrow"))
                                continue;

                            float topY = r.bounds.max.y;
                            float height = topY - baseHeight;
                            if (height > maxHeight)
                            {
                                maxHeight = height;
                                foundValidRenderer = true;
                            }
                        }
                    }

                    if (foundValidRenderer && maxHeight > 0.5f)
                    {
                        return maxHeight;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Error calculating building height: {ex.Message}");
            }

            return 3.0f;
        }

        private PlayerInteractable? GetInteractable()
        {
            if (_building == null) return null;

            PlayerInteractable interactable = _building.GetComponentInChildren<PlayerInteractable>(true);
            if (interactable != null) return interactable;

            if (_building.BuildingSpot != null)
            {
                return _building.BuildingSpot.Interactable;
            }

            return null;
        }

        private void OnDestroy()
        {
            if (_canvas != null && _canvas.gameObject != null)
            {
                Destroy(_canvas.gameObject);
            }
        }
    }
}
