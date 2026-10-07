using AdamSmasherMod.Content.Items;
using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;

namespace AdamSmasherMod.Common;

public static class SmasherLoot
{
    // One guaranteed choice; separate 50% rules could award both weapons or neither.
    public static IItemDropRule CreateWeaponRule() => ItemDropRule.OneFromOptionsNotScalingWithLuck(1,
        ModContent.ItemType<AnnihilationProtocol>(), ModContent.ItemType<ArasakaMantisBlades>());
}
