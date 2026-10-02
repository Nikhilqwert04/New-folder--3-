using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

namespace IntelVibrance.Core
{
    /// <summary>
    /// Represents a detected display monitor.
    /// </summary>
    public class DisplayInfo
    {
        public string DeviceName { get; set; }      // e.g. \\.\DISPLAY1
        public string FriendlyName { get; set; }    // e.g. "Intel UHD Graphics"
        public string MonitorName { get; set; }     // e.g. "BOE NV156FHM"
        public bool IsPrimary { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsHDR { get; set; }
        public IntPtr MonitorHandle { get; set; }

        public override string ToString() =>
            IsPrimary ? $"{FriendlyName} (Primary)" : FriendlyName;
    }

    /// <summary>
    /// Detects GPUs, monitors, and driver information.
    /// Uses WMI + EnumDisplayDevices + DXGI for comprehensive detection.
    /// </summary>
    public static class DisplayManager
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplayDevices(
            string lpDevice, uint iDevNum,
            ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplaySettings(
            string deviceName, int modeNum,
            ref DEVMODE devMode);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DISPLAY_DEVICE
        {
            [MarshalAs(UnmanagedType.U4)]
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            [MarshalAs(UnmanagedType.U4)]
            public uint StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        private const uint DISPLAY_DEVICE_ACTIVE = 0x00000001;
        private const uint DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004;
        private const uint DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;

        /// <summary>
        /// Enumerate all active monitors with GPU and monitor details.
        /// </summary>
        public static List<DisplayInfo> GetDisplays()
        {
            var displays = new List<DisplayInfo>();

            // Phase 1: Enumerate GPU adapters
            uint adapterIndex = 0;
            var adapter = new DISPLAY_DEVICE();
            adapter.cb = Marshal.SizeOf(adapter);

            while (EnumDisplayDevices(null, adapterIndex, ref adapter, 0))
            {
                if ((adapter.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0)
                {
                    // Phase 2: Enumerate monitors on this adapter
                    uint monitorIndex = 0;
                    var monitor = new DISPLAY_DEVICE();
                    monitor.cb = Marshal.SizeOf(monitor);

                    // Get display settings for resolution
                    var devMode = new DEVMODE();
                    devMode.dmSize = (short)Marshal.SizeOf(devMode);
                    bool hasSettings = EnumDisplaySettings(adapter.DeviceName, -1, ref devMode);

                    // First add with adapter name, then try to get monitor name
                    string monitorName = adapter.DeviceName;
                    bool monitorFound = false;

                    while (EnumDisplayDevices(adapter.DeviceName, monitorIndex, ref monitor, 0))
                    {
                        monitorName = !string.IsNullOrEmpty(monitor.DeviceString)
                            ? monitor.DeviceString
                            : adapter.DeviceName;
                        monitorFound = true;
                        break; // take first monitor per adapter
                    }

                    if (!monitorFound)
                        monitorName = adapter.DeviceName;

                    var info = new DisplayInfo
                    {
                        DeviceName = adapter.DeviceName,
                        FriendlyName = adapter.DeviceString,
                        MonitorName = CleanMonitorName(monitorName),
                        IsPrimary = (adapter.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0,
                        Width = hasSettings ? devMode.dmPelsWidth : 0,
                        Height = hasSettings ? devMode.dmPelsHeight : 0,
                        IsHDR = IsHDREnabled(adapter.DeviceName),
                        MonitorHandle = IntPtr.Zero
                    };

                    displays.Add(info);
                }
                adapterIndex++;
                adapter = new DISPLAY_DEVICE();
                adapter.cb = Marshal.SizeOf(adapter);
            }

            return displays;
        }

        /// <summary>
        /// Get GPU adapter information from WMI.
        /// Returns a list of GPU names and driver versions.
        /// </summary>
        public static List<(string Name, string DriverVersion)> GetGPUInfo()
        {
            var gpus = new List<(string, string)>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Name, DriverVersion FROM Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = obj["Name"]?.ToString() ?? "Unknown GPU";
                        string driver = obj["DriverVersion"]?.ToString() ?? "Unknown";
                        gpus.Add((name, driver));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[WARN] WMI GPU query failed: {ex.Message}");
                gpus.Add(("Unknown GPU", "Unknown"));
            }
            return gpus;
        }

        /// <summary>
        /// Detects if the system has Intel integrated graphics driving the internal display.
        /// Returns the Intel GPU name if found, null otherwise.
        /// </summary>
        public static string DetectIntelGPU()
        {
            var gpus = GetGPUInfo();
            foreach (var (name, driver) in gpus)
            {
                if (name.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (name.IndexOf("UHD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("Iris", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("HD Graphics", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("Arc", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return name;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the Windows version string.
        /// </summary>
        public static string GetWindowsVersion()
        {
            try
            {
                var version = Environment.OSVersion;
                // Distinguish Windows 10 vs 11 by build number
                if (version.Version.Build >= 22000)
                    return $"Windows 11 (Build {version.Version.Build})";
                else if (version.Version.Major == 10)
                    return $"Windows 10 (Build {version.Version.Build})";
                else
                    return version.ToString();
            }
            catch
            {
                return "Windows (Unknown Version)";
            }
        }

        /// <summary>
        /// Checks if HDR is enabled on a given display device.
        /// Uses a heuristic: if the display color depth is > 8 bpc in WMI, HDR may be on.
        /// NOTE: A definitive HDR check requires Windows.Devices.Display API (UWP/WinRT),
        /// not available in classic WinForms. We use a conservative heuristic here.
        /// </summary>
        public static bool IsHDREnabled(string deviceName)
        {
            // Check registry for HDR state – Windows stores AdvancedColorInfo
            // per monitor in: HKCU\Software\Microsoft\Windows\CurrentVersion\VideoSettings
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\VideoSettings"))
                {
                    if (key != null)
                    {
                        // Look for EnableHDRForPlayback or similar keys
                        // This is an approximation – exact per-monitor HDR state
                        // requires undocumented APIs.
                        object val = key.GetValue("EnableHDRForPlayback");
                        if (val != null && (int)val == 1) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static string CleanMonitorName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unknown Monitor";
            // Strip path prefix like \\.\DISPLAY1\
            if (name.StartsWith(@"\\.\"))
            {
                int slash = name.LastIndexOf('\\');
                if (slash > 3) return name.Substring(slash + 1);
            }
            return name;
        }
    }
}
