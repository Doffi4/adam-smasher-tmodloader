# Build locally

Use PowerShell 7.2+, .NET SDK 8 and your own tModLoader/Terraria installation. You also need installed Calamity Mod and Calamity Mod Music packages. No game libraries or dependencies are included in this repository.

```powershell
$env:TML_PATH = 'D:/Steam/steamapps/common/tModLoader'
$env:CALAMITY_MOD_PATH = 'D:/your-mod-packages/CalamityMod.tmod'
$env:CALAMITY_MUSIC_PATH = 'D:/your-mod-packages/CalamityModMusic.tmod'
pwsh -NoProfile -File tools/prepare-lab.ps1
pwsh -NoProfile -File tools/build.ps1
pwsh -NoProfile -File tools/package.ps1
```

Replace all example paths with your installations. The scripts create a private `lab/` runtime/profile and place the mod in `dist/`. Never commit or redistribute `lab/`, `bin/`, `obj/` or dependency DLLs. The environment variable is also used by harness projects for their tML targets import.

The ready-to-use `.tmod` does not require Python, Universal Modder or graphics generation. Original source PNGs are included.

## Engine checks

Harnesses require an isolated test world called `Task1Lab5.wld` in `lab/profile/Worlds`. Task1 creates/tests the lab world, or pass its `-ExistingLabWorld` option for a disposable existing world. Optional BossChecklist checks require its package in the lab mods folder.

```powershell
pwsh -NoProfile -File tools/test-task1.ps1
pwsh -NoProfile -File tools/test-boss014.ps1 -SkipProductionBuild
pwsh -NoProfile -File tools/test-task2.ps1 -SkipProductionBuild
pwsh -NoProfile -File tools/test-task3.ps1 -SkipProductionBuild
pwsh -NoProfile -File tools/test-task4.ps1 -SkipProductionBuild
pwsh -NoProfile -File tools/test-task5.ps1 -SkipProductionBuild
$env:ADAM_SMASHER_LAB_MODE = 'load'
pwsh -NoProfile -File tools/test-task6.ps1
```

Tests use a local dedicated server and disposable fixtures. They do not validate client rendering or real multiplayer transport. Inspect scripts before running them with custom worlds. Use the load smoke mode; the experimental full-player combat bench is not a verified DPS test.
