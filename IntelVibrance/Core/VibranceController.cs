using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using IntelVibrance.Windows;

namespace IntelVibrance.Core
{
    /// <summary>
    /// Manages the application of vibrance via Windows gamma ramps.
    /// Handles per-display DC creation, state backup, apply, and restore.
    /// </summary>
    public class VibranceController : IDisposable
    {
        private readonly object _lock = new object();
        private Dictionary<string, DisplayAPI.RAMP> _originalRamps = new Dictionary<string, DisplayAPI.RAMP>();
        private int _currentVibrance = 50; // 0–100, default 50 = neutral
        private bool _isApplied = false;
        private bool _disposed = false;

        public int CurrentVibrance => _currentVibrance;
        public bool IsApplied => _isApplied;

        /// <summary>
        /// Initialize controller. Backs up original gamma ramps for all displays.
        /// MUST be called before any Apply operations.
        /// Returns true if initialization succeeded.
        /// </summary>
        public bool Initialize(List<DisplayInfo> displays)
        {
            lock (_lock)
            {
                try
                {
                    _originalRamps.Clear();
                    foreach (var display in displays)
                    {
                        IntPtr hdc = IntPtr.Zero;
                        bool createdDC = false;
                        try
                        {
                            hdc = DisplayAPI.CreateDC(null, display.DeviceName, null, IntPtr.Zero);
                            createdDC = true;

                            if (hdc == IntPtr.Zero)
                            {
                                Logger.Log($"[WARN] Could not create DC for {display.DeviceName}, trying GetDC(null)");
                                hdc = DisplayAPI.GetDC(IntPtr.Zero);
                                createdDC = false;
                            }

                            var ramp = new DisplayAPI.RAMP(true);
                            if (DisplayAPI.GetDeviceGammaRamp(hdc, ref ramp))
                            {
                                _originalRamps[display.DeviceName] = ramp;
                                Logger.Log($"[INFO] Original gamma ramp saved for {display.DeviceName}");
                            }
                            else
                            {
                                Logger.Log($"[WARN] GetDeviceGammaRamp failed for {display.DeviceName}. Saving linear ramp as fallback.");
                                _originalRamps[display.DeviceName] = DisplayAPI.BuildLinearRamp();
                            }
                        }
                        finally
                        {
                            if (hdc != IntPtr.Zero)
                            {
                                if (createdDC)
                                    DisplayAPI.DeleteDC(hdc);
                                else
                                    DisplayAPI.ReleaseDC(IntPtr.Zero, hdc);
                            }
                        }
                    }
                    Logger.Log($"[INFO] VibranceController initialized. {_originalRamps.Count} display(s) backed up.");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Log($"[ERROR] VibranceController.Initialize failed: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Apply vibrance to a specific display.
        /// vibrance: 0-100
        /// Returns true if the ramp was successfully applied.
        /// </summary>
        public bool ApplyVibrance(DisplayInfo display, int vibrance)
        {
            lock (_lock)
            {
                if (display == null) return false;

                _currentVibrance = Math.Max(0, Math.Min(100, vibrance));
                double normalized = ColorTransform.NormalizeVibrance(_currentVibrance);
                var ramp = ColorTransform.BuildVibranceRamp(normalized);

                bool result = ApplyRampToDisplay(display.DeviceName, ramp);
                if (result)
                {
                    _isApplied = (_currentVibrance != 50);
                    Logger.Log($"[INFO] Vibrance set to {_currentVibrance} on {display.DeviceName}");
                }
                else
                {
                    Logger.Log($"[WARN] Failed to apply vibrance ramp to {display.DeviceName}");
                }
                return result;
            }
        }

        /// <summary>
        /// Apply vibrance to all displays simultaneously.
        /// </summary>
        public bool ApplyVibranceAll(List<DisplayInfo> displays, int vibrance)
        {
            bool allSuccess = true;
            foreach (var display in displays)
            {
                if (!ApplyVibrance(display, vibrance))
                    allSuccess = false;
            }
            return allSuccess;
        }

        /// <summary>
        /// Restore the original gamma ramp for a specific display.
        /// </summary>
        public bool RestoreDisplay(DisplayInfo display)
        {
            lock (_lock)
            {
                if (display == null) return false;
                if (!_originalRamps.TryGetValue(display.DeviceName, out var originalRamp))
                {
                    // No backup → use linear (neutral)
                    originalRamp = DisplayAPI.BuildLinearRamp();
                    Logger.Log($"[WARN] No backup ramp for {display.DeviceName}, restoring linear ramp");
                }

                bool result = ApplyRampToDisplay(display.DeviceName, originalRamp);
                if (result)
                    Logger.Log($"[INFO] Restored original gamma ramp for {display.DeviceName}");
                else
                    Logger.Log($"[WARN] Failed to restore gamma ramp for {display.DeviceName}");

                return result;
            }
        }

        /// <summary>
        /// Restore all displays to their original state.
        /// Called on application exit or by Reset button.
        /// </summary>
        public bool RestoreAll(List<DisplayInfo> displays)
        {
            bool allSuccess = true;
            foreach (var display in displays)
            {
                if (!RestoreDisplay(display))
                    allSuccess = false;
            }
            _isApplied = false;
            Logger.Log("[INFO] All displays restored to original state");
            return allSuccess;
        }

        /// <summary>
        /// Emergency restore: applies linear neutral ramp to all detected displays.
        /// Used when display list is unavailable (crash recovery).
        /// </summary>
        public static bool EmergencyRestore()
        {
            try
            {
                var linearRamp = DisplayAPI.BuildLinearRamp();
                // Try primary display DC
                IntPtr hdc = DisplayAPI.GetDC(IntPtr.Zero);
                if (hdc != IntPtr.Zero)
                {
                    DisplayAPI.SetDeviceGammaRamp(hdc, ref linearRamp);
                    DisplayAPI.ReleaseDC(IntPtr.Zero, hdc);
                    Logger.Log("[INFO] Emergency restore applied via GetDC(null)");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Log($"[ERROR] Emergency restore failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Internal helper: creates a DC for a specific device, applies the ramp, releases DC.
        /// Tries CreateDC first (per-display), falls back to GetDC(null) (global).
        /// </summary>
        private bool ApplyRampToDisplay(string deviceName, DisplayAPI.RAMP ramp)
        {
            // Validate ramp before applying
            if (!DisplayAPI.IsRampValid(ramp))
            {
                Logger.Log($"[WARN] Invalid ramp (non-monotonic) detected for {deviceName}");
                return false;
            }

            IntPtr hdc = IntPtr.Zero;
            bool createdDC = false;
            try
            {
                // Preferred: CreateDC with device name (per-monitor)
                hdc = DisplayAPI.CreateDC(null, deviceName, null, IntPtr.Zero);
                createdDC = true;

                if (hdc == IntPtr.Zero)
                {
                    // Fallback: global desktop DC
                    hdc = DisplayAPI.GetDC(IntPtr.Zero);
                    createdDC = false;
                    Logger.Log($"[WARN] CreateDC failed for {deviceName}, using global DC");
                }

                if (hdc == IntPtr.Zero)
                {
                    Logger.Log($"[ERROR] Cannot obtain any DC for {deviceName}");
                    return false;
                }

                bool result = DisplayAPI.SetDeviceGammaRamp(hdc, ref ramp);
                if (!result)
                {
                    int error = Marshal.GetLastWin32Error();
                    Logger.Log($"[WARN] SetDeviceGammaRamp returned false for {deviceName}. Win32 error: {error}");
                }
                return result;
            }
            finally
            {
                if (hdc != IntPtr.Zero)
                {
                    if (createdDC)
                        DisplayAPI.DeleteDC(hdc);
                    else
                        DisplayAPI.ReleaseDC(IntPtr.Zero, hdc);
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                // Note: caller should call RestoreAll before disposing
            }
        }
    }
}
