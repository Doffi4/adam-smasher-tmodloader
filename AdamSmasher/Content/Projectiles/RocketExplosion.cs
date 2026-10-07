using System.Collections.Generic;
using AdamSmasherMod.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AdamSmasherMod.Content.Projectiles;
// Separate hitbox keeps direct damage separate from bounded secondary damage.
public class RocketExplosion : ModProjectile
{
    public const float DamageFraction = 0.3f, Radius = 120;
    readonly HashSet<int> hitGroups = new();
    int visualAge;
    public override string Texture => "AdamSmasher/Content/Projectiles/PlasmaField";
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = (int)Radius * 2; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic; Projectile.penetrate = -1;
        Projectile.tileCollide = false; Projectile.ignoreWater = true; Projectile.timeLeft = 3;
        Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override bool? CanHitNPC(NPC target) => SmartRocket.Group(target) == (int)Projectile.ai[0] - 1 || hitGroups.Contains(SmartRocket.Group(target)) ? false : null;
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => hitGroups.Add(SmartRocket.Group(target));
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) =>
        Vector2.DistanceSquared(Projectile.Center, Utils.ClosestPointInRect(targetHitbox, Projectile.Center)) <= Radius * Radius;
    public override void AI()
    {
        if (Main.dedServ) return;
        if (visualAge++ == 0) WeaponVisuals.Burst(Projectile.Center, 12, 5.5f, 8, Projectile.identity * 0.7f);
        Lighting.AddLight(Projectile.Center, 1, 0.12f, 0.04f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        if (!WeaponVisuals.Visible(Projectile.Center, Radius + 32)) return false;
        Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
        float progress = MathHelper.Clamp(visualAge / 3f, 0, 1);
        float radius = Radius * (0.45f + 0.55f * progress);
        float opacity = 0.8f - progress * 0.35f;
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White * opacity,
            visualAge * 0.2f, texture.Size() / 2, radius * 2 / texture.Width, SpriteEffects.None);
        WeaponVisuals.Arc(Projectile.Center, radius, 0, MathHelper.TwoPi, WeaponVisuals.RedGlow * opacity, 4, 20);
        WeaponVisuals.Arc(Projectile.Center, radius * 0.93f, 0, MathHelper.TwoPi, WeaponVisuals.WhiteGlow * opacity, 1.5f, 20);
        for (int i = 0; i < 6; i++)
        {
            float angle = MathHelper.TwoPi * i / 6 + Projectile.identity * 0.7f;
            Vector2 direction = angle.ToRotationVector2();
            WeaponVisuals.Line(Projectile.Center + direction * radius * 0.55f, Projectile.Center + direction * radius,
                WeaponVisuals.WhiteGlow * (opacity * 0.55f), 1.5f);
        }
        return false;
    }
}
