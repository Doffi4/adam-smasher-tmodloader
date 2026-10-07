using System;
using AdamSmasherMod.Common;
using AdamSmasherMod.Content.NPCs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AdamSmasherMod.Content.Projectiles;
public class BossRocket : ModProjectile
{
    public const float Speed = 20;
    private int visualAge;
    public Vector2 StrikePoint => new(Projectile.ai[1], Projectile.ai[2]);
    public override string Texture => "AdamSmasher/Content/Projectiles/BossRocket";
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 9;
        ProjectileID.Sets.TrailingMode[Type] = 0;
        // A visible fixed impact marker must reach PreDraw even if delivery is off-screen.
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = (int)AdamSmasherBoss.EngagementRange + 400;
    }
    public override void SetDefaults()
    {
        Projectile.width = 26;
        Projectile.height = 12;
        Projectile.hostile = true;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 150;
        Projectile.aiStyle = -1;
    }
    public override void AI()
    {
        if (!BossBullet.LiveBoss(Projectile)) { Projectile.Kill(); return; }
        Vector2 destination = StrikePoint;
        if (!float.IsFinite(destination.X) || !float.IsFinite(destination.Y)) { Projectile.Kill(); return; }
        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            Vector2 delta = destination - Projectile.Center;
            if (delta.LengthSquared() <= Speed * Speed || Projectile.timeLeft <= 1)
            {
                Detonate(destination);
                return;
            }
            // Coordinates are fixed at launch; no player tracking during flight.
            Projectile.velocity = delta.SafeNormalize(Vector2.UnitX) * Speed;
            if ((int)Projectile.localAI[0]++ % 8 == 0) Projectile.netUpdate = true;
        }
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (!Main.dedServ)
        {
            visualAge++;
            Lighting.AddLight(Projectile.Center, 0.65f, 0.2f, 0.025f);
            if (visualAge % 4 == 0)
                WeaponVisuals.Particle(Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.UnitX) * 20,
                    -Projectile.velocity * 0.07f, 0.8f);
        }
    }
    private void Detonate(Vector2 point)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !BossBullet.LiveBoss(Projectile)) return;
        int bossIndex = (int)Projectile.ai[0];
        AdamSmasherBoss boss = (AdamSmasherBoss)Main.npc[bossIndex].ModNPC;
        int damage = Projectile.damage;
        int owner = Projectile.owner;
        var source = Projectile.GetSource_FromThis();
        Projectile.Center = point;
        Projectile.netUpdate = true;
        // Cleanup Kill never detonates. Releasing this slot also preserves the shared cap.
        Projectile.Kill();
        if (boss.OwnedProjectileCount >= AdamSmasherBoss.ProjectileLimit) return;
        Projectile.NewProjectile(source, point, Vector2.Zero, ModContent.ProjectileType<BossBlast>(),
            damage, 0, owner, bossIndex, BossBlast.Radius);
    }
    // All damage belongs to the previously marked impact circle, not the delivery path.
    public override bool? CanDamage() => false;
    public override bool CanHitPlayer(Player target) => false;
    public override bool PreDraw(ref Color lightColor)
    {
        if (!WeaponVisuals.Visible(Projectile.Center) && !WeaponVisuals.Visible(StrikePoint, BossBlast.Radius + 32)) return false;
        Color glow = new(255, 148, 32, 0);
        Color marker = new Color(255, 214, 95) * 0.72f;
        WeaponVisuals.Arc(StrikePoint, BossBlast.Radius, 0, MathHelper.TwoPi, marker, 2, 32);
        WeaponVisuals.Line(StrikePoint - Vector2.UnitX * 7, StrikePoint + Vector2.UnitX * 7, marker, 1);
        WeaponVisuals.Line(StrikePoint - Vector2.UnitY * 7, StrikePoint + Vector2.UnitY * 7, marker, 1);
        if (!WeaponVisuals.Visible(Projectile.Center)) return false;
        int count = Math.Min(visualAge, Projectile.oldPos.Length);
        Vector2 newer = Projectile.Center;
        for (int i = 1; i < count; i++)
        {
            if (Projectile.oldPos[i] == Vector2.Zero) break;
            Vector2 older = Projectile.oldPos[i] + Projectile.Size / 2;
            if (Vector2.DistanceSquared(newer, older) > 96 * 96) break;
            float fade = 1 - i / (float)count;
            WeaponVisuals.Line(older, newer, glow * (fade * 0.48f), 6 * fade);
            newer = older;
        }
        Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
        float scale = 56f / texture.Width;
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glow * 0.28f,
            Projectile.rotation, texture.Size() / 2, scale * 1.2f, SpriteEffects.None);
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White,
            Projectile.rotation, texture.Size() / 2, scale, SpriteEffects.None);
        Vector2 exhaust = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.UnitX) * 23;
        float pulse = 0.85f + 0.15f * MathF.Sin(visualAge * 0.8f);
        WeaponVisuals.Line(exhaust, exhaust - Projectile.velocity * (0.7f * pulse), glow * 0.8f, 5 * pulse);
        WeaponVisuals.Line(exhaust, exhaust - Projectile.velocity * (0.45f * pulse), WeaponVisuals.WhiteGlow * 0.8f, 1.5f);
        return false;
    }
    public override Color? GetAlpha(Color lightColor) => Color.White;
}
