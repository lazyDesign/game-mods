using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DuckovHaptics
{
    /// <summary>
    /// Handles controller vibration/haptic feedback via XInput API.
    /// Falls back gracefully if controller is unavailable.
    /// </summary>
    public class HapticsController
    {
        private const int ERROR_SUCCESS = 0;
        private const int ERROR_DEVICE_NOT_CONNECTED = 1167;
        private const ushort MAX_VIBRATION = 65535;

        private readonly int _controllerIndex;
        private bool _isAvailable;
        private float _vibrationEndTime;
        private bool _isVibrating;

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_VIBRATION
        {
            public ushort wLeftMotorSpeed;
            public ushort wRightMotorSpeed;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
        private static extern int XInputSetState(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState(int dwUserIndex, IntPtr pState);

        public HapticsController(int controllerIndex = 0)
        {
            _controllerIndex = controllerIndex;
            _isAvailable = CheckControllerAvailable();

            if (_isAvailable)
            {
                Debug.Log($"[DuckovHaptics] Controller {_controllerIndex} connected and ready for haptics");
            }
            else
            {
                Debug.LogWarning($"[DuckovHaptics] Controller {_controllerIndex} not available. Haptics disabled.");
            }
        }

        public bool IsAvailable => _isAvailable;

        /// <summary>
        /// Check if controller is connected
        /// </summary>
        private bool CheckControllerAvailable()
        {
            try
            {
                // XInputGetState requires a 16-byte state struct, we just check return value
                IntPtr statePtr = Marshal.AllocHGlobal(16);
                try
                {
                    int result = XInputGetState(_controllerIndex, statePtr);
                    return result == ERROR_SUCCESS;
                }
                finally
                {
                    Marshal.FreeHGlobal(statePtr);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovHaptics] XInput check failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Trigger vibration with specified intensities and duration
        /// </summary>
        /// <param name="lowFrequency">Left motor intensity (0.0 - 1.0)</param>
        /// <param name="highFrequency">Right motor intensity (0.0 - 1.0)</param>
        /// <param name="durationMs">Duration in milliseconds</param>
        public void Vibrate(float lowFrequency, float highFrequency, int durationMs)
        {
            if (!_isAvailable)
            {
                // Recheck availability periodically
                _isAvailable = CheckControllerAvailable();
                if (!_isAvailable) return;
            }

            try
            {
                var vibration = new XINPUT_VIBRATION
                {
                    wLeftMotorSpeed = (ushort)(Mathf.Clamp01(lowFrequency) * MAX_VIBRATION),
                    wRightMotorSpeed = (ushort)(Mathf.Clamp01(highFrequency) * MAX_VIBRATION)
                };

                int result = XInputSetState(_controllerIndex, ref vibration);

                if (result == ERROR_SUCCESS)
                {
                    _isVibrating = true;
                    _vibrationEndTime = Time.unscaledTime + (durationMs / 1000f);
                }
                else if (result == ERROR_DEVICE_NOT_CONNECTED)
                {
                    _isAvailable = false;
                    Debug.LogWarning("[DuckovHaptics] Controller disconnected");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DuckovHaptics] Vibration error: {ex.Message}");
                _isAvailable = false;
            }
        }

        /// <summary>
        /// Stop all vibration immediately
        /// </summary>
        public void StopVibration()
        {
            if (!_isVibrating) return;

            try
            {
                var vibration = new XINPUT_VIBRATION
                {
                    wLeftMotorSpeed = 0,
                    wRightMotorSpeed = 0
                };

                XInputSetState(_controllerIndex, ref vibration);
                _isVibrating = false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DuckovHaptics] Stop vibration error: {ex.Message}");
            }
        }

        /// <summary>
        /// Call every frame to handle vibration timeout
        /// </summary>
        public void Update()
        {
            if (_isVibrating && Time.unscaledTime >= _vibrationEndTime)
            {
                StopVibration();
            }
        }

        /// <summary>
        /// Clean up - stop any ongoing vibration
        /// </summary>
        public void Dispose()
        {
            StopVibration();
        }
    }
}
