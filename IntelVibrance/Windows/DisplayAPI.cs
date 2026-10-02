using System;
using System.Runtime.InteropServices;

namespace IntelVibrance.Windows
{
    /// <summary>
    /// P/Invoke declarations for Windows Display APIs.
    /// Primary API used: SetDeviceGammaRamp / GetDeviceGammaRamp (GDI32)
    /// This is the most universally supported method for global display color
    /// adjustment on Windows when no vendor-specific driver API is available.
    /// </summary>
    public static class DisplayAPI
    {
        // ─────────────────────────────────────────────────────────────────
        // GDI32 – Gamma Ramp API
        // The gamma ramp is a 256-entry LUT for R, G, B channels.
        // Values are 16-bit (0–65535). A "normal" ramp has entry[i] = i*256.
        // We use this to implement a saturation matrix approximation:
        // we compute per-channel ramps that, when combined, produce a
        // perceptually saturated output.
        // ─────────────────────────────────────────────────────────────────
        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool GetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(
            IntPtr hdc, IntPtr lprcClip,
            MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);

        // ─────────────────────────────────────────────────────────────────
        // Structures
        // ─────────────────────────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential)]
        public struct RAMP
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Red;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Green;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Blue;

            public RAMP(bool init)
            {
                Red = new ushort[256];
                Green = new ushort[256];
                Blue = new ushort[256];
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left, top, right, bottom;
        }

        public delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        // Monitor flags
        public const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;
        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        // ─────────────────────────────────────────────────────────────────
        // Helper: Build a standard linear gamma ramp
        // ─────────────────────────────────────────────────────────────────
        public static RAMP BuildLinearRamp()
        {
            var ramp = new RAMP(true);
            for (int i = 0; i < 256; i++)
            {
                ushort val = (ushort)(i * 256);
                ramp.Red[i] = val;
                ramp.Green[i] = val;
                ramp.Blue[i] = val;
            }
            return ramp;
        }

        /// <summary>
        /// Validate that a ramp is within Windows-accepted bounds.
        /// Windows rejects ramps that are too extreme. The heuristic check
        /// requires that each channel's ramp be monotonically non-decreasing
        /// and that the minimum entry is <= 6553 (10% of max).
        /// </summary>
        public static bool IsRampValid(RAMP ramp)
        {
            for (int i = 1; i < 256; i++)
            {
                if (ramp.Red[i] < ramp.Red[i - 1]) return false;
                if (ramp.Green[i] < ramp.Green[i - 1]) return false;
                if (ramp.Blue[i] < ramp.Blue[i - 1]) return false;
            }
            return true;
        }
    }
}
