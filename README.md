<h1>
<p align="center">
  <br>EasyDeliveryCoG920
</h1>
  <p align="center">
    Logitech G920 support for Easy Delivery Co.
    <br />
    <a href="#about">About</a>
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
This is a BepInEx + Harmony mod intended to add Logitech G920 wheel input support to Easy Delivery Co.

This mod uses Logitech's Steering Wheel SDK wrapper for Force Feedback (FFB).

## Installation
Dependencies
- `BepInEx-BepInExPack-5.4.2304`

FFB
- Install Logitech G HUB (or older Logitech Gaming Software) so the wheel drivers/components are present.
- `LogitechSteeringWheelEnginesWrapper.dll` must be alongside the plugin DLL (the build/package includes it).

Install
- r2modman/Thunderstore: install and launch the game
- Manual: copy `EasyDeliveryCoG920.dll` to `BepInEx/plugins/EasyDeliveryCoG920/`

## Configuration
- Config file: `BepInEx/config/shibe.easydeliveryco.g920.cfg`

In-game menu
- Click `wheel.exe` in the main menu to adjust wheel/FFB settings.

## Build
- In `G920-Workspace` profile: install `BepInEx-BepInExPack-5.4.2304` so the local reference DLLs exist.
- Build: `dotnet build EasyDeliveryCoG920/EasyDeliveryCoG920.csproj -c Release`
- Package: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 0.1.0`
