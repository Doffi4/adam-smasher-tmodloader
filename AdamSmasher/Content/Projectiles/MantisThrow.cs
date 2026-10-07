using System.IO;
using AdamSmasherMod.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.DataStructures;

namespace AdamSmasherMod.Content.Projectiles;
public class MantisThrow : ModProjectile
{
    public const int Duration = 180, OutboundTicks = 40;
    public const float Speed = 13, ReturnSpeed = 18, MaximumReach = 800, BladeRadius = 30, BladeWidth = 14;
    Item startingItem;
    int startingSlot;
    bool returning;
    float age;
    public override string Texture => "AdamSmasher/Content/Projectiles/MantisThrow";
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 76; Projectile.friendly = true; Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.ignoreWater = true;
        Projectile.extraUpdates = 1; Projectile.timeLeft = Duration * Projectile.MaxUpdates;
        Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = MantisPlayer.HitInterval * Projectile.MaxUpdates;
    }
    public override void OnSpawn(IEntitySource source)
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) return;
        Player player = Main.player[Projectile.owner]; startingSlot = player.selectedItem;
        if (Main.myPlayer == Projectile.owner) startingItem = player.HeldItem;
    }
    public override void SendExtraAI(BinaryWriter writer) { writer.Write(startingSlot); writer.Write(returning); writer.Write(age); }
    public override void ReceiveExtraAI(BinaryReader reader) { startingSlot = reader.ReadInt32(); returning = reader.ReadBoolean(); age = reader.ReadSingle(); }
    public override void AI()
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
        Player player = Main.player[Projectile.owner];
        if (!player.active || player.dead || (Main.myPlayer == Projectile.owner && !MantisPlayer.ValidSource(player, startingSlot, startingItem))) { Projectile.Kill(); return; }
        age += 1f / Projectile.MaxUpdates;
        if (age >= Duration || Vector2.DistanceSquared(player.MountedCenter, Projectile.Center) > 1800 * 1800) { Projectile.Kill(); return; }
        Vector2 target = new(Projectile.ai[1], Projectile.ai[2]);
        if (!returning && (age >= OutboundTicks || Vector2.DistanceSquared(target, Projectile.Center) <= 36 * 36))
        { returning = true; if (Main.myPlayer == Projectile.owner) Projectile.netUpdate = true; }
        if (returning)
        {
            Vector2 delta = player.MountedCenter - Projectile.Center;
            if (delta.LengthSquared() <= ReturnSpeed * ReturnSpeed) { Projectile.Kill(); return; }
            Projectile.velocity = delta.SafeNormalize(Vector2.UnitX) * ReturnSpeed;
        }
        else Projectile.velocity = Vector2.Lerp(Projectile.velocity, (target - Projectile.Center).SafeNormalize(Vector2.UnitX) * Speed, 0.14f);
        Projectile.rotation += Projectile.ai[0] * 0.55f / Projectile.MaxUpdates;
        if (!Main.dedServ) Lighting.AddLight(Projectile.Center, 0.8f, 0.03f, 0.01f);
    }
    public override bool? CanHitNPC(NPC target) => Main.player[Projectile.owner].GetModPlayer<MantisPlayer>().CanHitGroup(target) ? null : false;
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => Main.player[Projectile.owner].GetModPlayer<MantisPlayer>().RecordHitGroup(target);
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        Vector2 extent = Projectile.rotation.ToRotationVector2() * BladeRadius; float collisionPoint = 0;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center - extent, Projectile.Center + extent, BladeWidth, ref collisionPoint);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White * 0.9f, Projectile.rotation,
            texture.Size() / 2, new Vector2(BladeRadius * 2 / texture.Width, BladeWidth / texture.Height), SpriteEffects.None);
        return false;
    }
}
