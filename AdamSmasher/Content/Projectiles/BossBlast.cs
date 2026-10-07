using AdamSmasherMod.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AdamSmasherMod.Content.Projectiles;
public class BossBlast : ModProjectile
{
    public const int Duration = 24, DamageDuration = 8;
    public const float Radius = 110;
    private int Elapsed => Duration - Projectile.timeLeft;
    public override string Texture => "AdamSmasher/Content/Projectiles/PlasmaField";
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = (int)Radius * 2;
        Projectile.hostile = true;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.timeLeft = Duration;
        Projectile.aiStyle = -1;
    }
    public override void AI()
    {
        if (!BossBullet.LiveBoss(Projectile)) { Projectile.Kill(); return; }
        Projectile.velocity = Vector2.Zero;
        if (!Main.dedServ)
        {
            float fade = Projectile.timeLeft / (float)Duration;
            Lighting.AddLight(Projectile.Center, 0.9f * fade, 0.25f * fade, 0.035f * fade);
            if (Elapsed == 0) WeaponVisuals.Burst(Projectile.Center, 8, 5, 12, Projectile.identity * 0.4f);
        }
    }
    public override bool? CanDamage() => Elapsed < DamageDuration && BossBullet.LiveBoss(Projectile) ? null : false;
    public override bool CanHitPlayer(Player target) => Elapsed < DamageDuration && BossBullet.LiveBoss(Projectile);
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) =>
        Vector2.DistanceSquared(Projectile.Center, Utils.ClosestPointInRect(targetHitbox, Projectile.Center)) <= Radius * Radius;
    public override bool PreDraw(ref Color lightColor)
    {
        if (!WeaponVisuals.Visible(Projectile.Center, Radius + 32)) return false;
        float age = Elapsed;
        float fade = Projectile.timeLeft / (float)Duration;
        float visualRadius = Radius * (0.35f + 0.65f * MathHelper.Clamp(age / 8, 0, 1));
        Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
        Color glow = new(255, 145, 35, 0);
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glow * (fade * 0.65f),
            age * 0.045f, texture.Size() / 2, visualRadius * 2 / texture.Width, SpriteEffects.None);
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, new Color(255, 225, 150) * (fade * 0.5f),
            -age * 0.06f, texture.Size() / 2, visualRadius * 1.5f / texture.Width, SpriteEffects.None);
        // Full-radius rim always matches collision, even while the inner flash grows.
        WeaponVisuals.Arc(Projectile.Center, Radius, 0, MathHelper.TwoPi, glow * (fade * 0.85f), 3, 32);
        for (int i = 0; i < 3; i++)
        {
            float angle = age * 0.08f + i * MathHelper.TwoPi / 3;
            WeaponVisuals.Arc(Projectile.Center, visualRadius * 0.9f, angle, 1.1f, WeaponVisuals.WhiteGlow * (fade * 0.8f), 1.5f, 8);
        }
        return false;
    }
}
