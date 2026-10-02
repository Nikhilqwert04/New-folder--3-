# Technical Findings: Windows Display Color Control on Intel Integrated Graphics

## Date: 2026-10-02
## System Under Test: Intel UHD Graphics + NVIDIA RTX 4050 (Hybrid Laptop)

---

## 1. APIs Tested

### 1.1 SetDeviceGammaRamp / GetDeviceGammaRamp (GDI32)

**Status: IMPLEMENTED — FUNCTIONAL**

```c
BOOL SetDeviceGammaRamp(HDC hDC, LPVOID lpRamp);
BOOL GetDeviceGammaRamp(HDC hDC, LPVOID lpRamp);
```

- Source: `gdi32.dll`
- Operates on a 256-entry 16-bit LUT for each of Red, Green, Blue channels
- Each entry maps input intensity → output intensity
- Can be applied per-display using `CreateDC(NULL, "\\\\.\\DISPLAY1", NULL, NULL)`
- Default/neutral ramp: `ramp[i] = i * 256` for each channel
- **Confirmed working on Intel UHD Graphics on this system**

**What it can do:**
- Adjust brightness/contrast via power curves
- Apply S-curve contrast effects (perceptually similar to vibrance at moderate levels)
- Apply gamma correction
- Work globally for desktop and applications

**What it CANNOT do:**
- Apply a full 3×3 color transformation matrix (cross-channel terms are not possible)
- Provide true NVIDIA-style "Digital Vibrance" (which uses a color matrix in the driver)

**Limitations:**
- Windows applies internal heuristics and may silently reject "unsafe" ramps (e.g. inverted, all-zero)
- The ramp is global to the display device, not per-window or per-application
- Can be reset by: display driver events, resolution changes, monitor disconnect, Night Light toggle
- Behavior in exclusive fullscreen mode: inconsistent (games may own the display pipeline)
- Undefined behavior if HDR is enabled

### 1.2 CreateDC per Display Device

**Status: IMPLEMENTED — FUNCTIONAL**

```c
HDC CreateDC(NULL, "\\\\.\\DISPLAY1", NULL, NULL);
```

Allows targeting a specific monitor by device name rather than the global desktop.
On this system, `\\.\DISPLAY1` corresponds to the Intel UHD internal display.

### 1.3 DXGI (DirectX Graphics Infrastructure)

**Status: INVESTIGATED — NOT SUITABLE FOR THIS USE CASE**

DXGI provides `IDXGISwapChain::SetGammaControl()` and related interfaces.
However, these operate on a per-swap-chain basis (i.e., per-application), not globally.
DXGI gamma cannot be set for the entire desktop or for other applications' windows.

`IDXGIOutput::GetGammaControl()` / `SetGammaControl()` requires exclusive ownership of the output,
which is not available on a shared desktop.

### 1.4 Intel Graphics Command Center (IGCC) API

**Status: NO PUBLIC API EXISTS**

Intel does not expose a public programmatic API for color settings (saturation, vibrance, hue).
The IGCC application is a closed UWP application that stores settings in internal app data.
Intel provides no SDK, COM interface, or command-line interface for driver color controls.

Registry-based approaches have been reported by community users:
- Registry keys under `HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-...}`
- These are undocumented, driver-version specific, and unreliable
- NOT implemented in this application due to risk of driver instability

### 1.5 Windows Color System (WCS) / ICC Profiles

**Status: INVESTIGATED — INDIRECT USE**

ICC color profiles can define display color behavior. However:
- ICC profiles are applied by color-managed applications only
- Non-color-managed apps (most desktop apps, games) do not use ICC profile transforms
- ICC profiles cannot be reliably used for real-time vibrance adjustment
- Standard ICC profiles cannot encode a saturation boost without custom LUT manipulation

### 1.6 Windows Display Configuration API (CCD)

**Status: INVESTIGATED — NOT APPLICABLE**

`SetDisplayConfig()` and related APIs control display topology (resolution, refresh rate, rotation).
They do not control color transformation parameters.

### 1.7 Direct3D 11/12 Gamma

**Status: NOT APPLICABLE GLOBALLY**

Like DXGI, D3D gamma control is per-swap-chain and cannot affect other applications.

### 1.8 Intel oneAPI / IGC Driver SDK

**Status: NO PUBLIC SDK FOR COLOR MANAGEMENT**

Intel's developer SDKs (oneAPI, Level Zero) target compute workloads (AI/ML), not display color management.

### 1.9 HDR / Advanced Color Pipeline

**Status: INVESTIGATED — UNSUPPORTED WITH GAMMA RAMP**

When Windows Advanced Color (HDR) is enabled:
- The display operates in a high bit-depth color space
- `SetDeviceGammaRamp` behavior is undefined and may have no effect
- This application detects HDR state and warns the user

---

## 2. Results Summary

| API | Works? | Global Effect? | Intel GPU? | Requires Admin? | Games (Borderless)? | Games (Exclusive FS)? | HDR? |
|---|---|---|---|---|---|---|---|
| SetDeviceGammaRamp | ✅ Yes | ✅ Global | ✅ Yes | ❌ No | ✅ Yes | ⚠ Usually No | ❌ No |
| DXGI SetGammaControl | ⚠ Per-app only | ❌ No | ✅ Yes | ❌ No | ❌ No | ❌ No | N/A |
| Intel IGCC API | ❌ No public API | — | — | — | — | — | — |
| ICC Profile (WCS) | ⚠ Color-managed only | ❌ No | ✅ Yes | ❌ No | ❌ No | ❌ No | Partial |
| SetDisplayConfig | ❌ No color params | — | — | — | — | — | — |

**Selected Implementation: SetDeviceGammaRamp**

This is the most universally accessible display-level color transformation available to user-mode applications on Windows without vendor-specific driver APIs.

---

## 3. Implementation: Vibrance via Gamma Ramp

### Algorithm

True NVIDIA "Digital Vibrance" applies a 3×3 color transformation matrix to the YCbCr or RGB color space before the signal leaves the GPU. This multiplies color saturation by boosting the chroma channels while preserving luminance.

Because Windows only provides per-channel LUTs (R → R', G → G', B → B') via `SetDeviceGammaRamp`, cross-channel terms are impossible. We cannot compute "new_Red = 1.2*R - 0.1*G - 0.1*B" through a per-channel LUT.

The implemented approach:

1. **S-curve contrast adjustment** — An S-curve around the midpoint increases perceived contrast and color saturation perceptually without changing color hue.
2. **Gamma power curve** — A mild gamma adjustment complements the S-curve.

### Slider Mapping

| Slider Value | Effect |
|---|---|
| 0% | Desaturated/muted — contrast compressed toward midpoint |
| 50% | Neutral — linear ramp, no transformation applied |
| 75% | Noticeably more vivid — moderate S-curve applied |
| 100% | Maximum — strong S-curve contrast applied |

### Difference from NVIDIA Digital Vibrance

| Feature | NVIDIA Digital Vibrance | This Implementation |
|---|---|---|
| Mechanism | Full 3×3 color matrix in GPU driver | Per-channel S-curve gamma LUT |
| Cross-channel | Yes (R can affect G, B) | No |
| Color hue preservation | Very accurate | Good at moderate levels |
| Effectiveness in exclusive fullscreen | Yes (driver level) | Inconsistent |
| API | NVAPI (proprietary) | GDI32 SetDeviceGammaRamp (public) |
| Intel support | N/A | Yes |

---

## 4. Desktop / Application Coverage

**What the effect applies to:**
- Desktop background ✅
- Windows Explorer ✅
- Chrome / Edge / Firefox ✅
- Video players (windowed) ✅
- Games in windowed mode ✅
- Games in borderless fullscreen ✅ (usually — depends on game DPI awareness)

**What may NOT be affected:**
- Exclusive fullscreen games ⚠ (the game may reset gamma when taking display ownership)
- HDR content ❌
- Hardware video overlay ⚠

---

## 5. Gaming Notes

**Borderless Fullscreen (recommended for this app):**
The gamma ramp persists because Windows still controls the desktop composition.
Most modern games (Valorant in windowed borderless, CS2, Minecraft) work in this mode.

**Exclusive Fullscreen:**
When a game takes exclusive control of the display, it may reset the gamma ramp or bypass it entirely.
Results depend on the game's DirectX version and display ownership behavior.

**Anti-cheat (Vanguard, VAC, EAC):**
This implementation does NOT:
- Inject DLLs into any game process
- Hook any game function
- Modify any game file
- Read or write game memory

The gamma ramp is applied at the Windows display driver level, not at the game level.
This is equivalent to changing gamma in your monitor's OSD — it is display-level, not game-level.

---

## 6. HDR Behavior

When HDR / Advanced Color is enabled:
- `SetDeviceGammaRamp` has undefined behavior
- On some systems it may have no effect at all
- On others it may corrupt the display pipeline (black screen / color distortion)
- This application detects HDR state and warns the user
- The application does NOT apply the gamma ramp when HDR is detected

---

## 7. Administrator Privileges

**Not required.**

`SetDeviceGammaRamp` operates on the user session's display device context.
Standard user privileges are sufficient.

However, if the Intel driver or Group Policy locks gamma ramp changes (rare in enterprise environments),
`SetDeviceGammaRamp` will silently fail.

---

## 8. Driver Compatibility Notes

Tested on:
- Intel UHD Graphics, Driver: 32.0.101.7088
- Windows 11

Known behavior:
- Intel drivers may reset the gamma ramp during driver events (display configuration changes, sleep/wake)
- The application would need to re-apply after such events (periodic re-apply is a potential future feature)

---

## 9. Honest Assessment

This application provides the best available system-level display color adjustment for Intel integrated graphics on Windows where no public Intel driver color API exists.

The result is:
- **Real, visible change to the physical display** ✅
- **Global effect** (desktop + applications + borderless games) ✅
- **No screen capture or per-frame processing** ✅
- **Lightweight** ✅
- **Safe restoration on exit** ✅
- **NOT identical to NVIDIA Digital Vibrance** (different mechanism) ✅ (honestly disclosed)
- **May not work in exclusive fullscreen** ✅ (honestly disclosed)
- **May not work with HDR enabled** ✅ (honestly disclosed)
