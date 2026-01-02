using UnityEngine;

namespace DuckovHaptics
{
    /// <summary>
    /// Main mod entry point for Escape from Duckov Haptic Feedback.
    /// Inherits from Duckov.Modding.ModBehaviour to integrate with the game's mod system.
    /// </summary>
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private HapticsController? _haptics;
        private HapticsConfig? _config;
        private bool _wasFirePressed;
        private KeyCode _fireKeyCode = KeyCode.Mouse0;

        private void Start()
        {
            Debug.Log("[DuckovHaptics] Initializing Haptic Feedback Mod v1.0");

            // Load configuration
            _config = HapticsConfig.Load();

            if (!_config.enabled)
            {
                Debug.Log("[DuckovHaptics] Mod disabled in config");
                return;
            }

            // Parse fire key from config
            if (!string.IsNullOrEmpty(_config.fireKey))
            {
                if (System.Enum.TryParse(_config.fireKey, true, out KeyCode parsedKey))
                {
                    _fireKeyCode = parsedKey;
                }
            }

            // Initialize haptics controller
            _haptics = new HapticsController(_config.controllerIndex);

            if (_haptics.IsAvailable)
            {
                Debug.Log("[DuckovHaptics] Ready! Fire your weapon to feel the haptics.");
            }
            else
            {
                Debug.LogWarning("[DuckovHaptics] No controller detected. Plug in an Xbox controller and restart.");
            }
        }

        private void Update()
        {
            if (_haptics == null || _config == null || !_config.enabled)
                return;

            // Update haptics (handles vibration timeout)
            _haptics.Update();

            // Detect fire input
            DetectFireInput();
        }

        private void DetectFireInput()
        {
            if (_config?.fireHaptics == null || !_config.fireHaptics.enabled)
                return;

            bool isFirePressed = Input.GetKey(_fireKeyCode);

            // Trigger haptics on key down (not held)
            if (isFirePressed && !_wasFirePressed)
            {
                TriggerFireHaptics();
            }

            _wasFirePressed = isFirePressed;
        }

        private void TriggerFireHaptics()
        {
            if (_haptics == null || _config?.fireHaptics == null)
                return;

            var settings = _config.fireHaptics;
            _haptics.Vibrate(
                settings.lowFrequencyIntensity,
                settings.highFrequencyIntensity,
                settings.durationMs
            );
        }

        /// <summary>
        /// Call this method from game events to trigger reload haptics.
        /// Can be hooked into game's reload event system if available.
        /// </summary>
        public void TriggerReloadHaptics()
        {
            if (_haptics == null || _config?.reloadHaptics == null || !_config.reloadHaptics.enabled)
                return;

            var settings = _config.reloadHaptics;
            _haptics.Vibrate(
                settings.lowFrequencyIntensity,
                settings.highFrequencyIntensity,
                settings.durationMs
            );
        }

        /// <summary>
        /// Call this method from game events to trigger damage haptics.
        /// Can be hooked into game's damage event system if available.
        /// </summary>
        public void TriggerDamageHaptics()
        {
            if (_haptics == null || _config?.damageHaptics == null || !_config.damageHaptics.enabled)
                return;

            var settings = _config.damageHaptics;
            _haptics.Vibrate(
                settings.lowFrequencyIntensity,
                settings.highFrequencyIntensity,
                settings.durationMs
            );
        }

        private void OnDestroy()
        {
            Debug.Log("[DuckovHaptics] Shutting down...");
            _haptics?.Dispose();
        }

        private void OnApplicationQuit()
        {
            _haptics?.Dispose();
        }
    }
}
