using System;
using System.IO;
using Microsoft.Win32;

namespace IntelVibrance.Core
{
    /// <summary>
    /// Persists user settings to the registry and handles auto-start.
    /// Uses HKCU\Software\IntelVibrance for all settings.
    /// </summary>
    public static class StateManager
    {
        private const string RegKey = @"Software\IntelVibrance";
        private const string StartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "IntelVibrance";

        // Setting names
        private const string KeyVibrance = "Vibrance";
        private const string KeyPreset = "Preset";
        private const string KeyAutoStart = "AutoStart";
        private const string KeyDisplayName = "SelectedDisplay";
        private const string KeyHotkeyEnabled = "HotkeyEnabled";
        private const string KeyApplyOnStartup = "ApplyOnStartup";

        // ─────────────────────────────────────────────
        // Load / Save vibrance
        // ─────────────────────────────────────────────
        public static int LoadVibrance()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegKey))
                {
                    if (key != null)
                    {
                        var val = key.GetValue(KeyVibrance, 50);
                        return Math.Max(0, Math.Min(100, (int)val));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[WARN] LoadVibrance failed: {ex.Message}");
            }
            return 50; // Default: neutral
        }

        public static void SaveVibrance(int vibrance)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegKey))
                {
                    key?.SetValue(KeyVibrance, vibrance, RegistryValueKind.DWord);
                }
                Logger.Log($"[INFO] Vibrance saved: {vibrance}");
            }
            catch (Exception ex)
            {
                Logger.Log($"[WARN] SaveVibrance failed: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────
        // Preset names
        // ─────────────────────────────────────────────
        public static string LoadPreset()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegKey))
                {
                    return key?.GetValue(KeyPreset, "Custom")?.ToString() ?? "Custom";
                }
            }
            catch { return "Custom"; }
        }

        public static void SavePreset(string presetName)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegKey))
                {
                    key?.SetValue(KeyPreset, presetName, RegistryValueKind.String);
                }
            }
            catch { }
        }

        // ─────────────────────────────────────────────
        // Auto-start
        // ─────────────────────────────────────────────
        public static bool IsAutoStartEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(StartupKey))
                {
                    return key?.GetValue(AppName) != null;
                }
            }
            catch { return false; }
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(StartupKey, true))
                {
                    if (key == null) return;
                    if (enable)
                    {
                        string exePath = System.Windows.Forms.Application.ExecutablePath;
                        key.SetValue(AppName, $"\"{exePath}\" --startup");
                        Logger.Log($"[INFO] Auto-start enabled: {exePath}");
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                        Logger.Log("[INFO] Auto-start disabled");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[WARN] SetAutoStart failed: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────
        // Hotkey enabled state
        // ─────────────────────────────────────────────
        public static bool IsHotkeyEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegKey))
                {
                    var val = key?.GetValue(KeyHotkeyEnabled, 0);
                    return val != null && (int)val == 1;
                }
            }
            catch { return false; }
        }

        public static void SetHotkeyEnabled(bool enabled)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegKey))
                {
                    key?.SetValue(KeyHotkeyEnabled, enabled ? 1 : 0, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        // ─────────────────────────────────────────────
        // Apply-on-startup flag
        // ─────────────────────────────────────────────
        public static bool ShouldApplyOnStartup()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegKey))
                {
                    var val = key?.GetValue(KeyApplyOnStartup, 1);
                    return val == null || (int)val == 1;
                }
            }
            catch { return true; }
        }

        public static void SetApplyOnStartup(bool apply)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegKey))
                {
                    key?.SetValue(KeyApplyOnStartup, apply ? 1 : 0, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        // ─────────────────────────────────────────────
        // Selected display
        // ─────────────────────────────────────────────
        public static string LoadSelectedDisplay()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegKey))
                {
                    return key?.GetValue(KeyDisplayName, "")?.ToString() ?? "";
                }
            }
            catch { return ""; }
        }

        public static void SaveSelectedDisplay(string deviceName)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegKey))
                {
                    key?.SetValue(KeyDisplayName, deviceName ?? "", RegistryValueKind.String);
                }
            }
            catch { }
        }

        // ─────────────────────────────────────────────
        // Preset definitions
        // ─────────────────────────────────────────────
        public static (string Name, int Value)[] GetPresets() => new[]
        {
            ("Normal",    50),
            ("Gaming",    70),
            ("Valorant",  80),
            ("Minecraft", 65),
            ("Cinema",    60),
            ("Vivid",     90),
        };
    }
}
