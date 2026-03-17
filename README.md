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
    <a href="#screenshots">Screenshots</a>
    ·
    <a href="#installation">Installation</a>
    ·
    <a href="#configuration">Configuration</a>
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
    <td width="50%"></td>
  </tr>
</table>

## Installation
Dependencies
- `BepInEx-BepInExPack-5.4.2304`

FFB
- Install Logitech G HUB (or older Logitech Gaming Software) so the wheel drivers/components are present.
- `LogitechSteeringWheelEnginesWrapper.dll` must be alongside the plugin DLL (the build/package includes it).

Install
- r2modman/Thunderstore: install and launch the game
- Manual: copy `EasyLogiWheelSupport.dll` to `BepInEx/plugins/EasyLogiWheelSupport/`
- FFB DLL: make sure `LogitechSteeringWheelEnginesWrapper.dll` is alongside `EasyLogiWheelSupport.dll`

## Configuration
- Config file: `BepInEx/config/shibe.easydeliveryco.wheel.cfg`

Config migration
- If you previously used `shibe.easydeliveryco.g920.cfg`, the mod will copy it to the new filename on startup.

In-game menu
- Click `wheel.exe` in the main menu to adjust wheel/FFB settings.

Bindings
- `wheel.exe` -> `Bindings`
- Use `<` / `>` at the bottom to change pages.
- Set `Modifier` first.
- While binding an action, hold `Modifier` to set an `M+...` binding.
- Actions you can bind: `Interact`, `Back`, `Map/Items`, `Pause`, `Jobs`, `Camera`, `Reset`, `Lights`, `Horn`, `Radio Pwr`, `Scan`, `Channels`, `Scan Tog`
- `Jobs` opens the in-game jobs menu from the desktop.

D-pad behavior
- In menus: D-pad moves the fake mouse cursor.
- On foot: D-pad moves the player.
- While driving: use the bindings pages for actions (radio, lights, etc.).

Calibration
- Only needed if throttle/brake axes read incorrectly (e.g., stuck throttle).
- Use `wheel.exe` -> Calibration.

## Build
- In `G920-Workspace` profile: install `BepInEx-BepInExPack-5.4.2304` so the local reference DLLs exist.
- Build: `dotnet build EasyLogiWheelSupport/EasyLogiWheelSupport.csproj -c Release`
- Package: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 0.1.0`
