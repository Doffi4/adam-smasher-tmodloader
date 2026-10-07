using AdamSmasherMod.Common;
using AdamSmasherMod.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;

namespace AdamSmasherMod.Content.Items;
public class ArasakaMantisBlades : ModItem
{
    public const int BaseDamage = 3000, AttackInterval = 12;
    public const float CrossDamageMultiplier = 1.5f, RampDamageBonus = 0.5f;
    public override string Texture => "AdamSmasher/Content/Items/ArasakaMantisBlades";
    public override void SetDefaults()
    {
        Item.width = 54; Item.height = 40; Item.damage = BaseDamage; Item.DamageType = DamageClass.Melee;
        Item.useTime = Item.useAnimation = AttackInterval; Item.useStyle = ItemUseStyleID.Shoot;
        Item.noMelee = true; Item.noUseGraphic = true; Item.autoReuse = true;
        Item.knockBack = 6; Item.shootSpeed = MantisThrow.Speed;
        Item.shoot = ModContent.ProjectileType<MantisSlash>();
        Item.rare = ItemRarityID.Purple; Item.value = Item.sellPrice(gold: 40); Item.UseSound = SoundID.Item71;
    }
    public override bool AltFunctionUse(Player player) => true;
    public override bool CanUseItem(Player player)
    {
        int type = player.altFunctionUse == 2 ? ModContent.ProjectileType<MantisThrow>() : ModContent.ProjectileType<MantisSlash>();
        foreach (Projectile p in Main.ActiveProjectiles)
            if (p.owner == player.whoAmI && p.type == type) return false;
        return true;
    }
    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        if (Main.myPlayer != player.whoAmI) return false;
        MantisPlayer mantis = player.GetModPlayer<MantisPlayer>();
        if (player.altFunctionUse == 2) mantis.ResetRamp();
        if (!CanUseItem(player)) return false;
        if (player.altFunctionUse == 2)
        {
            Vector2 offset = Main.MouseWorld - player.MountedCenter;
            Vector2 direction = offset.SafeNormalize(new Vector2(player.direction, 0));
            Vector2 target = player.MountedCenter + direction * MathHelper.Clamp(offset.Length(), 48, MantisThrow.MaximumReach);
            for (int side = -1; side <= 1; side += 2)
                Projectile.NewProjectile(source, player.MountedCenter + direction.RotatedBy(MathHelper.PiOver2) * side * 12,
                    direction.RotatedBy(side * 0.08f) * MantisThrow.Speed, ModContent.ProjectileType<MantisThrow>(), damage, knockback, player.whoAmI, side, target.X, target.Y);
            mantis.ResetCombo();
        }
        else
        {
            float strength = mantis.BeginSlash(Item);
            int combo = mantis.NextSlash(Item);
            float angle = (Main.MouseWorld - player.MountedCenter).SafeNormalize(new Vector2(player.direction, 0)).ToRotation();
            int duration = player.itemAnimationMax > 0 ? player.itemAnimationMax : CombinedHooks.TotalAnimationTime(Item.useAnimation, player, Item);
            Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, ModContent.ProjectileType<MantisSlash>(),
                (int)(damage * (1 + RampDamageBonus * strength) * (combo == 2 ? CrossDamageMultiplier : 1)),
                knockback, player.whoAmI, combo, angle, System.Math.Max(2, duration));
        }
        return false;
    }
}
