<p align="center"><img src="docs/images/banner.png" alt="Adam Smasher boss and reward sprites" width="100%"></p>

# Adam Smasher

[English](README.md) В· [Р СѓСЃСЃРєРёР№](README.ru.md)

[![Release](https://img.shields.io/github/v/release/Doffi4/adam-smasher-tmodloader?style=flat-square&color=e9993c)](https://github.com/Doffi4/adam-smasher-tmodloader/releases/latest)
![Terraria](https://img.shields.io/badge/Terraria-1.4.4.9-658949?style=flat-square)
![tModLoader](https://img.shields.io/badge/tModLoader-2026.08.3.0-567998?style=flat-square)
[![MIT](https://img.shields.io/badge/license-MIT-c9b791?style=flat-square)](LICENSE.txt)

An unofficial Cyberpunk-inspired boss for late-endgame Calamity playthroughs. Learn fixed missile zones, aimed bursts and Sandevistan dash sequences, then win one of two powerful weapons.

**[Download AdamSmasher.tmod](https://github.com/Doffi4/adam-smasher-tmodloader/releases/latest/download/AdamSmasher.tmod)** В· [All release files](https://github.com/Doffi4/adam-smasher-tmodloader/releases/latest)

## The fight

The boss locks its final warning for 24 game ticks before attacking. Three marked missile strikes become five in phase two; rockets deliver explosions to the circles rather than causing contact damage along their route. Each gun burst and dash gets a new warning. Phase two chains missiles into a separately warned gun burst.

Base health: **18 million**. Defense: **110**. Contact damage: **600, during an active dash only**. Bullet/blast spawn damage: **350/450**, before difficulty, defense and other mod hooks. Balance is provisional.

## Two rewards, one drop

Each reward roll independently gives **exactly one weapon, 50%/50%**. Repeated drops are possible. Normal mode drops the weapon directly; Expert/Master uses the boss treasure bag.

| Reward | Left click | Right click |
| --- | --- | --- |
| **Annihilation Protocol** / РџСЂРѕС‚РѕРєРѕР» СѓРЅРёС‡С‚РѕР¶РµРЅРёСЏ | Two homing missiles; 10 base mana per pair; secondary area explosion | Charge for 1вЂ“2 seconds to fire a heavy plasma shot with an animated damage field |
| **Arasaka Mantis Blades** / РљР»РёРЅРєРё Р±РѕРіРѕРјРѕР»Р° В«РђСЂР°СЃР°РєР°В» | Hold for 2 seconds to ramp damage up to +50% and reach from 175 to 315 px; three-hit combo | Throw both blades toward the cursor and recall them |

## Install

1. Install **Calamity Mod** and **Calamity Mod Music** in tModLoader. Tested versions: 2.2.2 / 2.1. BossChecklist 2.2.4 is optional.
2. Download `AdamSmasher.tmod` and copy it into the profile's `Mods` folder (open it from tModLoader's Mods menu).
3. Enable the mod and restart tModLoader. Install the **same 0.1.5 version on the server and every client**.

Existing worlds are supported; no world generation is needed. Summon with the reusable **Arasaka Beacon**: **5 Shadowspec Bars + 25 Mysterious Circuitry + 25 Dubious Plating**, crafted at **Draedon's Forge**. Another active boss blocks the summon.

To uninstall, disable the mod and reload. Its items become unloaded items while disabled. Back up your world/player before adding or removing mods.

## Art preview

These are sprite previews, not gameplay screenshots.

| Armored missile | Plasma bolt |
| :---: | :---: |
| <img src="docs/images/BossRocket-preview.png" width="384" alt="Orange armored hostile missile"> | <img src="docs/images/BossBullet-preview.png" width="240" alt="Gold plasma bolt"> |

## Verification and compatibility

Fresh checks on **2026-10-07**: production build, 27 boss test groups, lifecycle and weapon regressions, 1,000 real treasure-bag openings, localization/recipe/BossChecklist, and an isolated Calamity load/summon smoke. See [verification details](docs/TESTING.md).

The AI and projectile authority are server-owned and tested with engine fixtures. **Live multi-client transport, client GPU appearance, the complete third-party modpack and equipped DPS/boss difficulty are not verified.** An eligible collision test is not a claim that a particular equipped player cannot win while idle. No guaranteed compatibility with every Calamity addon is claimed.

If something fails, [open an issue](https://github.com/Doffi4/adam-smasher-tmodloader/issues) with your versions, reproduction steps and relevant log excerpt. Remove account details, IP addresses and personal paths from logs.

## Build and source

C# 12 / .NET 8. The game and mod dependencies are **not redistributed**. [Build instructions](docs/BUILD.md) В· [Changelog](CHANGELOG.md) В· [Credits](docs/CREDITS.md).

Code and original project assets use MIT. Adam Smasher, Arasaka and Cyberpunk belong to their respective rightsholders; this fan project is unaffiliated. Created with OpenAI Codex (GPT-6 agent family); artwork uses AI generation plus project-specific sprite preparation. [Attribution](ATTRIBUTION.txt).
