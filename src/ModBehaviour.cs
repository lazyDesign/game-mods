using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuckovHaptics
{
    /// <summary>
    /// Haptic Feedback Mod for Escape from Duckov
    /// Provides controller vibration on game events
    /// v7.0 - Fixed event unsubscription and performance issues
    /// </summary>
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private float _vibrationEndTime;
        private bool _isVibrating;
        private const float GAMEPAD_CHECK_INTERVAL = 2f;

        // Config values
        private float _fireIntensityLow = 0.5f;
        private float _fireIntensityHigh = 0.7f;
        private int _fireDurationMs = 100;

        private float _damageIntensityLow = 0.8f;
        private float _damageIntensityHigh = 0.6f;
        private int _damageDurationMs = 200;

        // Kill/Headshot config
        private float _killIntensity = 0.9f;
        private float _headshotIntensity = 1.0f;

        // Event subscription tracking for proper cleanup
        private struct EventSubscription
        {
            public EventInfo EventInfo;
            public Delegate Handler;
            public object Target; // null for static events
        }
        private List<EventSubscription> _eventSubscriptions = new List<EventSubscription>();

        // Type cache to avoid repeated reflection
        private static Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();
        private static bool _typeCacheInitialized = false;

        // Debug window
        private bool _showDebugWindow = false;
        private Rect _windowRect = new Rect(20, 20, 350, 400);

        // Active gamepad tracking
        private Gamepad _activeGamepad;
        private string _activeGamepadName = "None";
        private float _lastGamepadInputTime;
        private const float GAMEPAD_INPUT_TIMEOUT = 1.0f;

        // ModConfig
        private const string MOD_NAME = "DuckovHaptics";
        private bool _modConfigInitialized = false;
        private bool _hapticsEnabled = true;

        void Awake()
        {
            Debug.Log("[DuckovHaptics] Haptic Feedback Mod v7.0 Loaded! Press F9 for debug window");
            InitializeTypeCache();
            InitializeModConfig();
        }

        private void InitializeTypeCache()
        {
            if (_typeCacheInitialized) return;

            try
            {
                // Cache commonly used types once
                string[] typeNames = { "ItemAgent_Gun", "LevelManager", "InputManager", "CharacterMainControl", "HitMarker" };

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var typeName in typeNames)
                    {
                        if (_typeCache.ContainsKey(typeName)) continue;

                        var type = asm.GetType(typeName) ?? asm.GetType($"Duckov.{typeName}");
                        if (type != null)
                        {
                            _typeCache[typeName] = type;
                        }
                    }
                }

                // Find OnKillMarker event host type
                if (!_typeCache.ContainsKey("KillMarkerHost"))
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        foreach (var type in asm.GetTypes())
                        {
                            try
                            {
                                var evt = type.GetEvent("OnKillMarker", BindingFlags.Public | BindingFlags.Static);
                                if (evt != null)
                                {
                                    _typeCache["KillMarkerHost"] = type;
                                    break;
                                }
                            }
                            catch { }
                        }
                        if (_typeCache.ContainsKey("KillMarkerHost")) break;
                    }
                }

                _typeCacheInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovHaptics] Type cache init failed: {ex.Message}");
            }
        }

        private Type GetCachedType(string typeName)
        {
            return _typeCache.TryGetValue(typeName, out var type) ? type : null;
        }

        private void InitializeModConfig()
        {
            try
            {
                if (!ModConfigAPI.Initialize())
                {
                    return;
                }

                ModConfigAPI.SafeAddBoolDropdownList(MOD_NAME, "Enabled", "Enable Haptic Feedback", true);
                ModConfigAPI.SafeAddInputWithSlider(MOD_NAME, "FireIntensity", "Fire Vibration Intensity",
                    typeof(float), 0.6f, new Vector2(0f, 1f));
                ModConfigAPI.SafeAddInputWithSlider(MOD_NAME, "FireDuration", "Fire Vibration Duration (ms)",
                    typeof(int), 100, new Vector2(10, 500));
                ModConfigAPI.SafeAddInputWithSlider(MOD_NAME, "KillIntensity", "Kill Vibration Intensity",
                    typeof(float), 0.9f, new Vector2(0f, 1f));
                ModConfigAPI.SafeAddInputWithSlider(MOD_NAME, "HeadshotIntensity", "Headshot Vibration Intensity",
                    typeof(float), 1.0f, new Vector2(0f, 1f));
                ModConfigAPI.SafeAddInputWithSlider(MOD_NAME, "DamageIntensity", "Damage Vibration Intensity",
                    typeof(float), 0.8f, new Vector2(0f, 1f));
                ModConfigAPI.SafeAddInputWithSlider(MOD_NAME, "DamageDuration", "Damage Vibration Duration (ms)",
                    typeof(int), 200, new Vector2(10, 1000));

                LoadSettings();
                ModConfigAPI.SafeAddOnOptionsChangedDelegate(OnConfigChanged);
                _modConfigInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovHaptics] ModConfig init failed: {ex.Message}");
            }
        }

        private void LoadSettings()
        {
            _hapticsEnabled = ModConfigAPI.SafeLoad<bool>(MOD_NAME, "Enabled", true);

            float intensity = ModConfigAPI.SafeLoad<float>(MOD_NAME, "FireIntensity", 0.6f);
            _fireIntensityLow = intensity * 0.7f;
            _fireIntensityHigh = intensity;
            _fireDurationMs = ModConfigAPI.SafeLoad<int>(MOD_NAME, "FireDuration", 100);

            _killIntensity = ModConfigAPI.SafeLoad<float>(MOD_NAME, "KillIntensity", 0.9f);
            _headshotIntensity = ModConfigAPI.SafeLoad<float>(MOD_NAME, "HeadshotIntensity", 1.0f);

            float damageIntensity = ModConfigAPI.SafeLoad<float>(MOD_NAME, "DamageIntensity", 0.8f);
            _damageIntensityLow = damageIntensity;
            _damageIntensityHigh = damageIntensity * 0.75f;
            _damageDurationMs = ModConfigAPI.SafeLoad<int>(MOD_NAME, "DamageDuration", 200);
        }

        private void OnConfigChanged(string key)
        {
            if (key.StartsWith(MOD_NAME))
            {
                LoadSettings();
            }
        }

        void OnEnable()
        {
            SubscribeToEvents();
        }

        void OnDisable()
        {
            StopVibration();
            UnsubscribeFromEvents();
        }

        void Update()
        {
            // Stop vibration when duration expires
            if (_isVibrating && Time.unscaledTime >= _vibrationEndTime)
            {
                StopVibration();
            }

            // Gamepad tracking - only check when needed
            int gamepadCount = Gamepad.all.Count;
            if (gamepadCount == 0)
            {
                _activeGamepad = null;
                return;
            }

            if (gamepadCount == 1)
            {
                if (_activeGamepad == null)
                {
                    _activeGamepad = Gamepad.all[0];
                    _activeGamepadName = _activeGamepad.name;
                }
                _lastGamepadInputTime = Time.unscaledTime;
                return;
            }

            // Multi-gamepad: detect which one is being used
            foreach (var gamepad in Gamepad.all)
            {
                bool hasInput = gamepad.buttonSouth.isPressed || gamepad.buttonNorth.isPressed ||
                    gamepad.buttonEast.isPressed || gamepad.buttonWest.isPressed ||
                    gamepad.leftTrigger.ReadValue() > 0.1f || gamepad.rightTrigger.ReadValue() > 0.1f ||
                    gamepad.leftShoulder.isPressed || gamepad.rightShoulder.isPressed ||
                    gamepad.leftStick.ReadValue().magnitude > 0.2f ||
                    gamepad.rightStick.ReadValue().magnitude > 0.2f;

                if (hasInput)
                {
                    _lastGamepadInputTime = Time.unscaledTime;
                    if (_activeGamepad != gamepad)
                    {
                        _activeGamepad = gamepad;
                        _activeGamepadName = gamepad.name;
                    }
                    break;
                }
            }

            // F9 to toggle debug window
            if (Input.GetKeyDown(KeyCode.F9))
            {
                _showDebugWindow = !_showDebugWindow;
            }
        }

        void OnGUI()
        {
            if (!_showDebugWindow) return;
            _windowRect = GUILayout.Window(98765, _windowRect, DrawDebugWindow, "DuckovHaptics Debug (F9 to close)");
        }

        private void DrawDebugWindow(int windowId)
        {
            GUILayout.Label("=== Gamepads ===");
            GUILayout.Label($"Gamepad.current: {(Gamepad.current != null ? Gamepad.current.name : "NULL")}");
            GUILayout.Label($"Gamepad.all.Count: {Gamepad.all.Count}");

            foreach (var gp in Gamepad.all)
            {
                GUILayout.Label($"  - {gp.name}");
            }

            GUILayout.Space(10);
            GUILayout.Label($"Active: {_activeGamepadName}");
            GUILayout.Label($"Vibrating: {_isVibrating}");
            GUILayout.Label($"Subscriptions: {_eventSubscriptions.Count}");

            GUILayout.Space(10);
            GUILayout.Label("=== Test Vibration ===");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Light")) Vibrate(0.2f, 0.3f, 200);
            if (GUILayout.Button("Medium")) Vibrate(0.5f, 0.6f, 300);
            if (GUILayout.Button("Strong")) Vibrate(0.9f, 1.0f, 500);
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        private void SubscribeToEvents()
        {
            SubscribeToStaticEvent("ItemAgent_Gun", "OnMainCharacterShootEvent", "OnShoot");
            SubscribeToStaticEvent("LevelManager", "OnMainCharacterDead", "OnDeath");
            SubscribeToStaticEvent("InputManager", "OnSwitchWeaponInput", "OnWeaponSwitch");
            SubscribeToKillMarkerEvent();
        }

        private void SubscribeToStaticEvent(string typeName, string eventName, string handlerName)
        {
            try
            {
                Type type = GetCachedType(typeName);
                if (type == null) return;

                var eventInfo = type.GetEvent(eventName, BindingFlags.Public | BindingFlags.Static);
                if (eventInfo == null) return;

                var handlerMethod = GetType().GetMethod(handlerName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (handlerMethod == null) return;

                var handler = CreateCompatibleDelegate(eventInfo.EventHandlerType, handlerMethod);
                if (handler != null)
                {
                    eventInfo.AddEventHandler(null, handler);
                    _eventSubscriptions.Add(new EventSubscription
                    {
                        EventInfo = eventInfo,
                        Handler = handler,
                        Target = null
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovHaptics] Failed to subscribe to {typeName}.{eventName}: {ex.Message}");
            }
        }

        private void SubscribeToKillMarkerEvent()
        {
            try
            {
                Type eventType = GetCachedType("KillMarkerHost");
                if (eventType == null) return;

                var killMarkerEvent = eventType.GetEvent("OnKillMarker", BindingFlags.Public | BindingFlags.Static);
                if (killMarkerEvent == null) return;

                var handler = CreateKillMarkerDelegate(killMarkerEvent.EventHandlerType);
                if (handler != null)
                {
                    killMarkerEvent.AddEventHandler(null, handler);
                    _eventSubscriptions.Add(new EventSubscription
                    {
                        EventInfo = killMarkerEvent,
                        Handler = handler,
                        Target = null
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovHaptics] Failed to subscribe to OnKillMarker: {ex.Message}");
            }
        }

        private void UnsubscribeFromEvents()
        {
            foreach (var sub in _eventSubscriptions)
            {
                try
                {
                    sub.EventInfo.RemoveEventHandler(sub.Target, sub.Handler);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DuckovHaptics] Failed to unsubscribe from {sub.EventInfo.Name}: {ex.Message}");
                }
            }
            _eventSubscriptions.Clear();
        }

        private Delegate CreateKillMarkerDelegate(Type delegateType)
        {
            try
            {
                var invokeMethod = delegateType.GetMethod("Invoke");
                var parameters = invokeMethod?.GetParameters();

                if (parameters == null || parameters.Length == 0)
                {
                    Action action = () => VibrateForKill(false);
                    return Delegate.CreateDelegate(delegateType, action.Target, action.Method);
                }
                else if (parameters.Length == 1)
                {
                    return Delegate.CreateDelegate(delegateType, this,
                        GetType().GetMethod("OnKillMarkerOneParam", BindingFlags.Instance | BindingFlags.NonPublic));
                }
                else if (parameters.Length == 2)
                {
                    return Delegate.CreateDelegate(delegateType, this,
                        GetType().GetMethod("OnKillMarkerTwoParams", BindingFlags.Instance | BindingFlags.NonPublic));
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private void OnKillMarkerOneParam(object param)
        {
            string paramStr = param?.ToString() ?? "";
            bool isHeadshot = paramStr.ToLower().Contains("crit");
            VibrateForKill(isHeadshot);
        }

        private void OnKillMarkerTwoParams(object param1, object param2)
        {
            string p1 = param1?.ToString() ?? "";
            string p2 = param2?.ToString() ?? "";
            bool isHeadshot = p1.ToLower().Contains("crit") || p2.ToLower().Contains("crit");
            VibrateForKill(isHeadshot);
        }

        private void VibrateForKill(bool isHeadshot)
        {
            if (isHeadshot)
            {
                Vibrate(_headshotIntensity * 0.8f, _headshotIntensity, 180);
            }
            else
            {
                Vibrate(_killIntensity * 0.5f, _killIntensity * 0.7f, 120);
            }
        }

        private Delegate CreateCompatibleDelegate(Type delegateType, MethodInfo method)
        {
            try
            {
                var invokeMethod = delegateType.GetMethod("Invoke");
                var paramCount = invokeMethod?.GetParameters().Length ?? 0;

                if (paramCount == 0)
                {
                    Action action = () => method.Invoke(this, null);
                    return Delegate.CreateDelegate(delegateType, action.Target, action.Method);
                }
                else
                {
                    return Delegate.CreateDelegate(delegateType, this,
                        GetType().GetMethod("GenericHandler", BindingFlags.Instance | BindingFlags.NonPublic));
                }
            }
            catch
            {
                return null;
            }
        }

        // Weapon category enum
        private enum WeaponCategory
        {
            Unknown, Pistol, SMG, Rifle, Shotgun, Sniper, Melee, Heavy
        }

        private WeaponCategory DetectWeaponCategory(string weaponName)
        {
            if (string.IsNullOrEmpty(weaponName)) return WeaponCategory.Unknown;

            string name = weaponName.ToUpper();

            if (name.Contains("MP155") || name.Contains("TOZ") || name.Contains("SHOTGUN") || name.Contains("SAIGA"))
                return WeaponCategory.Shotgun;
            if (name.Contains("SV98") || name.Contains("M107") || name.Contains("SNIPER") || name.Contains("SVD") || name.Contains("MOSIN"))
                return WeaponCategory.Sniper;
            if (name.Contains("MP7") || name.Contains("MP5") || name.Contains("VECTOR") || name.Contains("UZI") || name.Contains("SMG") || name.Contains("P90"))
                return WeaponCategory.SMG;
            if (name.Contains("AK") || name.Contains("MDR") || name.Contains("M4") || name.Contains("RIFLE") || name.Contains("SCAR"))
                return WeaponCategory.Rifle;
            if (name.Contains("GLOCK") || name.Contains("TT") || name.Contains("PISTOL") || name.Contains("1911") || name.Contains("BERETTA"))
                return WeaponCategory.Pistol;
            if (name.Contains("KNIFE") || name.Contains("MELEE") || name.Contains("AXE"))
                return WeaponCategory.Melee;
            if (name.Contains("ROCKET") || name.Contains("RPG") || name.Contains("LAUNCHER"))
                return WeaponCategory.Heavy;

            return WeaponCategory.Unknown;
        }

        private void VibrateForWeapon(WeaponCategory category)
        {
            float low, high;
            int duration;

            switch (category)
            {
                case WeaponCategory.Pistol: low = 0.3f; high = 0.5f; duration = 60; break;
                case WeaponCategory.SMG: low = 0.4f; high = 0.5f; duration = 40; break;
                case WeaponCategory.Rifle: low = 0.5f; high = 0.7f; duration = 80; break;
                case WeaponCategory.Shotgun: low = 0.8f; high = 1.0f; duration = 150; break;
                case WeaponCategory.Sniper: low = 1.0f; high = 1.0f; duration = 300; break;
                case WeaponCategory.Melee: low = 0.6f; high = 0.8f; duration = 100; break;
                case WeaponCategory.Heavy: low = 1.0f; high = 1.0f; duration = 300; break;
                default: low = _fireIntensityLow; high = _fireIntensityHigh; duration = _fireDurationMs; break;
            }

            Vibrate(low, high, duration);
        }

        // Event handlers
        private void OnShoot()
        {
            Vibrate(_fireIntensityLow, _fireIntensityHigh, _fireDurationMs);
        }

        private void OnAttack()
        {
            VibrateForWeapon(WeaponCategory.Melee);
        }

        private void OnDeath()
        {
            Vibrate(_damageIntensityLow, _damageIntensityHigh, _damageDurationMs * 2);
        }

        private void OnWeaponSwitch()
        {
            Vibrate(0.2f, 0.3f, 50);
        }

        private void GenericHandler(object arg)
        {
            string weaponInfo = arg?.ToString() ?? "";
            WeaponCategory category = DetectWeaponCategory(weaponInfo);
            VibrateForWeapon(category);
        }

        private void Vibrate(float lowFrequency, float highFrequency, int durationMs)
        {
            try
            {
                if (!_hapticsEnabled) return;

                // For multi-gamepad, check timeout
                if (Gamepad.all.Count > 1)
                {
                    if (Time.unscaledTime - _lastGamepadInputTime > GAMEPAD_INPUT_TIMEOUT)
                    {
                        return;
                    }
                }

                Gamepad targetGamepad = _activeGamepad;
                if (targetGamepad == null && Gamepad.all.Count > 0)
                {
                    targetGamepad = Gamepad.all[0];
                }

                if (targetGamepad == null) return;

                targetGamepad.SetMotorSpeeds(
                    Mathf.Clamp01(lowFrequency),
                    Mathf.Clamp01(highFrequency)
                );

                _isVibrating = true;
                _vibrationEndTime = Time.unscaledTime + (durationMs / 1000f);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DuckovHaptics] Vibration error: {ex.Message}");
            }
        }

        private void StopVibration()
        {
            if (!_isVibrating) return;

            try
            {
                _activeGamepad?.SetMotorSpeeds(0f, 0f);
                _isVibrating = false;
            }
            catch { }
        }

        void OnDestroy()
        {
            StopVibration();
            UnsubscribeFromEvents();
        }

        void OnApplicationQuit()
        {
            StopVibration();
            UnsubscribeFromEvents();
        }
    }
}
