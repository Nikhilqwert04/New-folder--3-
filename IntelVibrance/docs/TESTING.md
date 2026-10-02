# Test Plan — Intel Vibrance

## Status: In Progress (milestone-based)

---

## Test Environment

| Parameter | Value |
|---|---|
| CPU/iGPU | Intel UHD Graphics |
| dGPU | NVIDIA GeForce RTX 4050 Laptop GPU |
| OS | Windows 11 |
| Display | Internal 1920×1080 |
| HDR | Disabled (for baseline testing) |

---

## 1. Baseline Tests (Display Control)

### Test 1.1 — Application Launch
- [ ] App launches without crash
- [ ] Detects Intel UHD Graphics
- [ ] Detects internal display
- [ ] Saves original gamma ramp
- [ ] Log file created at `%LOCALAPPDATA%\IntelVibrance\vibrance.log`

### Test 1.2 — Vibrance Slider
| Slider Value | Expected Result | Actual | Pass? |
|---|---|---|---|
| 0% | Noticeably muted/desaturated | | |
| 25% | Slightly muted | | |
| 50% | Neutral (no visible change from stock) | | |
| 75% | Noticeably more vivid | | |
| 100% | Maximum vivid | | |

### Test 1.3 — Apply Button
- [ ] Clicking Apply saves the value to registry
- [ ] Value persists after app restart

### Test 1.4 — Reset Button
- [ ] Clicking Reset returns slider to 50%
- [ ] Display returns to neutral appearance

---

## 2. Desktop / Application Tests

### Test 2.1 — Desktop
- [ ] Wallpaper colors change with vibrance
- [ ] Effect visible on Start menu

### Test 2.2 — Chrome / Edge
- [ ] Web content appears more/less saturated with vibrance changes
- [ ] Images are visibly affected

### Test 2.3 — File Explorer
- [ ] Folder icons appear more vivid at high vibrance
- [ ] Effect visible on colored files

### Test 2.4 — Video Player (e.g., Windows Media Player, VLC)
- [ ] Video appears more saturated at high vibrance
- [ ] Effect applies to SDR video content

---

## 3. Gaming Tests

> ⚠ These tests require the games to be installed. Mark as N/A if not available.

### Test 3.1 — Minecraft (Java or Bedrock)
| Mode | Vibrance Visible? | Notes |
|---|---|---|
| Windowed | | |
| Borderless | | |
| Fullscreen | | |

### Test 3.2 — CS2 (Counter-Strike 2)
| Mode | Vibrance Visible? | Notes |
|---|---|---|
| Windowed | | |
| Borderless | | |
| Fullscreen | | |

### Test 3.3 — Valorant
| Mode | Vibrance Visible? | Notes |
|---|---|---|
| Windowed | | |
| Borderless | | |
| Fullscreen | | |

> Anti-cheat note: Vanguard is Valorant's anti-cheat. Intel Vibrance does NOT inject DLLs or hook processes.

### Test 3.4 — Other DirectX game
Game: ________________________

| Mode | Vibrance Visible? | Notes |
|---|---|---|
| Windowed | | |
| Borderless | | |
| Fullscreen | | |

---

## 4. Recovery Tests

### Test 4.1 — Normal Exit
- [ ] Display returns to original state after closing via tray "Exit" option
- [ ] Gamma ramp restored to pre-app values

### Test 4.2 — Reset Button
- [ ] Reset button restores 50% neutral state
- [ ] Display looks unchanged from pre-app state

### Test 4.3 — Crash Recovery
Procedure: Set vibrance to 100%, then kill `IntelVibrance.exe` via Task Manager
- [ ] Emergency restore attempted (check log)
- [ ] Display returns to neutral OR leaves saturated (document actual result)
- [ ] On next launch: app starts cleanly

### Test 4.4 — Windows Restart
- [ ] Reboot machine with app NOT set to auto-start
- [ ] Display returns to default after reboot (Windows clears gamma on reboot)
- [ ] ✅ Expected result: pass (Windows resets gamma ramp naturally on restart)

---

## 5. Auto-Start Tests

### Test 5.1 — Enable Auto-Start
- [ ] Check "Start with Windows" in app settings
- [ ] Verify entry in `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`
- [ ] Reboot and verify app starts automatically

### Test 5.2 — Disable Auto-Start
- [ ] Uncheck "Start with Windows"
- [ ] Verify entry removed from registry
- [ ] Reboot and verify app does NOT start

---

## 6. Hotkey Tests

### Test 6.1 — Register Hotkey
- [ ] Enable hotkey in settings
- [ ] Press Ctrl+Alt+V from another application
- [ ] Vibrance toggles off (display returns to neutral)

### Test 6.2 — Toggle Hotkey
- [ ] Press Ctrl+Alt+V again
- [ ] Vibrance restores to previous value

---

## 7. HDR Tests

### Test 7.1 — HDR On
- [ ] Enable HDR in Windows Settings → System → Display
- [ ] Launch Intel Vibrance
- [ ] App shows HDR warning
- [ ] App does NOT apply gamma ramp (or shows appropriate message)
- [ ] Display not corrupted

### Test 7.2 — HDR Off
- [ ] Disable HDR
- [ ] App shows "Digital Vibrance Available" status
- [ ] Vibrance works normally

---

## 8. Multiple Display Tests

### Test 8.1 — External Monitor Connected
- [ ] App detects multiple displays in dropdown
- [ ] Selecting different display applies vibrance to that display
- [ ] Primary and secondary displays can have independent vibrance (if supported)

---

## 9. Diagnostics Tab Tests

- [ ] Windows version shown correctly
- [ ] GPU name shown (Intel UHD + NVIDIA if present)
- [ ] Driver version shown
- [ ] Display device name shown
- [ ] HDR state shown correctly
- [ ] Log tab shows recent entries

---

## 10. Performance Tests

### Test 10.1 — CPU Usage
- [ ] CPU usage < 0.5% while idle (no slider movement)
- [ ] CPU spike during slider drag is brief and small

### Test 10.2 — Memory Usage
- [ ] RAM usage < 30 MB

### Test 10.3 — Gaming FPS Impact
Game: _________________ (set to borderless, vibrance 75%)
- [ ] FPS before Intel Vibrance: ______
- [ ] FPS with Intel Vibrance running: ______
- [ ] Difference < 2 FPS ✅

---

## Results Summary

| Category | Tests | Passed | Failed | N/A |
|---|---|---|---|---|
| Display Control | 4 | | | |
| Desktop Apps | 4 | | | |
| Gaming | 4+ | | | |
| Recovery | 4 | | | |
| Auto-Start | 2 | | | |
| Hotkey | 2 | | | |
| HDR | 2 | | | |
| Diagnostics | 1 | | | |
| Performance | 3 | | | |

---

## Known Limitations (Pre-documented)

1. Exclusive fullscreen mode may not respect gamma ramp changes
2. HDR mode makes gamma ramp effect undefined
3. Display driver events may temporarily reset the ramp
4. Effect is perceptual S-curve, not a true color matrix (different from NVIDIA)
