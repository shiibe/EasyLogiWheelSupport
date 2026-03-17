## 1.2.0 (Unreleased)
- Add manual transmission mode (R/N/1..Max Gears), plus tach + gear indicator.
- Add ignition keybind (hold-to-start) with engine-off behavior (kills throttle, mutes engine, stops fuel, turns off radio/lights) and HUD start indicator.
- Engine OFF now plays the same click as toggling headlights off, and extended engine-off time can trigger the game's "truck temperature low" behavior.
- Add `Ignition Time` slider to configure the hold-to-toggle delay.
- Add `Vehicle/HUD` paged menu and reorganize the related options.
- Add Defaults buttons to the Vehicle/HUD pages.
- Add HUD speedometer + units (km/h/mph), and per-element HUD positioning (Bottom L/Bottom R).
- Add headlight tuning sliders (brightness + distance/range).
- Improve headlight shutoff behavior (attempt to hide flare/glow artifacts when lights are off).
- Update speed multipliers to scale vehicle max speed instead of scaling input.
- `Max Gears` now scales gear spacing/top speed instead of limiting top speed; resets to gear 1 on ignition on.
- Fix engine sound pitch compounding; keep overrev ramp but make near-redline warning more audible.
- Allow `DPad` and `M+DPad` binds simultaneously (no duplicate conflict).
- Migrate PlayerPrefs from `G920*` to `ELWS_*` (automatic one-time migration).
- Ignition SFX now loads from `sfx/` folder; ignition OFF SFX support removed.

## 1.0.2
- Update calibration UI copy/placement and wheel-not-detected messaging.
- Fix FancyButton centering.
- Add README config option list.
- Add repo URL to Thunderstore manifest.

## 1.0.1
- Fix README asset links for Thunderstore.

## 1.0.0
- Initial release.

## 0.1.0
- Initial scaffold.
