using System;
using AdamSmasherMod.Common;
using AdamSmasherMod.Content.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace AdamSmasherMod.Content.Projectiles;
public class PlasmaCharge : ModProjectile
{
    public const int MinimumCharge = 60, MaximumCharge = 120, ManaInterval = 20, Cooldown = 300, FlightLifetime = 150;
    public const float HeavyDamageMultiplier = 4;
    Item startingItem;
    int remoteAge;
    bool fieldCreated;
    bool flightInitialized;
    int visualAge;
    int visualFlightAge;
    bool readyCueTriggered;
    Vector2 launchCenter;
    public override string Texture => "AdamSmasher/Content/Projectiles/PlasmaCharge";
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 8;
        ProjectileID.Sets.TrailingMode[Type] = 0;
    }
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 32; Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 1; Projectile.tileCollide = false; Projectile.ignoreWater = true;
        Projectile.timeLeft = FlightLifetime; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override void OnSpawn(IEntitySource source)
    {
        if (Main.myPlayer == Projectile.owner && Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
            startingItem = Main.player[Projectile.owner].HeldItem;
    }
    public override bool? CanDamage() => Projectile.ai[0] == 1 ? null : false;
    public override bool ShouldUpdatePosition() => Projectile.ai[0] == 1;
    public override void AI()
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
        Player player = Main.player[Projectile.owner];
        bool owner = Main.myPlayer == Projectile.owner;
        if (Projectile.ai[0] == 0)
        {
            Projectile.Center = player.MountedCenter + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 42;
            Projectile.timeLeft = 2; player.heldProj = Projectile.whoAmI;
            if (owner)
            {
                // Cancellation always wins over a false controlUseTile release.
                if (!player.active || player.dead || player.noItems || player.CCed || player.silence || player.mouseInterface || Main.blockMouse ||
                    !Main.hasFocus || Main.blockInput || Main.editSign || Main.editChest ||
                    (PlayerInput.LockGamepadTileUseButton && PlayerInput.UsingGamepad) || PlayerInput.InBuildingMode ||
                    player.selectedItem != (int)Projectile.ai[2] || player.HeldItem.type != ModContent.ItemType<AnnihilationProtocol>() ||
                    !ReferenceEquals(startingItem, player.HeldItem) || player.statMana <= 0)
                { Cancel(); return; }
                player.SetDummyItemTime(2);
                Vector2 aim = (Main.MouseWorld - player.MountedCenter).SafeNormalize(new Vector2(player.direction, 0)) * SmartRocket.Speed;
                if (Vector2.DistanceSquared(aim, Projectile.velocity) > 0.5f) { Projectile.velocity = aim; Projectile.netUpdate = true; }
                if (!player.controlUseTile)
                {
                    if (Projectile.ai[1] >= MinimumCharge) Release(player); else Cancel();
                    return;
                }
                Projectile.ai[1]++;
                if ((int)Projectile.ai[1] % ManaInterval == 0)
                {
                    if (!player.CheckMana(player.HeldItem, -1, true, true)) { Cancel(); return; }
                    player.manaRegenDelay = (int)player.maxRegenDelay; Projectile.netUpdate = true;
                }
                if (Projectile.ai[1] >= MaximumCharge) Release(player);
            }
            else if (++remoteAge > MaximumCharge + 30) { Projectile.Kill(); return; }
            Projectile.scale = 0.65f + MathHelper.Clamp(Projectile.ai[1] / MaximumCharge, 0, 1) * 0.75f;
        }
        else if (Projectile.ai[0] == 1)
        {
            // timeLeft/scale are not projectile ai: each peer must initialize the
            // flight after the owner publishes the mode change from holdout.
            if (!flightInitialized) { Projectile.timeLeft = FlightLifetime; flightInitialized = true; }
            Projectile.scale = 1.4f;
            Projectile.friendly = true; Projectile.tileCollide = true;
            if (!player.active || player.dead || Vector2.DistanceSquared(player.Center, Projectile.Center) > SmartRocket.TravelRange * SmartRocket.TravelRange)
            { Projectile.ai[0] = 2; Projectile.Kill(); return; }
            if (owner && (int)Projectile.localAI[0]++ % SmartRocket.RetargetInterval == 0)
            {
                int target = SmartRocket.FindTarget(Projectile.Center, Projectile.Center);
                Projectile.ai[2] = target + 1; Projectile.netUpdate = true;
            }
            int index = (int)Projectile.ai[2] - 1;
            if (owner && SmartRocket.ValidTarget(index, Projectile.Center))
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, (Main.npc[index].Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * SmartRocket.Speed, 0.07f).SafeNormalize(Vector2.UnitX) * SmartRocket.Speed;
        }
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (!Main.dedServ)
        {
            Lighting.AddLight(Projectile.Center, 0.8f, 0.06f, 0.01f);
            UpdateVisuals();
        }
    }
    void UpdateVisuals()
    {
        visualAge++;
        if (Projectile.ai[0] == 0)
        {
            if (!readyCueTriggered && Projectile.ai[1] >= MinimumCharge)
            {
                readyCueTriggered = true;
                WeaponVisuals.Burst(Projectile.Center, 6, 2.2f, 18, visualAge * 0.1f);
            }
            if (visualAge % 10 == 0)
            {
                Vector2 radial = (visualAge * 0.35f).ToRotationVector2();
                float charge = MathHelper.Clamp(Projectile.ai[1] / MaximumCharge, 0, 1);
                WeaponVisuals.Particle(Projectile.Center + radial * (24 + charge * 10), -radial * 1.6f, 0.8f);
            }
        }
        else if (Projectile.ai[0] == 1)
        {
            if (visualFlightAge++ == 0)
            {
                launchCenter = Projectile.Center;
                WeaponVisuals.Burst(launchCenter, 8, 3, 8, Projectile.rotation);
            }
            if (visualFlightAge % 4 == 0)
            {
                Vector2 backward = -Projectile.velocity.SafeNormalize(Vector2.UnitX);
                WeaponVisuals.Particle(Projectile.Center + backward * 16, backward * 1.8f, 1.1f);
            }
        }
    }
    void Cancel() { Projectile.ai[0] = 2; Projectile.netUpdate = true; Projectile.Kill(); }
    void Release(Player player)
    {
        if (player.GetModPlayer<AnnihilationPlayer>().PlasmaCooldownTicks > 0) { Cancel(); return; }
        Projectile.ai[0] = 1; Projectile.ai[2] = 0;
        Projectile.damage = (int)(Projectile.damage * HeavyDamageMultiplier);
        Projectile.friendly = true; Projectile.tileCollide = true; Projectile.timeLeft = FlightLifetime;
        flightInitialized = true;
        Projectile.scale = 1.4f; Projectile.netUpdate = true;
        player.GetModPlayer<AnnihilationPlayer>().StartPlasmaCooldown(Cooldown);
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => CreateField();
    public override void OnKill(int timeLeft)
    {
        if (!Main.dedServ && Projectile.ai[0] == 1) WeaponVisuals.Burst(Projectile.Center, 8, 4.5f, 8, Projectile.rotation);
        CreateField();
    }
    void CreateField()
    {
        if (fieldCreated || Projectile.ai[0] != 1 || Main.myPlayer != Projectile.owner) return;
        fieldCreated = true;
        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero, ModContent.ProjectileType<PlasmaField>(),
            (int)(Projectile.damage * PlasmaField.DamageFraction), Projectile.knockBack, Projectile.owner);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        if (!WeaponVisuals.Visible(Projectile.Center)) return false;
        Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
        bool flying = Projectile.ai[0] == 1;
        float charge = MathHelper.Clamp(Projectile.ai[1] / MaximumCharge, 0, 1);
        float pulse = 0.88f + 0.12f * MathF.Sin(visualAge * (flying ? 0.4f : 0.12f + charge * 0.13f));
        if (flying) WeaponVisuals.Trail(Projectile, visualFlightAge, 16);
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, WeaponVisuals.RedGlow * (0.32f * pulse),
            -visualAge * 0.06f, texture.Size() / 2, Projectile.scale * (1.3f + charge * 0.12f), SpriteEffects.None);
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White,
            Projectile.rotation, texture.Size() / 2, Projectile.scale, SpriteEffects.None);

        if (!flying)
        {
            float radius = 14 + 15 * charge;
            for (int i = 0; i < 2; i++)
            {
                float angle = visualAge * (0.035f + charge * 0.045f) + i * MathHelper.Pi;
                WeaponVisuals.Arc(Projectile.Center, radius, angle, 1.2f, WeaponVisuals.RedGlow * (0.4f + charge * 0.35f), 2, 6);
                WeaponVisuals.Spark(Projectile.Center + angle.ToRotationVector2() * radius, angle + MathHelper.PiOver2, 3 + charge * 3, 0.6f);
            }
            // The white outer circuit appears only once the shot can be released.
            if (Projectile.ai[1] >= MinimumCharge)
                WeaponVisuals.Arc(Projectile.Center, radius + 5, -visualAge * 0.055f, 4.7f,
                    WeaponVisuals.WhiteGlow * (0.48f * pulse), 1, 14);
        }
        else
        {
            Vector2 backward = -Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 exhaust = Projectile.Center + backward * 18;
            WeaponVisuals.Line(exhaust, exhaust + backward * (20 + pulse * 10), WeaponVisuals.RedGlow * 0.55f, 9 * pulse);
            WeaponVisuals.Line(exhaust, exhaust + backward * 20, WeaponVisuals.WhiteGlow * 0.6f, 2.5f);
            if (visualFlightAge < 10)
            {
                float flash = 1 - visualFlightAge / 10f;
                WeaponVisuals.Arc(launchCenter, 12 + visualFlightAge * 3, Projectile.rotation - 1.3f, 2.6f,
                    WeaponVisuals.WhiteGlow * (0.7f * flash), 2, 10);
            }
        }
        return false;
    }
    public override Color? GetAlpha(Color lightColor) => Color.White;
}
