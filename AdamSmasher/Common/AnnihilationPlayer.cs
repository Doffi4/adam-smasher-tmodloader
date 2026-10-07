using Terraria.ModLoader;

namespace AdamSmasherMod.Common;
// Per-player rather than per-item: swapping copies never resets the cooldown.
public class AnnihilationPlayer : ModPlayer
{
    public int PlasmaCooldownTicks { get; private set; }
    public void StartPlasmaCooldown(int ticks) => PlasmaCooldownTicks = System.Math.Max(PlasmaCooldownTicks, ticks);
    public override void PostUpdate() { if (PlasmaCooldownTicks > 0) PlasmaCooldownTicks--; }
    public override void UpdateDead() { if (PlasmaCooldownTicks > 0) PlasmaCooldownTicks--; }
}
