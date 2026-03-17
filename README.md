<h1>
<p align="center">
  <img src="https://raw.githubusercontent.com/shiibe/EasyLogiWheelSupport/refs/heads/main/assets/icon.png" alt="Logo" width="128">
  <br>EasyLogiWheelSupport
</h1>
  <p align="center">
    Logitech wheel support for Easy Delivery Co.
    <br />
    <a href="#about">About</a>
    ·
    <a href="#features">Features</a>
    ·
    <a href="#compatibility">Compatibility</a>
    ·
    <a href="#screenshots">Screenshots</a>
    ·
    <a href="#installation">Installation</a>
    ·
    <a href="#configuration">Configuration</a>
    ·
    <a href="#in-game-menu">In-game menu</a>
    ·
    <a href="#troubleshooting">Troubleshooting</a>
    ·
    <a href="#build">Build</a>
  </p>
</p>
<hr/>

## About
This is a BepInEx + Harmony mod intended to add Logitech wheel input support to Easy Delivery Co.

Tested on a G920, but it should work with most (if not all) modern Logitech wheels supported by Logitech G HUB / LGS.

This mod uses Logitech's Steering Wheel SDK wrapper for Force Feedback (FFB).

## Features
- Wheel steering + pedals
- Force Feedback (FFB)
- In-game `wheel.exe` settings menu
- Button bindings menu with optional `Modifier` (hold to bind `M+...` for extra inputs)
- D-pad support: menus (cursor) + on-foot movement (8-way)

## Compatibility
- Intended for modern Logitech wheels supported by Logitech G HUB / LGS.
- Tested on: G920
- Likely compatible: G29/G923 and similar Logitech wheels (not guaranteed).

## Screenshots
<table>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyLogiWheelSupport/refs/heads/main/assets/screenshots/1.jpg" alt="Screenshot 1" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyLogiWheelSupport/refs/heads/main/assets/screenshots/2.jpg" alt="Screenshot 2" width="100%"></td>
  </tr>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyLogiWheelSupport/refs/heads/main/assets/screenshots/3.jpg" alt="Screenshot 3" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyLogiWheelSupport/refs/heads/main/assets/screenshots/4.jpg" alt="Screenshot 4" width="100%"></td>
  </tr>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyLogiWheelSupport/refs/heads/main/assets/screenshots/5.jpg" alt="Screenshot 5" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyLogiWheelSupport/refs/heads/main/assets/screenshots/6.jpg" alt="Screenshot 6" width="100%"></td>
  </tr>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyLogiWheelSupport/refs/heads/main/assets/screenshots/7.jpg" alt="Screenshot 7" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyLogiWheelSupport/refs/heads/main/assets/screenshots/8.jpg" alt="Screenshot 8" width="100%"></td>
  </tr>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyLogiWheelSupport/refs/heads/main/assets/screenshots/9.jpg" alt="Screenshot 9" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyLogiWheelSupport/refs/heads/main/assets/screenshots/10.jpg" alt="Screenshot 10" width="100%"></td>
  </tr>
</table>

## Installation
**Dependencies**
- `BepInEx-BepInExPack-5.4.2304`

**Force Feedback (FFB)**
- Install Logitech G HUB (or older Logitech Gaming Software) so the wheel drivers/components are present.
- `LogitechSteeringWheelEnginesWrapper.dll` must be alongside the plugin DLL (the build/package includes it).

**Install**
- r2modman/Thunderstore: install and launch the game
- Manual: copy `EasyLogiWheelSupport.dll` to `BepInEx/plugins/EasyLogiWheelSupport/`
- FFB DLL: make sure `LogitechSteeringWheelEnginesWrapper.dll` is alongside `EasyLogiWheelSupport.dll`

**Quick start**
1. Install the mod and start the game once.
2. From the main menu, click `wheel.exe` to open the settings menu.
3. Go to `Axis Mapping` and confirm steering/throttle/brake/clutch axes look right.
4. If pedals are wrong or stuck, run `Calibration -> Calibrate` in the `wheel.exe` menu.
5. Go to `Bindings` and set `Modifier` + your button bindings.

## Configuration
- Config file: `BepInEx/config/shibe.easydeliveryco.logiwheel.cfg`

**General**
- `enable_mod` (bool, default: `true`): Enables/disables the mod entirely.
- `ignore_xinput_controllers` (bool, default: `true`): Pass `ignoreXInputControllers` to the Logitech SDK init (recommended).

**Menu**
- `show_wheel_menu_icon` (bool, default: `true`): Show/hide the Wheel Settings icon on the Main Menu.
- `wheel_menu_icon_x` (string, default: `"4"`): Main Menu icon X position. Example: `4`
- `wheel_menu_icon_y` (string, default: `"3.25"`): Main Menu icon Y position. Example: `3.25`

**Debug**
- `log_detected_devices` (bool, default: `true`): Log joystick names detected by Unity on startup.
- `debug_logging` (bool, default: `false`): Log debug information.

## In-game menu
- Click `wheel.exe` in the main menu to adjust wheel/FFB/settings.

**Calibration**
- Only needed if throttle/brake axes read incorrectly (e.g., stuck throttle).
- Use `wheel.exe` -> Calibration.
- Follow the instructions to calibrate the pedals. This should fix any issues with wrong or stuck axes. Make sure to hit the "Capture" button at each step to record the axis values.

**Bindings**
- You can set button bindings in `wheel.exe` -> Bindings.
- The `Modifier` option allows you to set a "hold to modify" button. When you hold the modifier, you can use the same buttons to trigger different inputs (e.g., `M+A` for `Modifier + A`).

## Troubleshooting
**My wheel/pedals aren't working or are stuck at full throttle.**
- Make sure Logitech G HUB (or older Logitech Gaming Software) is installed so the drivers/components are present.
- Check `wheel.exe` -> `Axis Mapping` to see if axes are detected correctly.
- If throttle/brake axes are wrong or stuck, got to `wheel.exe` -> `Calibration` and follow the instructions to calibrate.

**The SDK fails to load or I get errors about missing DLLs.**
- Make sure `LogitechSteeringWheelEnginesWrapper.dll` is in the same folder as `EasyLogiWheelSupport.dll`. The package includes it, but if you're building manually you need to copy it over.
- Make sure you have the latest Logitech G HUB (or older Logitech Gaming Software) installed, as the wrapper depends on the official Logitech SDK components.

**The in-game menu doesn't show up or I can't open it.**
- The menu is accessed by clicking `wheel.exe` from the main menu. If it's not there, make sure the icon is enabled in the mod config and that the plugin is loaded correctly (check BepInEx logs for any errors).

## Build
- Build: `dotnet build EasyLogiWheelSupport/EasyLogiWheelSupport.csproj -c Release`
- Package: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 1.0.2`
