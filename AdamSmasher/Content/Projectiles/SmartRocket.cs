using System;
using AdamSmasherMod.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AdamSmasherMod.Content.Projectiles;
public class SmartRocket : ModProjectile
{
    public const float Speed = 18, TargetRange = 900, TravelRange = 2400;
    public const int Lifetime = 150, RetargetInterval = 12;
    bool exploded;
    int visualAge;
    Vector2 launchCenter;
    public override string Texture => "AdamSmasher/Content/Projectiles/SmartRocket";
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 8;
        ProjectileID.Sets.TrailingMode[Type] = 0;
    }
    public static int Group(NPC npc) => npc.realLife >= 0 && npc.realLife < Main.maxNPCs ? npc.realLife : npc.whoAmI;
    public static bool ValidTarget(int index, Vector2 origin) => index >= 0 && index < Main.maxNPCs &&
        Main.npc[index].CanBeChasedBy() && Vector2.DistanceSquared(Main.npc[index].Center, origin) <= TargetRange * TargetRange;
    public static int FindTarget(Vector2 aim, Vector2 origin, int excluded = -1)
    {
        int chosen = -1, excludedGroup = excluded >= 0 && excluded < Main.maxNPCs ? Group(Main.npc[excluded]) : -1;
        float nearest = TargetRange * TargetRange;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            // Canonical roots prevent worm segments from consuming both target slots.
            if (Group(npc) != npc.whoAmI || Group(npc) == excludedGroup || !ValidTarget(npc.whoAmI, origin)) continue;
            float distance = Vector2.DistanceSquared(npc.Center, aim);
            if (distance < nearest) { nearest = distance; chosen = npc.whoAmI; }
        }
        return chosen;
    }
    public override void SetDefaults()
    {
        Projectile.width = 26; Projectile.height = 12; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic; Projectile.penetrate = 1;
        Projectile.timeLeft = Lifetime; Projectile.tileCollide = true; Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override void AI()
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers || !Main.player[Projectile.owner].active || Main.player[Projectile.owner].dead ||
            Vector2.DistanceSquared(Main.player[Projectile.owner].Center, Projectile.Center) > TravelRange * TravelRange)
        { Projectile.Kill(); return; }
        if (Main.myPlayer == Projectile.owner)
        {
            if ((int)Projectile.localAI[0]++ % RetargetInterval == 0)
            {
                int target = (int)Projectile.ai[0] - 1;
                if (!ValidTarget(target, Projectile.Center))
                {
                    Projectile.ai[0] = FindTarget(new Vector2(Projectile.ai[1], Projectile.ai[2]), Projectile.Center) + 1;
                    Projectile.netUpdate = true;
                }
            }
            int index = (int)Projectile.ai[0] - 1;
            if (ValidTarget(index, Projectile.Center))
            {
                Vector2 desired = (Main.npc[index].Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * Speed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.085f).SafeNormalize(Vector2.UnitX) * Speed;
                if ((int)Projectile.localAI[0] % RetargetInterval == 0) Projectile.netUpdate = true;
            }
        }
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (!Main.dedServ)
        {
            Lighting.AddLight(Projectile.Center, 0.7f, 0.08f, 0.02f);
            if (visualAge++ == 0)
            {
                launchCenter = Projectile.Center;
                WeaponVisuals.Burst(launchCenter, 4, 2.2f, 3, Projectile.rotation);
            }
            if (visualAge % 4 == 0)
            {
                Vector2 backward = -Projectile.velocity.SafeNormalize(Vector2.UnitX);
                WeaponVisuals.Particle(Projectile.Center + backward * 12, backward * 1.5f, 0.8f);
            }
        }
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => Explode(Group(target));
    public override void OnKill(int timeLeft) => Explode(-1);
    void Explode(int excludedGroup)
    {
        if (exploded || Main.myPlayer != Projectile.owner) return;
        exploded = true;
        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero, ModContent.ProjectileType<RocketExplosion>(),
            (int)(Projectile.damage * RocketExplosion.DamageFraction), Projectile.knockBack, Projectile.owner, excludedGroup + 1);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        if (!WeaponVisuals.Visible(Projectile.Center)) return false;
        Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
        WeaponVisuals.Trail(Projectile, visualAge, 8);
        float pulse = 0.88f + 0.12f * MathF.Sin(visualAge * 0.55f);
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, WeaponVisuals.RedGlow * (0.3f * pulse),
            Projectile.rotation, texture.Size() / 2, Projectile.scale * 1.3f, SpriteEffects.None);
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White,
            Projectile.rotation, texture.Size() / 2, Projectile.scale, SpriteEffects.None);
        Vector2 backward = -Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 exhaust = Projectile.Center + backward * 14;
        WeaponVisuals.Line(exhaust, exhaust + backward * (12 + 6 * pulse), WeaponVisuals.RedGlow * 0.7f, 5 * pulse);
        WeaponVisuals.Line(exhaust, exhaust + backward * (8 + 4 * pulse), WeaponVisuals.WhiteGlow * 0.8f, 1.5f);
        if (visualAge < 7)
        {
            float flash = 1 - visualAge / 7f;
            WeaponVisuals.Arc(launchCenter, 6 + visualAge * 2.5f, Projectile.rotation - 1.1f, 2.2f,
                WeaponVisuals.WhiteGlow * (flash * 0.65f), 2, 6);
        }
        return false;
    }
    public override Color? GetAlpha(Color lightColor) => Color.White;
}
