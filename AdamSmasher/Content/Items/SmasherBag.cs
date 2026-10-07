using AdamSmasherMod.Common;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AdamSmasherMod.Content.Items;

public class SmasherBag : ModItem
{
    public override string Texture => "AdamSmasher/Content/Items/SmasherBag";
    public override void SetStaticDefaults()
    {
        ItemID.Sets.BossBag[Type] = true;
        Item.ResearchUnlockCount = 3;
    }
    public override void SetDefaults()
    {
        Item.width = 40;
        Item.height = 44;
        Item.maxStack = Item.CommonMaxStack;
        Item.consumable = true;
        Item.rare = ItemRarityID.Expert;
        Item.expert = true;
    }
    public override bool CanRightClick() => true;
    public override void ModifyItemLoot(ItemLoot itemLoot) => itemLoot.Add(SmasherLoot.CreateWeaponRule());
}
