using System.IO;
using System.Collections.Generic;
using AdamSmasherMod.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.DataStructures;

namespace AdamSmasherMod.Content.Projectiles;
public class MantisSlash : ModProjectile
{
    public const float InnerRadius = 35, OuterRadius = 175, BladeWidth = 18, SweepAngle = 2.6f;
    public const float MaximumOuterRadius = 315, MaximumBladeWidth = 28;
    readonly HashSet<int> hitGroups = new();
    Item startingItem;
    int startingSlot;
    float rampStrength;
    float CurrentOuterRadius => MathHelper.Lerp(OuterRadius, MaximumOuterRadius, rampStrength);
    float CurrentBladeWidth => MathHelper.Lerp(BladeWidth, MaximumBladeWidth, rampStrength);
    public override string Texture => "AdamSmasher/Content/Projectiles/MantisSlash";
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 360; Projectile.friendly = true; Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.ignoreWater = true;
        Projectile.timeLeft = 120; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override void OnSpawn(IEntitySource source)
    {
        RestoreRemainingLifetime();
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) return;
        Player player = Main.player[Projectile.owner]; startingSlot = player.selectedItem;
        if (Main.myPlayer == Projectile.owner)
        { startingItem = player.HeldItem; rampStrength = player.GetModPlayer<MantisPlayer>().RampStrength; }
        RestoreGeometry();
    }
    public override void SendExtraAI(BinaryWriter writer) { writer.Write(startingSlot); writer.Write(Projectile.localAI[0]); writer.Write(rampStrength); }
    public override void ReceiveExtraAI(BinaryReader reader)
    {
        startingSlot = reader.ReadInt32(); Projectile.localAI[0] = reader.ReadSingle();
        float strength = reader.ReadSingle();
        rampStrength = float.IsFinite(strength) ? MathHelper.Clamp(strength, 0, 1) : 0;
        RestoreGeometry();
        // SyncProjectile calls SetDefaults + ReceiveExtraAI, never owner OnSpawn or a timeLeft sync.
        RestoreRemainingLifetime();
    }
    void RestoreGeometry()
    {
        // Keep the engine's broadphase large enough for every drawn/collidable trail, preserving its center.
        Vector2 center = Projectile.Center;
        Projectile.width = Projectile.height = (int)System.Math.Ceiling(CurrentOuterRadius * 2 + CurrentBladeWidth);
        Projectile.Center = center;
    }
    void RestoreRemainingLifetime()
    {
        float remaining = System.Math.Max(2, Projectile.ai[2]) - Projectile.localAI[0];
        if (!float.IsFinite(remaining) || remaining <= 0) { Projectile.Kill(); return; }
        Projectile.timeLeft = (int)System.Math.Ceiling(remaining * Projectile.MaxUpdates) + 1;
    }
    public override bool ShouldUpdatePosition() => false;
    public override void AI()
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
        Player player = Main.player[Projectile.owner];
        if (!player.active || player.dead || (Main.myPlayer == Projectile.owner && !MantisPlayer.ValidSource(player, startingSlot, startingItem))) { Projectile.Kill(); return; }
        Projectile.Center = player.MountedCenter; player.heldProj = Projectile.whoAmI;
        Projectile.localAI[0] += 1f / Projectile.MaxUpdates;
        if (Projectile.localAI[0] >= System.Math.Max(2, Projectile.ai[2])) { Projectile.Kill(); return; }
        if (!Main.dedServ) Lighting.AddLight(Projectile.Center, 0.8f + rampStrength * 0.4f, 0.03f + rampStrength * 0.2f, 0.01f);
    }
    public override bool? CanDamage() => Projectile.localAI[0] <= 0 ? false : null;
    public override bool? CanHitNPC(NPC target) => hitGroups.Contains(MantisPlayer.Group(target)) || !Main.player[Projectile.owner].GetModPlayer<MantisPlayer>().CanHitGroup(target) ? false : null;
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        hitGroups.Add(MantisPlayer.Group(target)); Main.player[Projectile.owner].GetModPlayer<MantisPlayer>().RecordHitGroup(target);
    }
    // Drawing and collision use the same three short trail lines, rather than a full square.
    float Angle(int blade, int sample)
    {
        float progress = MathHelper.Clamp((Projectile.localAI[0] - sample * 0.65f) / System.Math.Max(2, Projectile.ai[2]), 0, 1);
        float offset = (progress - 0.5f) * SweepAngle;
        return Projectile.ai[1] + (Projectile.ai[0] == 1 || blade == 1 ? -offset : offset);
    }
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        for (int blade = 0; blade < (Projectile.ai[0] == 2 ? 2 : 1); blade++)
            for (int sample = 0; sample < 3; sample++)
            {
                Vector2 direction = Angle(blade, sample).ToRotationVector2(); float collisionPoint = 0;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center + direction * InnerRadius,
                    Projectile.Center + direction * CurrentOuterRadius, CurrentBladeWidth, ref collisionPoint)) return true;
            }
        return false;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
        for (int blade = 0; blade < (Projectile.ai[0] == 2 ? 2 : 1); blade++)
            for (int sample = 2; sample >= 0; sample--)
            {
                float angle = Angle(blade, sample);
                Vector2 midpoint = Projectile.Center + angle.ToRotationVector2() * ((InnerRadius + CurrentOuterRadius) / 2);
                Color heat = Color.Lerp(Color.White, new Color(255, 160, 105), rampStrength);
                Main.EntitySpriteDraw(texture, midpoint - Main.screenPosition, null, heat * ((0.78f + rampStrength * 0.17f) * (1 - sample * 0.23f)),
                    angle, texture.Size() / 2, new Vector2((CurrentOuterRadius - InnerRadius) / texture.Width, CurrentBladeWidth / texture.Height), SpriteEffects.None);
            }
        return false;
    }
}
