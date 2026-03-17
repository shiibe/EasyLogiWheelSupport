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

## What it does
- Adds wheel + pedal input while driving
- Adds Force Feedback (optional)
- Adds an in-game settings panel (`wheel.exe`) since the game has no native bind menu

## Compatibility
- Intended for modern Logitech wheels supported by Logitech G HUB / LGS.
- Tested on: G920
- Likely compatible: G29/G923 and similar Logitech wheels (not guaranteed).

## Features
- Wheel steering + pedals
- Force Feedback (FFB)
- In-game `wheel.exe` settings menu
- Button bindings menu with optional `Modifier` (hold to bind `M+...` for extra inputs)
- D-pad support: menus (cursor) + on-foot movement (8-way)

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

Quick start
1. Install the mod and start the game once.
2. From the main menu, click `wheel.exe`.
3. Go to `Axis Mapping` and confirm steering/throttle/brake/clutch axes look right.
4. If pedals are wrong or stuck, run `Calibration Wizard`.
5. Go to `Bindings` and set `Modifier` + your button bindings.

## Configuration
- Config file: `BepInEx/config/shibe.easydeliveryco.wheel.cfg`

Config migration
- If you previously used `shibe.easydeliveryco.g920.cfg`, the mod will copy it to the new filename on startup.

## In-game menu
- Click `wheel.exe` in the main menu to adjust wheel/FFB/settings.

Bindings
- `wheel.exe` -> `Bindings`
- Use `Prev` / `Next` at the bottom to switch pages.
- Set `Modifier` first (optional).
- When binding an action: hold `Modifier` to set an `M+...` binding (shown as `M+But. N` or `M+DP ...`).
- If you try to reuse an input, you'll get a warning and can `Replace` (unbinds the old one).
- Actions you can bind: `Interact`, `Back`, `Map/Items`, `Pause`, `Jobs`, `Camera`, `Reset`, `Lights`, `Horn`, `Radio Pwr`, `Scan`, `Channels`, `Scan Tog`
- `Jobs` opens the jobs program (it will also pause the game so it opens immediately).
- Binding labels use the Logitech SDK numbering (`But. N`, `DP ...`). If you're unsure which is which, bind by pressing the physical control.

D-pad behavior
- In menus: D-pad moves the fake mouse cursor.
- On foot: D-pad moves the player (8-way/diagonal).
- While driving: D-pad can be bound to actions.
- While binding: D-pad cursor control is disabled so you can bind DP directions.

Calibration
- Only needed if throttle/brake axes read incorrectly (e.g., stuck throttle).
- Use `wheel.exe` -> Calibration.
- Wizard captures: steering center/left/right + throttle/brake/clutch released/pressed.

## Troubleshooting
- Wheel not detected: install Logitech G HUB (or LGS), plug wheel in before launching the game, then reopen `wheel.exe`.
- No FFB: verify `LogitechSteeringWheelEnginesWrapper.dll` is next to `EasyLogiWheelSupport.dll`.
- Pedals stuck / inverted: run `wheel.exe` -> Calibration Wizard.
- Can't bind D-pad: enter binding capture and press a DP direction (cursor movement is disabled on that screen).
- Jobs binding opens nothing: make sure you're not currently inside `wheel.exe` (bindings are disabled while the wheel menu is open).

## Build
- In `G920-Workspace` profile: install `BepInEx-BepInExPack-5.4.2304` so the local reference DLLs exist.
- Build: `dotnet build EasyLogiWheelSupport/EasyLogiWheelSupport.csproj -c Release`
- Package: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 0.1.0`
