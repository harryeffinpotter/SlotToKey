# QuickSelect

Bind any item to any button combo. Press it, and your character instantly equips it — no matter where it is in your inventory.

## The problem

You're in Skull Cavern, health is dropping, and you're fumbling through three toolbar rows trying to find your food. You die. Again.

## The fix

One button combo. Food is eaten, you're back on your sword. Never happened.

## Features

- **Bind any item to any key combo** — L1+Circle for your sword, R1+Triangle for food, whatever you want
- **Finds items across all rows** — doesn't matter where you put it, QuickSelect hunts it down
- **Auto-eat food & swap back** — press the combo, food gets eaten, you're back on your weapon automatically
- **Auto-place bombs & swap back** — drop a bomb mid-fight without losing your sword
- **Full keyboard + controller support** — works with any combo on both
- **Bindings persist** — saved to config.json, survives restarts

## How to use

1. Hold your item, press **F1** (keyboard) or **hold Select** (controller)
2. Press your desired combo
3. Done. That combo now equips that item forever.

Hold **~** (tilde) to clear all bindings.

## Config

Everything's configurable in [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) or `config.json`.

| Setting | Default | Description |
|---|---|---|
| Bind Mode Key | F1 | Keyboard key to enter bind mode |
| Bind Mode Button | Select | Controller button (hold to bind) |
| Clear All Bindings Key | ~ | Wipe all keybinds |
| Auto-Use & Swap Back | ON | Auto-eat/place consumables and swap back |

Advanced options (modifier keys, directional buttons, disabled buttons, extra consumable IDs) can be edited in `config.json`.

## Requirements

- Stardew Valley 1.6+
- SMAPI 4.0+
- [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) (optional)

## Install

1. Install [SMAPI](https://smapi.io/)
2. Drop the `QuickSelect` folder into your `Stardew Valley/Mods` directory
3. Launch the game
