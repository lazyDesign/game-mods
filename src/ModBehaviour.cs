using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuckovHaptics
{
    /// <summary>
    /// Haptic Feedback Mod for Escape from Duckov
    /// Provides controller vibration on game events
    /// </summary>
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private float _vibrationEndTime;
        private bool _isVibrating;
        private Gamepad _cachedGamepad;
        private float _lastGamepadCheck;
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

        // Event delegates to unsubscribe later
        private Delegate _shootEventHandler;
        private Delegate _attackEventHandler;
        private Delegate _deathEventHandler;
        private Delegate _weaponSwitchHandler;
        private Delegate _enemyDeadHandler;

        // Debug window
        private bool _showDebugWindow = false;
        private Rect _windowRect = new Rect(20, 20, 350, 400);

        // Active gamepad tracking
        private Gamepad _activeGamepad;
        private string _activeGamepadName = "None";
        private float _lastGamepadInputTime;
        private const float GAMEPAD_INPUT_TIMEOUT = 1.0f; // Only vibrate if gamepad used within 1 second

        // ModConfig
        private const string MOD_NAME = "DuckovHaptics";
        private bool _modConfigInitialized = false;
        private bool _hapticsEnabled = true;

        void Awake()
        {
            Debug.Log("[DuckovHaptics] ====================================");
            Debug.Log("[DuckovHaptics] Haptic Feedback Mod v6.8 Loaded! Press F9 for debug window");
            Debug.Log("[DuckovHaptics] ====================================");
            LogAllInputDevices();
            InitializeModConfig();
        }

        private void InitializeModConfig()
        {
            try
            {
                if (!ModConfigAPI.Initialize())
                {
                    Debug.Log("[DuckovHaptics] ModConfig not available, using default settings");
                    return;
                }

                // Register settings - Enabled toggle first, then organized by category
                ModConfigAPI.SafeAddBoolDropdownList(MOD_NAME, "Enabled", "Enable Haptic Feedback", true);

                // Fire/Shoot settings
                ModConfigAPI.SafeAddInputWithSlider(MOD_NAME, "FireIntensity", "Fire Vibration Intensity",
                    typeof(float), 0.6f, new Vector2(0f, 1f));
                ModConfigAPI.SafeAddInputWithSlider(MOD_NAME, "FireDuration", "Fire Vibration Duration (ms)",
                    typeof(int), 100, new Vector2(10, 500));

                // Kill feedback settings
                ModConfigAPI.SafeAddInputWithSlider(MOD_NAME, "KillIntensity", "Kill Vibration Intensity",
                    typeof(float), 0.9f, new Vector2(0f, 1f));
                ModConfigAPI.SafeAddInputWithSlider(MOD_NAME, "HeadshotIntensity", "Headshot Vibration Intensity",
                    typeof(float), 1.0f, new Vector2(0f, 1f));

                // Damage settings
                ModConfigAPI.SafeAddInputWithSlider(MOD_NAME, "DamageIntensity", "Damage Vibration Intensity",
                    typeof(float), 0.8f, new Vector2(0f, 1f));
                ModConfigAPI.SafeAddInputWithSlider(MOD_NAME, "DamageDuration", "Damage Vibration Duration (ms)",
                    typeof(int), 200, new Vector2(10, 1000));

                // Load saved values
                LoadSettings();

                // Subscribe to config changes
                ModConfigAPI.SafeAddOnOptionsChangedDelegate(OnConfigChanged);

                _modConfigInitialized = true;
                Debug.Log("[DuckovHaptics] ModConfig integration initialized!");
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

            Debug.Log($"[DuckovHaptics] Settings loaded: Enabled={_hapticsEnabled}, Fire={intensity:F2}/{_fireDurationMs}ms, Kill={_killIntensity:F2}, Headshot={_headshotIntensity:F2}");
        }

        private void OnConfigChanged(string key)
        {
            if (key.StartsWith(MOD_NAME))
            {
                Debug.Log($"[DuckovHaptics] Config changed: {key}");
                LoadSettings();
            }
        }

        void OnEnable()
        {
            Debug.Log("[DuckovHaptics] OnEnable - Subscribing to events...");
            SubscribeToEvents();
        }

        void OnDisable()
        {
            Debug.Log("[DuckovHaptics] OnDisable - Unsubscribing...");
            StopVibration();
            UnsubscribeFromEvents();
        }

        void Update()
        {
            if (_isVibrating && Time.unscaledTime >= _vibrationEndTime)
            {
                StopVibration();
            }

            // Track which gamepad is being used
            // If only one gamepad, just use it and keep refreshing the input time
            if (Gamepad.all.Count == 1)
            {
                if (_activeGamepad == null)
                {
                    _activeGamepad = Gamepad.all[0];
                    _activeGamepadName = _activeGamepad.name;
                    Debug.Log($"[DuckovHaptics] Auto-selected single gamepad: {_activeGamepad.name}");
                }
                // Always keep input time fresh for single gamepad (no need to detect which one)
                _lastGamepadInputTime = Time.unscaledTime;
            }

            foreach (var gamepad in Gamepad.all)
            {
                // Check if any button or stick is being used on this gamepad
                // Use ReadValue() for triggers since isPressed might not work with Steam Input
                bool hasInput = gamepad.buttonSouth.isPressed || gamepad.buttonNorth.isPressed ||
                    gamepad.buttonEast.isPressed || gamepad.buttonWest.isPressed ||
                    gamepad.leftTrigger.ReadValue() > 0.1f || gamepad.rightTrigger.ReadValue() > 0.1f ||
                    gamepad.leftShoulder.isPressed || gamepad.rightShoulder.isPressed ||
                    gamepad.leftStick.ReadValue().magnitude > 0.2f ||
                    gamepad.rightStick.ReadValue().magnitude > 0.2f ||
                    gamepad.dpad.up.isPressed || gamepad.dpad.down.isPressed ||
                    gamepad.dpad.left.isPressed || gamepad.dpad.right.isPressed;

                if (hasInput)
                {
                    _lastGamepadInputTime = Time.unscaledTime;
                    if (_activeGamepad != gamepad)
                    {
                        _activeGamepad = gamepad;
                        _activeGamepadName = gamepad.name;
                        Debug.Log($"[DuckovHaptics] Active gamepad changed to: {gamepad.name}");
                    }
                    break;
                }
            }

            // F9 to toggle debug window
            if (Input.GetKeyDown(KeyCode.F9))
            {
                _showDebugWindow = !_showDebugWindow;
                Debug.Log($"[DuckovHaptics] Debug window: {(_showDebugWindow ? "OPEN" : "CLOSED")}");
            }
        }

        void OnGUI()
        {
            if (!_showDebugWindow) return;

            _windowRect = GUILayout.Window(98765, _windowRect, DrawDebugWindow, "DuckovHaptics Debug (F9 to close)");
        }

        private void DrawDebugWindow(int windowId)
        {
            GUILayout.Label("=== Input Devices ===");
            GUILayout.Label($"Total devices: {InputSystem.devices.Count}");

            foreach (var device in InputSystem.devices)
            {
                GUILayout.Label($"  • {device.name} ({device.GetType().Name})");
            }

            GUILayout.Space(10);
            GUILayout.Label("=== Gamepads ===");
            GUILayout.Label($"Gamepad.current: {(Gamepad.current != null ? Gamepad.current.name : "NULL")}");
            GUILayout.Label($"Gamepad.all.Count: {Gamepad.all.Count}");

            foreach (var gp in Gamepad.all)
            {
                GUILayout.Label($"  • {gp.name} ({gp.GetType().Name})");
            }

            GUILayout.Space(10);
            GUILayout.Label("=== Active Gamepad ===");
            GUILayout.Label($"Using: {_activeGamepadName}");

            GUILayout.Space(10);
            GUILayout.Label("=== Vibration Settings ===");
            GUILayout.Label($"Fire: L={_fireIntensityLow:F2} H={_fireIntensityHigh:F2} {_fireDurationMs}ms");
            GUILayout.Label($"Damage: L={_damageIntensityLow:F2} H={_damageIntensityHigh:F2} {_damageDurationMs}ms");
            GUILayout.Label($"Currently vibrating: {_isVibrating}");

            GUILayout.Space(10);
            GUILayout.Label("=== Test Vibration ===");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Light"))
            {
                Vibrate(0.2f, 0.3f, 200);
            }
            if (GUILayout.Button("Medium"))
            {
                Vibrate(0.5f, 0.6f, 300);
            }
            if (GUILayout.Button("Strong"))
            {
                Vibrate(0.9f, 1.0f, 500);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Refresh Devices"))
            {
                LogAllInputDevices();
            }

            GUI.DragWindow();
        }

        private void SubscribeToEvents()
        {
            // Subscribe to ItemAgent_Gun.OnMainCharacterShootEvent (static event)
            SubscribeToStaticEvent("ItemAgent_Gun", "OnMainCharacterShootEvent", "OnShoot");

            // Subscribe to CharacterMainControl events (instance events on found object)
            SubscribeToCharacterEvents();

            // Subscribe to LevelManager.OnMainCharacterDead
            SubscribeToStaticEvent("LevelManager", "OnMainCharacterDead", "OnDeath");

            // Subscribe to InputManager.OnSwitchWeaponInput
            SubscribeToStaticEvent("InputManager", "OnSwitchWeaponInput", "OnWeaponSwitch");

            // NOTE: Removed Health.OnDead subscription - it was causing enemies to delay dying ("zombie bug")
        }

        private void SubscribeToStaticEvent(string typeName, string eventName, string handlerName)
        {
            try
            {
                Type type = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType(typeName);
                    if (type != null) break;

                    // Try with common namespaces
                    type = asm.GetType($"Duckov.{typeName}");
                    if (type != null) break;
                }

                if (type == null)
                {
                    Debug.LogWarning($"[DuckovHaptics] Type not found: {typeName}");
                    return;
                }

                var eventInfo = type.GetEvent(eventName, BindingFlags.Public | BindingFlags.Static);
                if (eventInfo == null)
                {
                    Debug.LogWarning($"[DuckovHaptics] Event not found: {typeName}.{eventName}");
                    return;
                }

                // Get handler method
                var handlerMethod = GetType().GetMethod(handlerName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (handlerMethod == null)
                {
                    Debug.LogWarning($"[DuckovHaptics] Handler not found: {handlerName}");
                    return;
                }

                // Create delegate matching event signature
                var handler = CreateCompatibleDelegate(eventInfo.EventHandlerType, handlerMethod);
                if (handler != null)
                {
                    eventInfo.AddEventHandler(null, handler); // null for static events
                    Debug.Log($"[DuckovHaptics] Subscribed to {typeName}.{eventName}");

                    // Store for unsubscription
                    if (eventName.Contains("Shoot")) _shootEventHandler = handler;
                    else if (eventName.Contains("Death")) _deathEventHandler = handler;
                    else if (eventName.Contains("Weapon")) _weaponSwitchHandler = handler;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovHaptics] Failed to subscribe to {typeName}.{eventName}: {ex.Message}");
            }
        }

        private void SubscribeToCharacterEvents()
        {
            try
            {
                // Find CharacterMainControl instance
                Type charType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    charType = asm.GetType("CharacterMainControl");
                    if (charType != null) break;
                }

                if (charType == null)
                {
                    Debug.LogWarning("[DuckovHaptics] CharacterMainControl type not found");
                    return;
                }

                var charInstance = FindObjectOfType(charType);
                if (charInstance == null)
                {
                    Debug.LogWarning("[DuckovHaptics] CharacterMainControl instance not found - will retry");
                    return;
                }

                // Subscribe to OnShootEvent
                var shootEvent = charType.GetEvent("OnShootEvent");
                if (shootEvent != null)
                {
                    var handler = CreateCompatibleDelegate(shootEvent.EventHandlerType,
                        GetType().GetMethod("OnShoot", BindingFlags.Instance | BindingFlags.NonPublic));
                    if (handler != null)
                    {
                        shootEvent.AddEventHandler(charInstance, handler);
                        Debug.Log("[DuckovHaptics] Subscribed to CharacterMainControl.OnShootEvent");
                    }
                }

                // Subscribe to OnAttackEvent
                var attackEvent = charType.GetEvent("OnAttackEvent");
                if (attackEvent != null)
                {
                    var handler = CreateCompatibleDelegate(attackEvent.EventHandlerType,
                        GetType().GetMethod("OnAttack", BindingFlags.Instance | BindingFlags.NonPublic));
                    if (handler != null)
                    {
                        attackEvent.AddEventHandler(charInstance, handler);
                        Debug.Log("[DuckovHaptics] Subscribed to CharacterMainControl.OnAttackEvent");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovHaptics] Failed to subscribe to character events: {ex.Message}");
            }
        }

        private void SubscribeToHealthDeadEvent()
        {
            // Based on BFKillFeedback mod - subscribe to Health.OnDead to detect kills
            // Health.OnDead provides DamageInfo with crit > 0 for headshots
            try
            {
                Type healthType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    healthType = asm.GetType("Health");
                    if (healthType != null) break;

                    healthType = asm.GetType("Duckov.Health");
                    if (healthType != null) break;
                }

                if (healthType == null)
                {
                    Debug.LogWarning("[DuckovHaptics] Health type not found - kill detection unavailable");
                    return;
                }

                // Look for OnDead static event
                var onDeadEvent = healthType.GetEvent("OnDead", BindingFlags.Public | BindingFlags.Static);
                if (onDeadEvent != null)
                {
                    Debug.Log($"[DuckovHaptics] Found Health.OnDead event, subscribing...");

                    // The event signature is typically Action<Health, DamageInfo>
                    // We'll use a generic handler that receives both parameters
                    var handler = CreateKillEventDelegate(onDeadEvent.EventHandlerType);
                    if (handler != null)
                    {
                        onDeadEvent.AddEventHandler(null, handler);
                        _enemyDeadHandler = handler;
                        Debug.Log("[DuckovHaptics] Subscribed to Health.OnDead for kill detection");
                    }
                }
                else
                {
                    Debug.LogWarning("[DuckovHaptics] Health.OnDead event not found");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovHaptics] Failed to subscribe to Health.OnDead: {ex.Message}");
            }
        }

        private Delegate CreateKillEventDelegate(Type delegateType)
        {
            try
            {
                // Create a dynamic handler that can receive Health and DamageInfo
                var invokeMethod = delegateType.GetMethod("Invoke");
                var parameters = invokeMethod?.GetParameters();

                if (parameters == null || parameters.Length == 0)
                {
                    Action action = () => OnKill(null, null);
                    return Delegate.CreateDelegate(delegateType, action.Target, action.Method);
                }
                else if (parameters.Length == 1)
                {
                    return Delegate.CreateDelegate(delegateType, this,
                        GetType().GetMethod("OnKillOneParam", BindingFlags.Instance | BindingFlags.NonPublic));
                }
                else if (parameters.Length == 2)
                {
                    return Delegate.CreateDelegate(delegateType, this,
                        GetType().GetMethod("OnKillTwoParams", BindingFlags.Instance | BindingFlags.NonPublic));
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovHaptics] CreateKillEventDelegate failed: {ex.Message}");
                return null;
            }
        }

        // Handler for single parameter kill event
        private void OnKillOneParam(object healthOrDamageInfo)
        {
            Debug.Log($"[DuckovHaptics] >>> KILL (1 param): {healthOrDamageInfo?.GetType().Name} <<<");
            OnKill(healthOrDamageInfo, null);
        }

        // Handler for two parameter kill event (Health, DamageInfo)
        private void OnKillTwoParams(object health, object damageInfo)
        {
            Debug.Log($"[DuckovHaptics] >>> KILL (2 params): Health={health?.GetType().Name}, DamageInfo={damageInfo?.GetType().Name} <<<");
            OnKill(health, damageInfo);
        }

        private void OnKill(object health, object damageInfo)
        {
            try
            {
                bool isHeadshot = false;

                // Try to detect headshot from DamageInfo.crit > 0 (like BFKillFeedback)
                if (damageInfo != null)
                {
                    var critField = damageInfo.GetType().GetField("crit");
                    var critProp = damageInfo.GetType().GetProperty("crit");

                    float critValue = 0f;
                    if (critField != null)
                    {
                        critValue = Convert.ToSingle(critField.GetValue(damageInfo));
                    }
                    else if (critProp != null)
                    {
                        critValue = Convert.ToSingle(critProp.GetValue(damageInfo));
                    }

                    isHeadshot = critValue > 0f;
                    Debug.Log($"[DuckovHaptics] Kill crit value: {critValue}, isHeadshot: {isHeadshot}");
                }

                if (isHeadshot)
                {
                    Debug.Log("[DuckovHaptics] >>> HEADSHOT KILL! <<<");
                    // Strong double-pulse for headshot
                    Vibrate(_headshotIntensity, _headshotIntensity, 150);
                }
                else
                {
                    Debug.Log("[DuckovHaptics] >>> KILL! <<<");
                    // Normal kill vibration
                    Vibrate(_killIntensity * 0.7f, _killIntensity, 120);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovHaptics] OnKill error: {ex.Message}");
                // Fallback - still give some feedback
                Vibrate(_killIntensity * 0.7f, _killIntensity, 120);
            }
        }

        private Delegate CreateCompatibleDelegate(Type delegateType, MethodInfo method)
        {
            try
            {
                var invokeMethod = delegateType.GetMethod("Invoke");
                var paramCount = invokeMethod?.GetParameters().Length ?? 0;

                // Create wrapper based on parameter count
                if (paramCount == 0)
                {
                    Action action = () => method.Invoke(this, null);
                    return Delegate.CreateDelegate(delegateType, action.Target, action.Method);
                }
                else
                {
                    // For events with parameters, create generic handler
                    return Delegate.CreateDelegate(delegateType, this,
                        GetType().GetMethod("GenericHandler", BindingFlags.Instance | BindingFlags.NonPublic));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovHaptics] Delegate creation failed: {ex.Message}");
                return null;
            }
        }

        private void UnsubscribeFromEvents()
        {
            // TODO: Implement proper unsubscription
        }

        // Weapon category enum
        private enum WeaponCategory
        {
            Unknown,
            Pistol,
            SMG,
            Rifle,
            Shotgun,
            Sniper,
            Melee,
            Heavy  // Rockets, grenade launchers
        }

        // Detect weapon category from name
        private WeaponCategory DetectWeaponCategory(string weaponName)
        {
            if (string.IsNullOrEmpty(weaponName))
                return WeaponCategory.Unknown;

            string name = weaponName.ToUpper();

            // Shotguns
            if (name.Contains("MP155") || name.Contains("MP-155") || name.Contains("TOZ") ||
                name.Contains("SHOTGUN") || name.Contains("SAIGA"))
                return WeaponCategory.Shotgun;

            // Snipers
            if (name.Contains("SV98") || name.Contains("SV-98") || name.Contains("M107") || name.Contains("M700") ||
                name.Contains("BARRETT") || name.Contains("SNIPER") || name.Contains("AWP") ||
                name.Contains("MOSIN") || name.Contains("SVD"))
                return WeaponCategory.Sniper;

            // SMGs
            if (name.Contains("MP7") || name.Contains("MP5") || name.Contains("VECTOR") ||
                name.Contains("UZI") || name.Contains("AK74U") || name.Contains("PP-19") ||
                name.Contains("SMG") || name.Contains("MAC10") || name.Contains("P90"))
                return WeaponCategory.SMG;

            // Rifles
            if (name.Contains("AK") || name.Contains("MDR") || name.Contains("M4") ||
                name.Contains("AR-15") || name.Contains("RIFLE") || name.Contains("FAL") ||
                name.Contains("SCAR") || name.Contains("HK416") || name.Contains("556"))
                return WeaponCategory.Rifle;

            // Pistols
            if (name.Contains("GLOCK") || name.Contains("TT-33") || name.Contains("TT33") ||
                name.Contains("PM") || name.Contains("MAKAROV") || name.Contains("DESERT") ||
                name.Contains("EAGLE") || name.Contains("PISTOL") || name.Contains("1911") ||
                name.Contains("BERETTA") || name.Contains("USP") || name.Contains("GLICK"))
                return WeaponCategory.Pistol;

            // Melee
            if (name.Contains("KNIFE") || name.Contains("MELEE") || name.Contains("AXE") ||
                name.Contains("SWORD") || name.Contains("MACHETE"))
                return WeaponCategory.Melee;

            // Heavy
            if (name.Contains("ROCKET") || name.Contains("RPG") || name.Contains("GRENADE") ||
                name.Contains("LAUNCHER"))
                return WeaponCategory.Heavy;

            return WeaponCategory.Unknown;
        }

        // Get vibration pattern for weapon category
        private void VibrateForWeapon(WeaponCategory category)
        {
            float low, high;
            int duration;

            switch (category)
            {
                case WeaponCategory.Pistol:
                    low = 0.3f; high = 0.5f; duration = 60;
                    break;
                case WeaponCategory.SMG:
                    low = 0.4f; high = 0.5f; duration = 40;
                    break;
                case WeaponCategory.Rifle:
                    low = 0.5f; high = 0.7f; duration = 80;
                    break;
                case WeaponCategory.Shotgun:
                    low = 0.8f; high = 1.0f; duration = 150;
                    break;
                case WeaponCategory.Sniper:
                    low = 1.0f; high = 1.0f; duration = 300;
                    break;
                case WeaponCategory.Melee:
                    low = 0.6f; high = 0.8f; duration = 100;
                    break;
                case WeaponCategory.Heavy:
                    low = 1.0f; high = 1.0f; duration = 300;
                    break;
                default:
                    low = _fireIntensityLow; high = _fireIntensityHigh; duration = _fireDurationMs;
                    break;
            }

            Debug.Log($"[DuckovHaptics] Weapon category: {category} -> L={low:F2} H={high:F2} {duration}ms");
            Vibrate(low, high, duration);
        }

        // Event handlers
        private void OnShoot()
        {
            Debug.Log("[DuckovHaptics] >>> SHOOT EVENT! <<<");
            Vibrate(_fireIntensityLow, _fireIntensityHigh, _fireDurationMs);
        }

        private void OnAttack()
        {
            Debug.Log("[DuckovHaptics] >>> ATTACK EVENT! <<<");
            VibrateForWeapon(WeaponCategory.Melee);
        }

        private void OnDeath()
        {
            Debug.Log("[DuckovHaptics] >>> DEATH EVENT! <<<");
            Vibrate(_damageIntensityLow, _damageIntensityHigh, _damageDurationMs * 2);
        }

        private void OnWeaponSwitch()
        {
            Debug.Log("[DuckovHaptics] >>> WEAPON SWITCH! <<<");
            Vibrate(0.2f, 0.3f, 50);
        }

        private void GenericHandler(object arg)
        {
            string weaponInfo = arg?.ToString() ?? "";
            Debug.Log($"[DuckovHaptics] >>> GENERIC EVENT: {weaponInfo} <<<");

            WeaponCategory category = DetectWeaponCategory(weaponInfo);
            VibrateForWeapon(category);
        }

        private void LogAllInputDevices()
        {
            Debug.Log("[DuckovHaptics] === Scanning All Input Devices ===");

            // Log all registered devices
            var allDevices = InputSystem.devices;
            Debug.Log($"[DuckovHaptics] Total devices: {allDevices.Count}");

            foreach (var device in allDevices)
            {
                Debug.Log($"[DuckovHaptics] Device: {device.name} | Type: {device.GetType().Name} | Path: {device.path}");
            }

            // Specifically check gamepads
            Debug.Log($"[DuckovHaptics] Gamepad.current: {(Gamepad.current != null ? Gamepad.current.name : "NULL")}");
            Debug.Log($"[DuckovHaptics] Gamepad.all count: {Gamepad.all.Count}");

            foreach (var gp in Gamepad.all)
            {
                Debug.Log($"[DuckovHaptics] Gamepad found: {gp.name} | Type: {gp.GetType().Name}");
            }

            Debug.Log("[DuckovHaptics] === End Device Scan ===");
        }

        private Gamepad FindGamepad()
        {
            // Check cache first (refresh every few seconds)
            if (_cachedGamepad != null && Time.unscaledTime - _lastGamepadCheck < GAMEPAD_CHECK_INTERVAL)
            {
                return _cachedGamepad;
            }

            _lastGamepadCheck = Time.unscaledTime;

            // Strategy 1: Gamepad.current
            if (Gamepad.current != null)
            {
                _cachedGamepad = Gamepad.current;
                Debug.Log($"[DuckovHaptics] Found gamepad via Gamepad.current: {_cachedGamepad.name}");
                return _cachedGamepad;
            }

            // Strategy 2: First from Gamepad.all
            if (Gamepad.all.Count > 0)
            {
                _cachedGamepad = Gamepad.all[0];
                Debug.Log($"[DuckovHaptics] Found gamepad via Gamepad.all: {_cachedGamepad.name}");
                return _cachedGamepad;
            }

            // Strategy 3: Search all devices for anything that looks like a gamepad
            foreach (var device in InputSystem.devices)
            {
                if (device is Gamepad gp)
                {
                    _cachedGamepad = gp;
                    Debug.Log($"[DuckovHaptics] Found gamepad via device scan: {_cachedGamepad.name}");
                    return _cachedGamepad;
                }
            }

            // Strategy 4: Look for specific controller types by name
            foreach (var device in InputSystem.devices)
            {
                var name = device.name.ToLower();
                if (name.Contains("controller") || name.Contains("gamepad") ||
                    name.Contains("xbox") || name.Contains("dualshock") ||
                    name.Contains("dualsense") || name.Contains("xinput"))
                {
                    Debug.Log($"[DuckovHaptics] Found controller-like device: {device.name} ({device.GetType().Name})");
                    // Try to cast it
                    if (device is Gamepad gp2)
                    {
                        _cachedGamepad = gp2;
                        return _cachedGamepad;
                    }
                }
            }

            return null;
        }

        private void Vibrate(float lowFrequency, float highFrequency, int durationMs)
        {
            try
            {
                Debug.Log($"[DuckovHaptics] Vibrate called: L={lowFrequency:F2} H={highFrequency:F2} {durationMs}ms");

                // Check if haptics are enabled
                if (!_hapticsEnabled)
                {
                    Debug.Log("[DuckovHaptics] Vibrate skipped: haptics disabled");
                    return;
                }

                // For single gamepad, skip timeout check
                if (Gamepad.all.Count > 1)
                {
                    // Only vibrate if gamepad was used recently (not mouse/keyboard)
                    if (Time.unscaledTime - _lastGamepadInputTime > GAMEPAD_INPUT_TIMEOUT)
                    {
                        Debug.Log("[DuckovHaptics] Vibrate skipped: timeout (multi-gamepad mode)");
                        return;
                    }
                }

                // Use active gamepad, or fallback to first available
                Gamepad targetGamepad = _activeGamepad;
                if (targetGamepad == null && Gamepad.all.Count > 0)
                {
                    targetGamepad = Gamepad.all[0];
                    Debug.Log($"[DuckovHaptics] Using fallback gamepad: {targetGamepad.name}");
                }

                if (targetGamepad == null)
                {
                    Debug.Log("[DuckovHaptics] No gamepad available to vibrate");
                    return;
                }

                Debug.Log($"[DuckovHaptics] Calling SetMotorSpeeds on {targetGamepad.name}...");
                targetGamepad.SetMotorSpeeds(
                    Mathf.Clamp01(lowFrequency),
                    Mathf.Clamp01(highFrequency)
                );
                Debug.Log($"[DuckovHaptics] SetMotorSpeeds completed!");

                _isVibrating = true;
                _vibrationEndTime = Time.unscaledTime + (durationMs / 1000f);

                Debug.Log($"[DuckovHaptics] Vibrating {targetGamepad.name}: L={lowFrequency:F2} H={highFrequency:F2} for {durationMs}ms");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DuckovHaptics] Vibration error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void StopVibration()
        {
            if (!_isVibrating) return;

            try
            {
                // Stop active gamepad
                _activeGamepad?.SetMotorSpeeds(0f, 0f);
                _isVibrating = false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DuckovHaptics] Stop vibration error: {ex.Message}");
            }
        }

        void OnDestroy()
        {
            StopVibration();
        }

        void OnApplicationQuit()
        {
            StopVibration();
        }
    }
}
