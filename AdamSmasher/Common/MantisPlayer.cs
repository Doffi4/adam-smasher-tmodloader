using System.Collections.Generic;
using AdamSmasherMod.Content.Items;
using AdamSmasherMod.Content.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace AdamSmasherMod.Common;
// Both blade visuals and both attack modes share one accepted-hit clock per HP group.
public class MantisPlayer : ModPlayer
{
    public const int ComboResetTicks = 45, HitInterval = 8, RampTicks = 120;
    readonly Dictionary<int, long> nextHitByGroup = new();
    long tick;
    int combo, comboIdle, comboSlot = -1;
    Item comboItem;
    int rampTicks, rampSlot = -1;
    Item rampItem;
    public float RampStrength => rampTicks / (float)RampTicks;

    bool HoldingSlash() => Player.controlUseItem && !Player.controlUseTile && Player.altFunctionUse != 2 &&
        !Player.mouseInterface && !Main.blockMouse && Main.hasFocus && !Main.blockInput && !Main.editSign && !Main.editChest;
    public float BeginSlash(Item item)
    {
        // Only an actual owner attack starts accumulation; holding an unrelated/UI input cannot precharge it.
        if (!ValidSource(Player, rampSlot, rampItem) || !HoldingSlash()) ResetRamp();
        if (HoldingSlash() && ValidSource(Player, Player.selectedItem, item))
        { rampSlot = Player.selectedItem; rampItem = item; }
        return RampStrength;
    }
    public void ResetRamp() { rampTicks = 0; rampSlot = -1; rampItem = null; }

    public int NextSlash(Item item)
    {
        if (comboIdle <= 0 || comboSlot != Player.selectedItem || !ReferenceEquals(comboItem, item)) ResetCombo();
        int result = combo;
        combo = (combo + 1) % 3;
        comboIdle = ComboResetTicks; comboSlot = Player.selectedItem; comboItem = item;
        return result;
    }
    public void ResetCombo() { combo = comboIdle = 0; comboSlot = -1; comboItem = null; }
    public static int Group(NPC target) => target.realLife >= 0 ? target.realLife : target.whoAmI;
    public bool CanHitGroup(NPC target) => !nextHitByGroup.TryGetValue(Group(target), out long next) || tick >= next;
    public void RecordHitGroup(NPC target) => nextHitByGroup[Group(target)] = tick + HitInterval;
    public static bool ValidSource(Player player, int slot, Item item) => player.active && !player.dead && !player.noItems && !player.CCed &&
        player.selectedItem == slot && player.HeldItem.type == ModContent.ItemType<ArasakaMantisBlades>() && ReferenceEquals(player.HeldItem, item);
    public override void PostUpdate()
    {
        tick++;
        // Charge counts game ticks, not swings or projectile extra updates; each slash snapshots it at spawn.
        if (Main.myPlayer == Player.whoAmI)
        {
            if (!HoldingSlash() || !ValidSource(Player, rampSlot, rampItem)) ResetRamp();
            else if (rampTicks < RampTicks) rampTicks++;
        }
        if (!ValidSource(Player, comboSlot, comboItem)) { ResetCombo(); return; }
        // The reset window measures idle time after the swing, not time spent attacking.
        int slashType = ModContent.ProjectileType<MantisSlash>();
        foreach (Projectile p in Main.ActiveProjectiles)
            if (p.owner == Player.whoAmI && p.type == slashType) { comboIdle = ComboResetTicks; return; }
        if (comboIdle > 0) comboIdle--;
        if (comboIdle <= 0) ResetCombo();
    }
    public override void UpdateDead() { tick++; ResetCombo(); ResetRamp(); }
}
