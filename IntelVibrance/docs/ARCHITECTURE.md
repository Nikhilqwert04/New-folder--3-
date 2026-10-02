# Architecture — Intel Vibrance

## Overview

Intel Vibrance is a single-process Windows desktop application built with:
- **Language:** C# (.NET Framework 4.8)
- **UI Framework:** WinForms (Windows Forms) with custom-painted controls
- **Compiler:** Microsoft Roslyn C# 3.11 (Roslyn csc.exe, packaged via NuGet)
- **Display API:** Windows GDI32 SetDeviceGammaRamp

---

## Project Structure

```
IntelVibrance/
│
├── Program.cs                   ← Entry point, crash handler, single-instance mutex
│
├── Core/
│   ├── Logger.cs                ← File logging with rotation
│   ├── DisplayManager.cs        ← GPU detection, monitor enumeration, driver info
│   ├── ColorTransform.cs        ← Vibrance algorithm (S-curve gamma LUT)
│   ├── VibranceController.cs    ← Apply/restore gamma ramp per display
│   └── StateManager.cs          ← Settings persistence (registry), presets, auto-start
│
├── Windows/
│   └── DisplayAPI.cs            ← P/Invoke: SetDeviceGammaRamp, GetDeviceGammaRamp,
│                                   CreateDC, EnumDisplayMonitors, MONITORINFOEX
│
├── UI/
│   ├── Controls.cs              ← VibranceSlider (custom), DarkButton (custom)
│   └── MainWindow.cs            ← Main window: slider, presets, GPU info, tabs
│
├── docs/
│   ├── TECHNICAL_FINDINGS.md
│   ├── ARCHITECTURE.md          ← This file
│   ├── TESTING.md
│   └── TROUBLESHOOTING.md
│
├── bin/
│   ├── IntelVibrance.exe        ← Compiled output
│   └── compilers/               ← Roslyn compiler (Microsoft.Net.Compilers)
│
└── build.bat                    ← Build script (no Visual Studio required)
```

---

## Component Responsibilities

### Program.cs
- `[STAThread]` entry point
- System.Threading.Mutex for single-instance enforcement
- `AppDomain.CurrentDomain.UnhandledException` handler for crash recovery
- `--startup` command-line flag support (for Windows auto-start)

### Core/Logger.cs
- Static logger with file output to `%LOCALAPPDATA%\IntelVibrance\vibrance.log`
- Thread-safe via lock
- Log rotation at 1 MB
- No external logging library

### Core/DisplayManager.cs
- `GetDisplays()` — Enumerates active monitors via `EnumDisplayDevices`
- `GetGPUInfo()` — GPU name + driver version via WMI `Win32_VideoController`
- `DetectIntelGPU()` — Identifies Intel iGPU by name
- `GetWindowsVersion()` — Detects Windows 10 vs 11 by build number
- `IsHDREnabled()` — Heuristic HDR check via registry

### Core/ColorTransform.cs
- `BuildVibranceRamp(double vibrance)` — Converts 0.0–1.0 vibrance to a 256×3 RAMP
- Algorithm: S-curve contrast + gamma power curve
- `EnsureMonotonic()` — Enforces Windows ramp validity requirement
- `NormalizeVibrance(int)` — Converts 0–100 integer to 0.0–1.0 double

### Core/VibranceController.cs
- Manages per-display original ramp backup via `GetDeviceGammaRamp`
- `Initialize()` — Backs up original ramps for all displays
- `ApplyVibrance()` — Applies computed ramp via `SetDeviceGammaRamp`
- `RestoreDisplay()` / `RestoreAll()` — Restores original ramps
- `EmergencyRestore()` — Static method for crash handler (applies linear ramp via `GetDC(null)`)

### Core/StateManager.cs
- Registry key: `HKCU\Software\IntelVibrance`
- Auto-start: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- Preset definitions: Name → vibrance value tuples

### Windows/DisplayAPI.cs
- All P/Invoke declarations in one place
- RAMP struct (3 × ushort[256])
- `BuildLinearRamp()` — Constructs a neutral gamma ramp
- `IsRampValid()` — Checks monotonicity before applying

### UI/Controls.cs
- `VibranceSlider` — Custom Control with mouse drag, scroll wheel, thumb rendering
- `DarkButton` — Custom Button with hover animation and rounded corners

### UI/MainWindow.cs
- Main window with 3 tabs: Vibrance, Diagnostics, Log
- GPU/display card with real-time detection
- Preset buttons wired to vibrance slider
- System tray integration with NotifyIcon
- Global hotkey via `RegisterHotKey` (Ctrl+Alt+V)
- Dark title bar via `DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE)`

---

## Data Flow

```
User moves slider
     │
     ▼
VibranceSlider.ValueChanged event
     │
     ▼
ColorTransform.BuildVibranceRamp(normalized_value)
     │   Computes 256-entry LUT for R, G, B channels
     ▼
VibranceController.ApplyRampToDisplay(deviceName, ramp)
     │   CreateDC(NULL, "\\\\.\\DISPLAY1", NULL, NULL)
     │   SetDeviceGammaRamp(hdc, ref ramp)
     │   DeleteDC(hdc)
     ▼
Windows Display Driver (Intel UHD)
     │
     ▼
Physical Display
```

---

## Safety Architecture

```
App Start
  │
  ├─ Mutex acquired (single instance)
  │
  ├─ UnhandledException handler registered
  │
  ├─ VibranceController.Initialize()
  │     └─ Saves original ramps for all displays
  │
  ├─ Apply user's saved vibrance
  │
  [Running...]
  │
  ├─ User: Reset button
  │     └─ RestoreDisplay() for selected display
  │
  ├─ Window close → minimizes to tray
  │
  └─ Real exit (via tray menu)
        └─ RestoreAll() for all displays
        └─ UnregisterHotKey()
        └─ Mutex released

Crash path:
  UnhandledException → EmergencyRestore() → linear ramp via GetDC(null)
```

---

## Design Decisions

### Why WinForms and not WinUI 3 or UWP?

- .NET Framework 4.8 with WinForms is available without any SDK installation
- The Roslyn C# compiler can be downloaded as a NuGet package (no Visual Studio needed)
- WinForms provides full access to Win32 P/Invoke for display APIs
- WinUI 3 / UWP require the Windows App SDK which is not pre-installed

### Why SetDeviceGammaRamp?

- Only publicly documented API that provides global display color control on Windows
- Works without vendor-specific driver APIs (Intel has none)
- No screen capture, no frame processing — hardware-level (display LUT)
- Minimum privilege (no admin required)

### Why not inject into Intel driver?

- Intel does not expose a public driver color API
- Undocumented registry manipulation is unstable across driver versions
- DLL injection could be flagged by security software or anti-cheat
