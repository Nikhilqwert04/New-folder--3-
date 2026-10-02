# Intel Vibrance

> Digital Vibrance utility for Intel integrated graphics on Windows 10/11

---

## What It Does

Intel Vibrance lets you adjust **Digital Vibrance** for your display — a single slider that makes colors appear more vivid or more muted — similar to NVIDIA Control Panel's Digital Vibrance feature, but built for Intel integrated graphics.

```
Digital Vibrance

0% ───────────────●──────── 100%
                  75%
```

Moving the slider:
- **Left (0%)** — Muted, desaturated appearance
- **Center (50%)** — Default, neutral
- **Right (100%)** — Maximum vivid/saturated appearance

The effect applies **globally** to your entire display: desktop, applications, videos, and games.

---

## Supported Hardware

- Intel UHD Graphics (7th gen and newer)
- Intel Iris Xe Graphics
- Intel Iris Plus Graphics
- Any Intel integrated GPU running Windows 10/11

**Works on hybrid laptops** (Intel iGPU + NVIDIA/AMD dGPU) where the internal display is driven by Intel.

---

## How Digital Vibrance Works

Intel Vibrance uses the Windows `SetDeviceGammaRamp` API (GDI32) to apply an S-curve contrast transformation to the display's gamma lookup table. This produces a perceptual vibrance/saturation effect that affects the whole display at the driver level.

**Important:** This is not identical to NVIDIA Digital Vibrance (which uses a proprietary 3×3 color matrix). See `docs/TECHNICAL_FINDINGS.md` for a complete technical explanation.

---

## Installation

### Option A: Run Directly
1. Download `IntelVibrance.exe`
2. Run it — no installation needed
3. The app will appear in the system tray

### Option B: Build from Source
Requirements: Windows 10/11 with .NET Framework 4.8 (built-in) + internet access for the Roslyn compiler

```batch
cd IntelVibrance
build.bat
```

The executable will be in `bin\IntelVibrance.exe`.

---

## Usage

1. **Move the slider** — Changes take effect immediately
2. **Select display** — Choose which monitor to adjust (top of window)
3. **Apply** — Locks in the current value and saves it
4. **Reset** — Returns to neutral (50%)
5. **Toggle** — Quickly switch vibrance on/off with the same button
6. **Presets** — Quick values for different use cases (Normal, Gaming, Valorant, Minecraft, etc.)

### System Tray
- Close the window to minimize to tray
- Double-click tray icon to restore
- Right-click tray icon → "Exit (Restore Display)" to fully quit and restore original display state

---

## Settings

- **Start with Windows** — Automatically start and apply your vibrance on boot
- **Ctrl+Alt+V hotkey** — Toggle vibrance on/off from any application

---

## Gaming Compatibility

| Game Mode | Works? | Notes |
|---|---|---|
| Windowed | ✅ Yes | Full effect |
| Borderless Fullscreen | ✅ Yes (usually) | Recommended for gaming |
| Exclusive Fullscreen | ⚠ Depends | Game may reset gamma |

**Anti-cheat safety:** This app does NOT inject DLLs, hook game functions, or modify game memory. It operates at the Windows display driver level, equivalent to your monitor's OSD controls.

---

## HDR

If HDR is enabled on your display, vibrance may be limited or unavailable. The app will show a warning. Disable HDR in Windows Settings → System → Display for best results with this utility.

---

## Recovery

Intel Vibrance automatically:
1. Saves your original display state when it starts
2. Restores the original state when you exit
3. Performs an emergency restore if it crashes

If your display ever looks wrong after the app exits or crashes, simply relaunch and click **Reset to Default**.

---

## Limitations

- Effect may not work in exclusive fullscreen mode
- Effect may not work with HDR enabled
- Display driver events (sleep/wake, resolution change) may reset the gamma ramp temporarily
- The effect is a gamma-based approximation, not a true driver-level color matrix like NVIDIA Digital Vibrance

See `docs/TECHNICAL_FINDINGS.md` for complete technical details.

---

## Files

```
IntelVibrance/
├── bin/
│   └── IntelVibrance.exe        ← Run this
├── docs/
│   ├── TECHNICAL_FINDINGS.md    ← API research
│   ├── ARCHITECTURE.md          ← Code structure
│   ├── TESTING.md               ← Test plan
│   └── TROUBLESHOOTING.md       ← Common issues
├── Core/                        ← Display engine
├── UI/                          ← WinForms UI
├── Windows/                     ← P/Invoke declarations
└── build.bat                    ← Build script
```

Log file: `%LOCALAPPDATA%\IntelVibrance\vibrance.log`

---

## License

MIT License. Not affiliated with Intel Corporation.
