using System;
using System.IO;
using UnityEngine;

namespace DuckovHaptics
{
    [Serializable]
    public class HapticSettings
    {
        public bool enabled = true;
        public float lowFrequencyIntensity = 0.5f;
        public float highFrequencyIntensity = 0.5f;
        public int durationMs = 80;
    }

    [Serializable]
    public class HapticsConfig
    {
        public bool enabled = true;
        public int controllerIndex = 0;
        public HapticSettings fireHaptics = new HapticSettings
        {
            enabled = true,
            lowFrequencyIntensity = 0.6f,
            highFrequencyIntensity = 0.8f,
            durationMs = 80
        };
        public HapticSettings reloadHaptics = new HapticSettings
        {
            enabled = true,
            lowFrequencyIntensity = 0.3f,
            highFrequencyIntensity = 0.2f,
            durationMs = 150
        };
        public HapticSettings damageHaptics = new HapticSettings
        {
            enabled = true,
            lowFrequencyIntensity = 0.9f,
            highFrequencyIntensity = 0.5f,
            durationMs = 200
        };
        public string fireKey = "Mouse0";

        private static string ConfigPath => Path.Combine(
            Application.persistentDataPath,
            "DuckovHaptics",
            "config.json"
        );

        public static HapticsConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var config = JsonUtility.FromJson<HapticsConfig>(json);
                    Debug.Log("[DuckovHaptics] Config loaded successfully");
                    return config ?? CreateDefault();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovHaptics] Failed to load config: {ex.Message}");
            }

            return CreateDefault();
        }

        public void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }

                string json = JsonUtility.ToJson(this, prettyPrint: true);
                File.WriteAllText(ConfigPath, json);
                Debug.Log("[DuckovHaptics] Config saved successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DuckovHaptics] Failed to save config: {ex.Message}");
            }
        }

        private static HapticsConfig CreateDefault()
        {
            var config = new HapticsConfig();
            config.Save();
            Debug.Log("[DuckovHaptics] Created default config");
            return config;
        }
    }
}
