<p align="center"><img src="docs/images/banner.png" alt="Adam Smasher and his weapon rewards" width="100%"></p>

# Adam Smasher

**A late-endgame boss for Terraria with Calamity, inspired by Cyberpunk.**

**English** | [Russian](README.ru.md)

[![Release](https://img.shields.io/github/v/release/Doffi4/adam-smasher-tmodloader?style=flat-square&color=e9993c)](https://github.com/Doffi4/adam-smasher-tmodloader/releases/latest)
![Terraria](https://img.shields.io/badge/Terraria-1.4.4.9-658949?style=flat-square)
[![License](https://img.shields.io/badge/license-MIT-c9b791?style=flat-square)](LICENSE.txt)

Learn missile strike patterns, dodge aimed gunfire and survive Sandevistan dash sequences. Defeat Adam Smasher to earn one of two weapons: a homing magic launcher or expanding melee blades.

**[Download the mod](https://github.com/Doffi4/adam-smasher-tmodloader/releases/latest/download/AdamSmasher.tmod)** | [Release notes and files](https://github.com/Doffi4/adam-smasher-tmodloader/releases/latest)

## Install

1. Install **Calamity Mod** in tModLoader.
2. Download `AdamSmasher.tmod` and copy it to your profile's `Mods` folder. You can open this folder from tModLoader's Mods menu.
3. Enable Adam Smasher and restart tModLoader.

**Multiplayer: the server and every player must use the same mod version. The current release is 0.1.5.**

Tested with **tModLoader 2026.08.3.0 / Terraria 1.4.4.9 / Calamity 2.2.2**. BossChecklist is optional; its integration was checked with 2.2.4. Calamity Mod Music 2.1 was present in the test setup but is not required by this mod.

Existing worlds are supported; no new world generation is needed.

## Summon the boss

Craft the reusable **Arasaka Beacon** at **Draedon's Forge**:

| Ingredient | Amount |
| --- | ---: |
| Shadowspec Bar | 5 |
| Mysterious Circuitry | 25 |
| Dubious Plating | 25 |

Use the beacon while no other boss is active. The Shadowspec recipe places this encounter in late-endgame Calamity progression.

## The encounter

- Missile strikes mark their impact zones before detonating: three zones in phase one, five in phase two.
- Gun bursts and Sandevistan dashes have separate warnings.
- The final aim position stays fixed for 24 game ticks, giving you a window to dodge.
- Phase two chains missile strikes into a new, separately warned gun burst.

Base health is **18 million**, with **110 defense**. Base dash contact damage is **600**; bullets and explosions start at **350 / 450 damage**, before difficulty scaling, defense and other mods. Contact damage applies only during active dashes. Balance still needs live gameplay feedback.

## Weapons and loot

Every reward roll gives **exactly one weapon**, with a **50% chance for either weapon**. Rolls are independent, so duplicates are possible. Normal mode drops the weapon directly; Expert and Master use the boss treasure bag.

| Weapon | Left click | Right click |
| --- | --- | --- |
| **Annihilation Protocol** (magic) | Fire two homing missiles with area explosions. Base cost: 10 mana per pair. | Hold for 1-2 seconds to release a heavy plasma shot with an animated damage field. Charging also consumes mana. |
| **Arasaka Mantis Blades** (melee) | Hold to build up a three-hit combo. Over 2 seconds, damage increases by up to 50% and reach grows from 175 to 315 pixels. | Throw both blades toward the cursor and recall them. |

## Sprite previews

| Armored missile | Plasma bolt |
| :---: | :---: |
| <img src="docs/images/BossRocket-preview.png" width="384" alt="Armored missile sprite"> | <img src="docs/images/BossBullet-preview.png" width="240" alt="Plasma bolt sprite"> |

These are enlarged sprite previews. In-game animation and effects are separate.

## Checks and compatibility

The 0.1.5 build completed with **zero errors and warnings**. Checks covered 27 boss test groups, weapon and mana regressions, lifecycle cleanup, 1,000 actual treasure-bag openings, the recipe, localization, BossChecklist and isolated loading with Calamity. [Read the test report](docs/TESTING.md).

Live multiplayer transport, GPU appearance, the complete third-party modpack and balance on a fully equipped character still need in-game testing.

Found a bug? [Open an issue](https://github.com/Doffi4/adam-smasher-tmodloader/issues) with your mod versions, reproduction steps and a relevant log excerpt.

## Source and credits

[Build instructions](docs/BUILD.md) | [Changelog](CHANGELOG.md) | [Credits](docs/CREDITS.md)

Written in C# / .NET 8. Game DLLs and dependency mods are not included. Code and original project assets are licensed under MIT.

Adam Smasher, Arasaka and Cyberpunk belong to their respective rightsholders. This is an unofficial fan project. Development used OpenAI Codex; artwork used AI generation and sprite preparation. See [attribution](ATTRIBUTION.txt).
