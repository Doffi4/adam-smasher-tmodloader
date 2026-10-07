# Changelog

## [0.1.5] - 2026-10-07

### Fixed
- Keep boss aim lines and missile strike circles eligible for drawing when the boss or delivery rocket is off-screen. Boss always-draw registration and a bounded 3400px rocket draw margin prevent engine culling before PreDraw.
- Combat timing, balance, weapons and reward probabilities are unchanged from 0.1.4.

## [0.1.4] - 2026-10-04

### Added
- Fixed marked missile impacts with radius-110 blasts: eight damage ticks and a 24-tick visual lifetime.
- New hostile rocket and plasma-bolt sprites, trails and animated impact effects.

### Changed
- Hard boss tuning: base 18 million HP, 600 dash contact, 350 bullet and 450 blast damage before standard scaling.
- Repeated aimed gun bursts and Sandevistan dashes with a fixed final 24-tick warning.
- Phase two chains missile strikes into a fresh gun warning. ExtraAI includes target point, dash duration and burst index; all clients/server must use 0.1.4.

### Fixed
- Oversized warning lines caused by sampling Terraria's entire 1x1000 MagicPixel strip.

## [0.1.3] - 2026-10-04

### Fixed
- Oversized magic trails/arcs/flashes by sampling a 1x1 pixel.

### Changed
- Base mana per salvo/charge payment reduced from 20 to 10; melee ramp preserved.

## [0.1.2] - 2026-10-04

### Added
- Held melee ramp: up to +50% damage over two seconds, reach 175 to 315 pixels.
- Animated magic projectile trails, impact flashes and plasma field growth/rotation/fade.

## [0.1.1] - 2026-10-04

### Changed
- Projectile texture and drawing revision.

## [0.1.0] - 2026-10-04

### Added
- Adam Smasher boss, reusable Arasaka Beacon and exactly-one independent 50/50 mage/melee reward.
- Calamity recipe, optional BossChecklist entry, Russian and English localization.

[0.1.5]: https://github.com/Doffi4/adam-smasher-tmodloader/releases/tag/v0.1.5
[0.1.4]: https://github.com/Doffi4/adam-smasher-tmodloader/releases/tag/v0.1.4
