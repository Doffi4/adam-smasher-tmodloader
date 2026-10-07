using AdamSmasherMod.Common;
using AdamSmasherMod.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;

namespace AdamSmasherMod.Content.Items;
public class AnnihilationProtocol : ModItem
{
    public const int BaseDamage = 1800, ManaPerUse = 10, SalvoInterval = 10;
    public override string Texture => "AdamSmasher/Content/Items/AnnihilationProtocol";
    public override void SetDefaults()
    {
        Item.width = 64; Item.height = 30;
        Item.damage = BaseDamage; Item.DamageType = DamageClass.Magic;
        Item.mana = ManaPerUse; Item.useTime = Item.useAnimation = SalvoInterval;
        Item.useStyle = ItemUseStyleID.Shoot; Item.noMelee = true;
        Item.autoReuse = true; Item.channel = true;
        Item.knockBack = 4; Item.shootSpeed = SmartRocket.Speed;
        Item.shoot = ModContent.ProjectileType<SmartRocket>();
        Item.rare = ItemRarityID.Purple; Item.value = Item.sellPrice(gold: 40);
        Item.UseSound = SoundID.Item91;
    }
    public override bool AltFunctionUse(Player player) => true;
    public override bool CanUseItem(Player player)
    {
        if (player.altFunctionUse == 2 && player.GetModPlayer<AnnihilationPlayer>().PlasmaCooldownTicks > 0) return false;
        int chargeType = ModContent.ProjectileType<PlasmaCharge>();
        if (player.ownedProjectileCounts[chargeType] > 0)
            foreach (Projectile p in Main.ActiveProjectiles)
                if (p.owner == player.whoAmI && p.type == chargeType && p.ai[0] == 0) return false;
        return true;
    }
    public override Vector2? HoldoutOffset() => new Vector2(-12, 0);
    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        if (Main.myPlayer != player.whoAmI) return false;
        Vector2 aim = Main.MouseWorld;
        if (player.altFunctionUse == 2)
        {
            if (CanUseItem(player))
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<PlasmaCharge>(), damage, knockback, player.whoAmI, 0, 0, player.selectedItem);
            return false;
        }
        int first = SmartRocket.FindTarget(aim, position);
        int second = SmartRocket.FindTarget(aim, position, first);
        if (second < 0) second = first;
        Vector2 direction = velocity.SafeNormalize(new Vector2(player.direction, 0));
        for (int shot = 0; shot < 2; shot++)
            Projectile.NewProjectile(source, position, direction.RotatedBy(shot == 0 ? -0.06f : 0.06f) * SmartRocket.Speed,
                ModContent.ProjectileType<SmartRocket>(), damage, knockback, player.whoAmI, (shot == 0 ? first : second) + 1, aim.X, aim.Y);
        return false;
    }
}
