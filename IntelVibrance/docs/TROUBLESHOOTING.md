# Troubleshooting — Intel Vibrance

---

## Problem: Display doesn't change when I move the slider

**Cause 1: HDR is enabled**
- Go to Windows Settings → System → Display → Advanced Display
- Check if HDR is listed as "On"
- If yes, the gamma ramp API does not work with HDR
- Solution: Disable HDR, then try again

**Cause 2: Display driver locked gamma ramp**
- Some Intel driver versions or enterprise configurations may prevent gamma changes
- Check the log file: `%LOCALAPPDATA%\IntelVibrance\vibrance.log`
- Look for: `SetDeviceGammaRamp returned false`
- Solution: Update Intel display drivers from Intel's website

**Cause 3: Another application is overriding gamma**
- F.lux, Night Light, and display calibration software modify the gamma ramp
- Disable Night Light in Windows Settings → System → Display → Night Light
- Close F.lux or similar apps

**Cause 4: Exclusive fullscreen application**
- If a game runs in exclusive fullscreen, it may own the gamma ramp
- Switch the game to borderless windowed mode

---

## Problem: The app shows "Generic PnP Monitor" instead of my monitor model

This is normal when the monitor name cannot be read from the display driver.
The vibrance effect still applies to that display regardless of the name shown.

---

## Problem: Display stays saturated after app crashes

The app registers a crash handler that attempts to restore the gamma ramp.
However, if the crash happens very early (before initialization), restoration may not work.

**Solution 1:** Relaunch Intel Vibrance → it will automatically restore neutral state on next launch.

**Solution 2:** Manual restore via PowerShell:
```powershell
# This resets the system gamma ramp to linear (neutral)
Add-Type -TypeDefinition '
using System;
using System.Runtime.InteropServices;
public class GammaReset {
    [DllImport("gdi32.dll")] public static extern bool SetDeviceGammaRamp(IntPtr h, ref Ramp r);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [StructLayout(LayoutKind.Sequential)]
    public struct Ramp {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst=256)] public ushort[] R;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst=256)] public ushort[] G;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst=256)] public ushort[] B;
    }
}
'
$r = New-Object GammaReset+Ramp
$r.R = 0..255 | %{ [ushort]($_ * 256) }
$r.G = $r.R
$r.B = $r.R
$dc = [GammaReset]::GetDC([IntPtr]::Zero)
[GammaReset]::SetDeviceGammaRamp($dc, [ref]$r)
[GammaReset]::ReleaseDC([IntPtr]::Zero, $dc)
Write-Host "Gamma reset to linear"
```

**Solution 3:** Restarting Windows always resets the gamma ramp to the driver default.

---

## Problem: App says "already running" but I don't see it

The app is minimized to the system tray.
- Look for the app icon in the Windows notification area (bottom-right corner)
- Click the "^" arrow to show hidden tray icons
- Double-click the Intel Vibrance icon to restore the window

---

## Problem: Vibrance resets when I wake from sleep

The Intel display driver resets the gamma ramp on some wake events.
This is a known limitation of the `SetDeviceGammaRamp` API.

**Workaround:** Re-apply vibrance by clicking Apply after waking from sleep.
A future version may auto-reapply using a system event listener.

---

## Problem: Exclusive fullscreen game reverts to unsaturated

When a game switches to exclusive fullscreen, it takes direct control of the display pipeline and often resets the gamma ramp.

**Solutions:**
1. Switch the game to "Borderless Windowed" mode (best option)
2. Re-apply vibrance after tabbing in (Ctrl+Alt+V if hotkey is enabled)

---

## Problem: My Intel GPU is not detected

Check the Diagnostics tab for system information.

**Possible causes:**
- Dual-GPU laptop where the active display is driven by the NVIDIA dGPU (not Intel)
- Intel GPU disabled in Device Manager
- Very old Intel graphics (pre-7th gen) may not support standard gamma ramp

**Check:** In Device Manager → Display adapters, confirm Intel GPU is enabled.

---

## Viewing Logs

Logs are stored at: `%LOCALAPPDATA%\IntelVibrance\vibrance.log`

To open:
1. Press Win+R
2. Type: `%LOCALAPPDATA%\IntelVibrance`
3. Open `vibrance.log` in Notepad

Or press Win+R and type: `notepad "%LOCALAPPDATA%\IntelVibrance\vibrance.log"`

---

## Uninstalling

Intel Vibrance is portable and does not use an installer.

To fully remove:
1. Close Intel Vibrance (right-click tray → Exit)
2. Delete the `IntelVibrance` folder
3. Remove auto-start entry (if enabled):
   - Win+R → `regedit`
   - Navigate to `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`
   - Delete `IntelVibrance` entry
4. Remove settings:
   - In Registry Editor, navigate to `HKEY_CURRENT_USER\Software\IntelVibrance`
   - Delete the `IntelVibrance` key
5. Delete log folder: `%LOCALAPPDATA%\IntelVibrance`
