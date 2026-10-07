using System;
using AdamSmasherMod.Common;
using AdamSmasherMod.Content.NPCs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AdamSmasherMod.Content.Projectiles;
public class BossBullet : ModProjectile
{
    private int visualAge;
    public override string Texture => "AdamSmasher/Content/Projectiles/BossBullet";
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 7;
        ProjectileID.Sets.TrailingMode[Type] = 0;
    }
    public override void SetDefaults()
    {
        Projectile.width = 18;
        Projectile.height = 6;
        Projectile.hostile = true;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 100;
        Projectile.aiStyle = -1;
    }
    internal static bool LiveBoss(Projectile projectile)
    {
        int index = (int)projectile.ai[0];
        return index >= 0 && index < Main.maxNPCs && Main.npc[index].active &&
            Main.npc[index].ModNPC is AdamSmasherBoss boss && boss.State != AdamSmasherBoss.AttackState.Retire &&
            Vector2.DistanceSquared(projectile.Center, Main.npc[index].Center) < 2800 * 2800;
    }
    public override void AI()
    {
        if (!LiveBoss(Projectile)) { Projectile.Kill(); return; }
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (!Main.dedServ)
        {
            visualAge++;
            Lighting.AddLight(Projectile.Center, 0.5f, 0.17f, 0.015f);
            if (visualAge % 8 == 0) WeaponVisuals.Particle(Projectile.Center, -Projectile.velocity * 0.05f, 0.55f);
        }
    }
    public override bool CanHitPlayer(Player target) => LiveBoss(Projectile);
    public override bool PreDraw(ref Color lightColor)
    {
        if (!WeaponVisuals.Visible(Projectile.Center)) return false;
        Color glow = new(255, 150, 35, 0);
        Vector2 newer = Projectile.Center;
        int count = Math.Min(visualAge, Projectile.oldPos.Length);
        for (int i = 1; i < count; i++)
        {
            if (Projectile.oldPos[i] == Vector2.Zero) break;
            Vector2 older = Projectile.oldPos[i] + Projectile.Size / 2;
            if (Vector2.DistanceSquared(newer, older) > 96 * 96) break;
            float fade = 1 - i / (float)count;
            WeaponVisuals.Line(older, newer, glow * (fade * 0.55f), 3 * fade);
            newer = older;
        }
        Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
        float scale = 32f / texture.Width;
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glow * 0.4f,
            Projectile.rotation, texture.Size() / 2, scale * 1.2f, SpriteEffects.None);
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White,
            Projectile.rotation, texture.Size() / 2, scale, SpriteEffects.None);
        return false;
    }
    public override Color? GetAlpha(Color lightColor) => Color.White;
}
