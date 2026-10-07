using System;
using System.Collections.Generic;
using AdamSmasherMod.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace AdamSmasherMod.Content.Projectiles;
public class PlasmaField : ModProjectile
{
    public const int Duration = 180, HitInterval = 20;
    public const float Radius = 240, DamageFraction = 0.2f;
    readonly Dictionary<int, int> lastHitByGroup = new();
    public override string Texture => "AdamSmasher/Content/Projectiles/PlasmaField";
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = (int)Radius * 2;
        Projectile.friendly = true; Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.ignoreWater = true;
        Projectile.timeLeft = Duration; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = HitInterval;
    }
    public override void OnSpawn(IEntitySource source)
    {
        if (Main.myPlayer != Projectile.owner) return;
        foreach (Projectile old in Main.ActiveProjectiles)
            if (old.whoAmI != Projectile.whoAmI && old.type == Type && old.owner == Projectile.owner) old.Kill();
    }
    public override void AI()
    {
        Projectile.ai[0]++;
        if (!Main.dedServ)
        {
            float fade = MathHelper.Clamp(Projectile.timeLeft / 24f, 0, 1);
            Lighting.AddLight(Projectile.Center, 0.8f * fade, 0.03f * fade, 0.01f * fade);
            if (Projectile.ai[0] == 1) WeaponVisuals.Burst(Projectile.Center, 6, 4, 18);
            if ((int)Projectile.ai[0] % 6 == 0 && Projectile.timeLeft > 24)
            {
                float angle = Projectile.ai[0] * 0.31f;
                Vector2 radial = angle.ToRotationVector2();
                WeaponVisuals.Particle(Projectile.Center + radial * Radius * 0.92f, radial.RotatedBy(1.2f) * 1.8f);
            }
        }
    }
    public override bool? CanHitNPC(NPC target) => lastHitByGroup.TryGetValue(SmartRocket.Group(target), out int last) && Projectile.ai[0] - last < HitInterval ? false : null;
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => lastHitByGroup[SmartRocket.Group(target)] = (int)Projectile.ai[0];
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) =>
        Vector2.DistanceSquared(Projectile.Center, Utils.ClosestPointInRect(targetHitbox, Projectile.Center)) <= Radius * Radius;
    public override bool PreDraw(ref Color lightColor)
    {
        if (!WeaponVisuals.Visible(Projectile.Center, Radius + 32)) return false;
        Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
        float age = Projectile.ai[0];
        float start = MathHelper.Clamp(age / 16f, 0, 1);
        float grow = 1 - MathF.Pow(1 - start, 3);
        float fade = MathHelper.SmoothStep(0, 1, MathHelper.Clamp(Projectile.timeLeft / 24f, 0, 1));
        float opacity = fade * MathHelper.Clamp(age / 5f, 0, 1);
        float radius = Radius * (0.16f + 0.84f * grow) * (0.92f + 0.08f * fade);
        float pulse = 0.88f + 0.12f * MathF.Sin(age * 0.19f);
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White * (0.42f * pulse * opacity),
            age * 0.025f, texture.Size() / 2, radius * 2 / texture.Width, SpriteEffects.None);
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, WeaponVisuals.RedGlow * (0.12f * opacity),
            -age * 0.037f, texture.Size() / 2, radius * 1.45f / texture.Width, SpriteEffects.None);

        for (int i = 0; i < 3; i++)
        {
            float angle = age * 0.037f + i * MathHelper.TwoPi / 3;
            WeaponVisuals.Arc(Projectile.Center, radius, angle, 1.05f, WeaponVisuals.RedGlow * (0.65f * opacity), 3);
            WeaponVisuals.Arc(Projectile.Center, radius, angle + 0.15f, 0.38f, WeaponVisuals.WhiteGlow * (0.72f * opacity), 1.25f, 4);
            Vector2 tip = Projectile.Center + (angle + 1.05f).ToRotationVector2() * radius;
            WeaponVisuals.Spark(tip, angle + MathHelper.PiOver2, 4 + 2 * pulse, opacity * 0.8f);
        }
        for (int i = 0; i < 2; i++)
        {
            WeaponVisuals.Arc(Projectile.Center, radius * 0.69f, -age * 0.051f + i * MathHelper.Pi,
                0.95f, WeaponVisuals.RedGlow * (0.38f * opacity), 2);
            float wave = (age + i * 22) % 44 / 44f;
            WeaponVisuals.Arc(Projectile.Center, radius * (0.22f + 0.76f * wave), age * 0.012f + i * MathHelper.Pi,
                2.1f, WeaponVisuals.RedGlow * ((1 - wave) * 0.18f * opacity), 1.5f);
            float crackleAngle = -age * 0.026f + i * MathHelper.Pi + 0.6f;
            Vector2 previous = Projectile.Center + crackleAngle.ToRotationVector2() * radius * 0.74f;
            for (int segment = 1; segment <= 3; segment++)
            {
                float jitter = MathF.Sin((int)(age / 3) * 2.7f + segment * 4.1f + i) * 0.045f;
                Vector2 next = Projectile.Center + (crackleAngle + segment * 0.12f + jitter).ToRotationVector2() * radius * (0.74f + segment * 0.075f);
                WeaponVisuals.Line(previous, next, WeaponVisuals.WhiteGlow * (0.38f * opacity), 1);
                previous = next;
            }
        }
        if (start < 1)
            WeaponVisuals.Arc(Projectile.Center, radius, 0, MathHelper.TwoPi,
                WeaponVisuals.WhiteGlow * ((1 - start) * opacity * 0.5f), 2, 16);
        return false;
    }
}
