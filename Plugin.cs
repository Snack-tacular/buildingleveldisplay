using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace BuildingLevelDisplay
{
    [BepInPlugin("com.kp.buildingleveldisplay", "Building Level Display", "1.0.0")]
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

        [HarmonyPatch(typeof(PlayerBuilding), "OnEnable")]
        [HarmonyPostfix]
        public static void OnEnable_Postfix(PlayerBuilding __instance)
        {
            EnsureComponentAttached(__instance);
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
        private int _recalculateHeightFrames = 0;
        private PlayerInteract? _localPlayer;
        private float _lastPlayerCheckTime = -99f;

        private void Start()
        {
            _building = GetComponent<PlayerBuilding>();
            _cam = Camera.main;
        }

        private void Update()
        {
            if (_building == null)
            {
                _building = GetComponent<PlayerBuilding>();
                if (_building == null) return;
            }

            // Only show UI if the house type is built and not None
            bool shouldShow = _building.HouseType != HouseType.None && _building.gameObject.activeInHierarchy;
            
            if (!shouldShow)
            {
                if (_canvas != null && _canvas.gameObject.activeSelf)
                {
                    _canvas.gameObject.SetActive(false);
                }
                return;
            }

            // If we should show but UI isn't initialized yet, do it now
            if (!_uiInitialized)
            {
                InitializeUI();
            }

            if (_canvas != null && !_canvas.gameObject.activeSelf)
            {
                _canvas.gameObject.SetActive(true);
            }

            // Handle house type or level changes to recalculate the height bounds and level text
            if (_building.HouseType != _lastHouseType || _levelModule.Level != _lastLevel)
            {
                _lastHouseType = _building.HouseType;
                _lastLevel = _levelModule.Level;
                
                _cachedHeight = CalculateBuildingHeight();
                _recalculateHeightFrames = 15; // recalculate over next 15 frames to let visual transition complete
                
                UpdateLevelText();
            }

            if (_recalculateHeightFrames > 0)
            {
                _cachedHeight = CalculateBuildingHeight();
                _recalculateHeightFrames--;
            }

            // Keep the canvas positioned above the building or interactable button
            if (_canvasRT != null)
            {
                PlayerInteractable? interactable = GetInteractable();
                if (interactable != null)
                {
                    // Position right above the interact button in world space
                    Vector3 interactPos = interactable.transform.position + interactable.CostPanelOffset;
                    _canvasRT.position = interactPos + new Vector3(0f, 1.3f, 0f); // Raised offset
                }
                else
                {
                    // Fallback to mesh-based height (using world space)
                    float targetY = _cachedHeight + 0.3f;
                    _canvasRT.position = transform.position + new Vector3(0f, targetY, 0f);
                }

                // Face the camera (billboard effect)
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

        private void InitializeUI()
        {
            if (_uiInitialized) return;

            // 1. Create Canvas GameObject (Not parented to avoid scale/skew distortion)
            GameObject canvasGo = new GameObject("BuildingLevelCanvas");

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 150; // Below HP bars (200), above regular UI

            _canvasRT = canvasGo.GetComponent<RectTransform>();
            _canvasRT.sizeDelta = new Vector2(90f, 40f);
            _canvasRT.localScale = Vector3.one * 0.026f; // Absolute world-space scale
            _canvasRT.position = transform.position + new Vector3(0f, 3f, 0f);

            // 2. Create Border (Background)
            GameObject borderGo = new GameObject("Border");
            borderGo.transform.SetParent(canvasGo.transform, false);
            _borderImage = borderGo.AddComponent<Image>();
            _borderImage.color = new Color(0.35f, 0.35f, 0.4f, 0.75f); // Sleek grey border
            RectTransform borderRT = borderGo.GetComponent<RectTransform>();
            borderRT.anchorMin = new Vector2(0.5f, 0.5f);
            borderRT.anchorMax = new Vector2(0.5f, 0.5f);
            borderRT.pivot = new Vector2(0.5f, 0.5f);
            borderRT.sizeDelta = new Vector2(82f, 32f); // Slightly larger than background
            borderRT.anchoredPosition = Vector2.zero;

            // 3. Create Inner Background
            GameObject bgGo = new GameObject("Background");
            bgGo.transform.SetParent(canvasGo.transform, false);
            _bgImage = bgGo.AddComponent<Image>();
            _bgImage.color = new Color(0.08f, 0.08f, 0.1f, 0.9f); // Dark translucent card background
            RectTransform bgRT = bgGo.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.5f, 0.5f);
            bgRT.anchorMax = new Vector2(0.5f, 0.5f);
            bgRT.pivot = new Vector2(0.5f, 0.5f);
            bgRT.sizeDelta = new Vector2(80f, 30f);
            bgRT.anchoredPosition = Vector2.zero;

            // 4. Create Text Label
            GameObject textGo = new GameObject("LevelText");
            textGo.transform.SetParent(canvasGo.transform, false);
            _labelText = textGo.AddComponent<Text>();
            _labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _labelText.fontSize = 15; // Larger font size for better readability
            _labelText.fontStyle = FontStyle.Bold;
            _labelText.alignment = TextAnchor.MiddleCenter;
            _labelText.color = Color.white;
            _labelText.supportRichText = true;

            RectTransform textRT = textGo.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0.5f, 0.5f);
            textRT.anchorMax = new Vector2(0.5f, 0.5f);
            textRT.pivot = new Vector2(0.5f, 0.5f);

            // Create materials that ignore depth testing (ZTest Always)
            try
            {
                Material imageOverlayMaterial = new Material(Shader.Find("UI/Default"));
                imageOverlayMaterial.SetInt("unity_GUIZTestMode", 8); // 8 = Always

                Shader textShader = Shader.Find("UI/Default Font") ?? Shader.Find("UI/Default");
                Material textOverlayMaterial = new Material(textShader);
                textOverlayMaterial.SetInt("unity_GUIZTestMode", 8); // 8 = Always

                _borderImage.material = imageOverlayMaterial;
                _bgImage.material = imageOverlayMaterial;
                _labelText.material = textOverlayMaterial;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Error creating overlay materials: {ex.Message}");
            }
            textRT.sizeDelta = new Vector2(80f, 30f);
            textRT.anchoredPosition = Vector2.zero;

            _uiInitialized = true;
            UpdateLevelText();
        }

        private void UpdateLevelText()
        {
            if (_labelText == null) return;

            int level = 1;
            int maxLevel = 0;
            bool isMax = false;

            if (_levelModule == null && _building != null)
            {
                _levelModule = _building.LevelModule;
            }

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
                    // Gold text and border for maxed buildings
                    _labelText.text = $"Lvl {level}/{maxLevel}";
                    _labelText.color = new Color(1.0f, 0.82f, 0.0f, 0.95f);
                    
                    if (_borderImage != null)
                    {
                        _borderImage.color = new Color(1.0f, 0.82f, 0.0f, 0.7f);
                    }
                }
                else
                {
                    // Clean soft white/blue text for leveling
                    _labelText.text = $"Lvl {level}/{maxLevel}";
                    _labelText.color = new Color(0.85f, 0.9f, 1.0f, 0.95f);
                    
                    if (_borderImage != null)
                    {
                        _borderImage.color = new Color(0.35f, 0.35f, 0.4f, 0.75f);
                    }
                }
            }
            else
            {
                // Fallback if no max level is configured
                _labelText.text = $"Lvl {level}";
                _labelText.color = new Color(0.85f, 0.9f, 1.0f, 0.95f);
                
                if (_borderImage != null)
                {
                    _borderImage.color = new Color(0.35f, 0.35f, 0.4f, 0.75f);
                }
            }
        }

        private float CalculateBuildingHeight()
        {
            try
            {
                // Find all renderers in children
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
                            // Skip outline effects, shadows, selectors, and ranges
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

            return 3.0f; // Default fallback height
        }

        private PlayerInteract? GetLocalPlayer()
        {
            if (_localPlayer != null && _localPlayer.gameObject != null) return _localPlayer;
            if (Time.time - _lastPlayerCheckTime > 2.0f)
            {
                _lastPlayerCheckTime = Time.time;
                _localPlayer = FindObjectOfType<PlayerInteract>();
            }
            return _localPlayer;
        }

        private PlayerInteractable? GetInteractable()
        {
            if (_building == null) return null;

            // Try to find in children
            PlayerInteractable interactable = _building.GetComponentInChildren<PlayerInteractable>(true);
            if (interactable != null) return interactable;

            // Fallback to building spot
            if (_building.BuildingSpot != null)
            {
                return _building.BuildingSpot.Interactable;
            }

            return null;
        }

        private void OnEnable()
        {
            if (_canvas != null && _canvas.gameObject != null)
            {
                _canvas.gameObject.SetActive(true);
            }
        }

        private void OnDisable()
        {
            if (_canvas != null && _canvas.gameObject != null)
            {
                _canvas.gameObject.SetActive(false);
            }
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
