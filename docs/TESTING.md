# Release verification — 0.1.5

Date: 2026-10-07. Environment: tModLoader2026.08.3.0 / Terraria1.4.4.9 / .NET8; isolated Calamity2.2.2 + Music2.1. Optional BossChecklist2.2.4 checked by the loot/integration fixture.

## Fresh results

| Check | Result |
| --- | --- |
| Production build and official tML package | 0 errors / 0 warnings |
| Boss engine fixture | 27 groups PASS, including two new off-screen draw-eligibility regressions |
| Lifecycle and networking branches | PASS; state/ExtraAI, no client spawning, retirement/death/orphan cleanup and repeat summon |
| Difficulty pipeline | PASS; Normal/Expert/Master CombinedHooks fixtures |
| Magic weapon | PASS; actual item-use mana path, charges/cancellation, owner authority, homing and secondary explosion |
| Melee weapon | PASS; held ramp, collision snapshots, combo, interruption resets and shared accepted-hit clock |
| Reward and integrations | 1,000/1,000 bags contained exactly one weapon; 495 magic / 505 melee; Normal/Expert/Master branches, recipe, RU/EN and BossChecklist PASS |
| Load/summon smoke | Loaded AdamSmasher0.1.5, 360 full NPC.UpdateNPC updates and retirement cleanup PASS |

Machine-readable line reports are in [verification/](verification/). Harness source is under `verification/` in the repository; no game/dependency DLLs are included.

## Fixed before publication

Code review found that Terraria culls an off-screen NPC/projectile before invoking its PreDraw. Boss aim lines and rocket strike markers could therefore disappear while their marked zones intersected the player's screen. Version0.1.5 registers the boss with MustAlwaysDraw and the rocket with a bounded3400px draw margin, retaining the rocket's local visibility checks.

The new checks first failed on0.1.4 for both registered draw gates, then passed on0.1.5. They verify engine draw eligibility and geometry; they do not execute GPU rendering. Other attack timing, damage, weapons, loot and source PNGs are unchanged from0.1.4.

## Scope limits

Stationary-pressure checks use actual Projectile.Update and collision hooks and find eligible impacts in both phases; their moving fixtures escape at7px/tick after target lock. They do not measure equipped damage, survival or DPS and do not prove that every equipped player must die while idle.

Synthetic client/server branches and ExtraAI round-trips do not test real network transport or latency. Fresh live multi-client play, client GPU visual quality, full third-party modpack compatibility and tuned difficulty with the user's gear remain unverified. No automated cloud build is claimed: Calamity/game dependencies must be supplied locally.
