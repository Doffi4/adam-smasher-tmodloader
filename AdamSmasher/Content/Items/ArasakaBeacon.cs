using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AdamSmasherMod.Content.Items;

public class ArasakaBeacon : ModItem
{
    public override string Texture => "AdamSmasher/Content/Items/ArasakaBeacon";

    public override void SetStaticDefaults() => ItemID.Sets.SortingPriorityBossSpawns[Type] = 13;

    public override void SetDefaults()
    {
        Item.width = 32;
        Item.height = 40;
        Item.maxStack = 1;
        Item.rare = ItemRarityID.Red;
        Item.useTime = Item.useAnimation = 30;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.consumable = false;
    }

    public override bool CanUseItem(Player player) => AdamSmasher.CanSummon(player);

    public override void AddRecipes()
    {
        if (!ModLoader.TryGetMod("CalamityMod", out Mod calamity) ||
            !calamity.TryFind<ModItem>("ShadowspecBar", out var bar) ||
            !calamity.TryFind<ModItem>("MysteriousCircuitry", out var circuits) ||
            !calamity.TryFind<ModItem>("DubiousPlating", out var plating) ||
            !calamity.TryFind<ModTile>("DraedonsForge", out var forge))
        {
            Mod.Logger.Error("Arasaka Beacon recipe unavailable: expected CalamityMod ShadowspecBar, MysteriousCircuitry, DubiousPlating items and DraedonsForge tile. Check Calamity compatibility.");
            return;
        }
        CreateRecipe().AddIngredient(bar.Type, 5).AddIngredient(circuits.Type, 25)
            .AddIngredient(plating.Type, 25).AddTile(forge.Type).Register();
    }

    public override bool? UseItem(Player player)
    {
        if (player.whoAmI == Main.myPlayer)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return AdamSmasher.TrySummon(player.whoAmI);
            else
            {
                ModPacket packet = Mod.GetPacket();
                packet.Write(AdamSmasher.SummonRequest);
                packet.Send();
            }
        }
        return true;
    }
}
