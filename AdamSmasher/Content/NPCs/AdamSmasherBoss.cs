using System;
using System.IO;
using AdamSmasherMod.Common;
using AdamSmasherMod.Content.Items;
using AdamSmasherMod.Content.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AdamSmasherMod.Content.NPCs;

[AutoloadBossHead]
public class AdamSmasherBoss : ModNPC
{
    public const int BaseLife = 18_000_000;
    public const int BaseContactDamage = 600;
    public const int BaseDefense = 110;
    public const int ProjectileLimit = 20;
    public const float EngagementRange = 3000f;
    public const int BulletDamage = 350;
    public const int RocketDamage = 450;
    public const int BlastDamage = 450;
    public enum AttackState { Reposition, MissileWarning, Salvo, GunWarning, Barrage, DashWarning, Dash, Recovery, Overload, Retire }
    public AttackState State => (AttackState)(int)NPC.ai[0];
    public bool Overloaded => NPC.ai[2] != 0;
    private bool ActiveDash => State == AttackState.Dash && Timer < dashDuration;
    public Vector2 LockedDirection => lockedDirection;
    public Vector2 LockedAim => lockedAim;
    public int DashDuration => dashDuration;
    public int BurstIndex => burstIndex;
    public int LockRemaining => IsWarning ? Math.Max(0, WarningDuration - Timer + 1) : 0;
    public bool AimLocked => IsWarning && LockRemaining <= 24;
    private Vector2 lockedDirection = Vector2.UnitX;
    private Vector2 lockedAim;
    private int dashDuration = 14;
    private int burstIndex;
    private int nextAttack;
    private bool Authority => Main.netMode != NetmodeID.MultiplayerClient;
    private int Timer => (int)NPC.ai[1];
    private bool IsWarning => State is AttackState.MissileWarning or AttackState.GunWarning or AttackState.DashWarning;
    private int WarningDuration => State == AttackState.MissileWarning ? (Overloaded ? 42 : 48) : (Overloaded ? 36 : 42);
    private float DashSpeed => Overloaded ? 50 : 44;
    private float DashLength => MathHelper.Clamp(Vector2.Distance(NPC.Center, lockedAim) + 180, 600, 1000);
    internal int OwnedProjectileCount => CountProjectiles();
    public override string Texture => "AdamSmasher/Content/NPCs/AdamSmasherBoss";
    public override string BossHeadTexture => "AdamSmasher/Content/NPCs/AdamSmasherBoss_Head_Boss";

    public override void ModifyNPCLoot(NPCLoot npcLoot)
    {
        npcLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<SmasherBag>()));
        var normal = new LeadingConditionRule(new Conditions.NotExpert());
        normal.OnSuccess(SmasherLoot.CreateWeaponRule());
        npcLoot.Add(normal);
    }

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 4;
        // Warnings may intersect the player screen while the boss body is outside it.
        NPCID.Sets.MustAlwaysDraw[Type] = true;
    }

    public override void SetDefaults()
    {
        NPC.width = 96;
        NPC.height = 140;
        NPC.damage = BaseContactDamage;
        NPC.defense = BaseDefense;
        NPC.lifeMax = BaseLife;
        NPC.boss = true;
        NPC.aiStyle = -1;
        NPC.noGravity = true;
        NPC.noTileCollide = true;
        NPC.knockBackResist = 0f;
        NPC.npcSlots = 10f;
        NPC.HitSound = SoundID.NPCHit4;
        NPC.DeathSound = SoundID.NPCDeath14;
        if (!Main.dedServ)
            Music = MusicID.Boss2;
    }

    // tML already applied expert/master life/damage modifiers before this hook.
    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        => NPC.lifeMax = (int)(NPC.lifeMax * balance * bossAdjustment);

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(lockedDirection.X);
        writer.Write(lockedDirection.Y);
        writer.Write(nextAttack);
        writer.Write(lockedAim.X);
        writer.Write(lockedAim.Y);
        writer.Write(dashDuration);
        writer.Write(burstIndex);
    }
    public override void ReceiveExtraAI(BinaryReader reader)
    {
        lockedDirection = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        nextAttack = reader.ReadInt32();
        lockedAim = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        dashDuration = reader.ReadInt32();
        burstIndex = reader.ReadInt32();
    }

    private bool ValidTarget(int index) => index >= 0 && index < Main.maxPlayers && Main.player[index].active &&
        !Main.player[index].dead && Vector2.DistanceSquared(Main.player[index].Center, NPC.Center) <= EngagementRange * EngagementRange;

    private void AcquireTarget()
    {
        if (ValidTarget(NPC.target)) return;
        int best = -1;
        float distance = EngagementRange * EngagementRange;
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            if (!ValidTarget(i)) continue;
            float candidate = Vector2.DistanceSquared(Main.player[i].Center, NPC.Center);
            if (candidate <= distance) { best = i; distance = candidate; }
        }
        NPC.target = best < 0 ? Main.maxPlayers : best;
        NPC.netUpdate = true;
    }

    private void Enter(AttackState state, Player target = null)
    {
        if (!Authority) return;
        NPC.ai[0] = (int)state;
        NPC.ai[1] = 0;
        if (IsWarning)
        {
            UpdateAim(target);
            NPC.velocity *= 0.2f;
        }
        NPC.netUpdate = true;
    }

    private void UpdateAim(Player target)
    {
        Vector2 lead = State == AttackState.MissileWarning ? Vector2.Zero : target.velocity * (Overloaded ? 4 : 2);
        if (lead.LengthSquared() > 200 * 200) lead = lead.SafeNormalize(Vector2.UnitX) * 200;
        lockedAim = target.Center + lead;
        lockedDirection = (lockedAim - NPC.Center).SafeNormalize(Vector2.UnitX);
    }

    private void Warning(Player target)
    {
        int lockTick = WarningDuration - 24;
        // Freeze before the final snapshot: all following twenty-four updates share its origin.
        if (Timer >= lockTick) NPC.velocity = Vector2.Zero;
        else NPC.velocity *= 0.85f;
        if (Authority && Timer <= lockTick)
        {
            UpdateAim(target);
            if (Timer % 4 == 0 || Timer == lockTick) NPC.netUpdate = true;
        }
    }

    private Vector2 StrikePoint(int index) => lockedAim + (index switch
    {
        1 => new Vector2(-180, 0),
        2 => new Vector2(180, 0),
        3 => new Vector2(0, -180),
        4 => new Vector2(0, 180),
        _ => Vector2.Zero
    });

    public override void AI()
    {
        NPC.damage = ActiveDash ? NPC.defDamage : 0;
        if (State == AttackState.Retire)
        {
            NPC.damage = 0;
            NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(0, -12), 0.06f);
            NPC.EncourageDespawn(60);
            if (++NPC.ai[1] >= 60 && Authority)
            {
                NPC.active = false;
                if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
            }
            return;
        }
        if (Authority) AcquireTarget();
        if (!ValidTarget(NPC.target))
        {
            NPC.damage = 0;
            if (Authority) { ClearProjectiles(); Enter(AttackState.Retire); }
            return;
        }
        Player target = Main.player[NPC.target];
        if (Authority && !Overloaded && NPC.life <= NPC.lifeMax / 2)
        {
            NPC.ai[2] = 1;
            NPC.ai[3] = 0;
            nextAttack = 0;
            burstIndex = 0;
            NPC.damage = 0;
            ClearProjectiles();
            Enter(AttackState.Overload);
        }
        NPC.direction = NPC.spriteDirection = (State is AttackState.Reposition or AttackState.Recovery ? target.Center.X - NPC.Center.X : lockedDirection.X) >= 0 ? 1 : -1;
        if (!Main.dedServ) Lighting.AddLight(NPC.Center, Overloaded ? 0.65f : 0.25f, 0.025f, 0.01f);
        AttackState startingState = State;
        switch (State)
        {
            case AttackState.Reposition:
                Vector2 destination = target.Center + new Vector2(NPC.Center.X < target.Center.X ? -480 : 480, nextAttack == 2 ? -80 : -180);
                Vector2 desired = (destination - NPC.Center) * 0.065f;
                float moveSpeed = Overloaded ? 26 : 22;
                if (desired.LengthSquared() > moveSpeed * moveSpeed) desired = desired.SafeNormalize(Vector2.UnitX) * moveSpeed;
                NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.18f);
                if (Authority && Timer >= (Overloaded ? 24 : 36))
                {
                    AttackState warning = nextAttack switch { 0 => AttackState.MissileWarning, 1 => AttackState.GunWarning, _ => AttackState.DashWarning };
                    if (warning == AttackState.DashWarning) NPC.ai[3] = 0;
                    if (warning == AttackState.GunWarning) burstIndex = 0;
                    Enter(warning, target);
                }
                break;
            case AttackState.MissileWarning:
                Warning(target);
                if (Timer >= WarningDuration) Enter(AttackState.Salvo);
                break;
            case AttackState.Salvo:
                NPC.velocity *= 0.9f;
                int count = Overloaded ? 5 : 3;
                if (Timer < count * 8 && Timer % 8 == 0) FireRocket(StrikePoint(Timer / 8));
                if (Timer >= count * 8 + 8) FinishAttack(target);
                break;
            case AttackState.GunWarning:
                Warning(target);
                if (Timer >= WarningDuration) Enter(AttackState.Barrage);
                break;
            case AttackState.Barrage:
                NPC.velocity *= 0.9f;
                // Three volleys at 0/12/24, followed by twelve recovery ticks.
                if (Timer <= 24 && Timer % 12 == 0)
                {
                    for (int lane = -1; lane <= 1; lane++)
                        Fire(ModContent.ProjectileType<BossBullet>(), lockedDirection.RotatedBy(lane * 0.10f) * 24, BulletDamage);
                }
                if (Authority && Timer >= 36)
                {
                    burstIndex++;
                    if (burstIndex < (Overloaded ? 3 : 2)) Enter(AttackState.GunWarning, target);
                    else FinishAttack(target);
                }
                break;
            case AttackState.DashWarning:
                Warning(target);
                if (Authority && Timer >= WarningDuration)
                {
                    dashDuration = (int)Math.Ceiling(DashLength / DashSpeed);
                    NPC.velocity = lockedDirection * DashSpeed;
                    Enter(AttackState.Dash);
                }
                break;
            case AttackState.Dash:
                if (Timer >= dashDuration)
                {
                    NPC.damage = 0;
                    if (Authority || Timer == dashDuration) NPC.velocity *= 0.15f;
                    if (Authority) NPC.ai[3]++;
                    Enter(AttackState.Recovery);
                }
                break;
            case AttackState.Recovery:
                NPC.velocity *= 0.88f;
                if (Timer >= (Overloaded ? 16 : 20))
                {
                    if (NPC.ai[3] < (Overloaded ? 3 : 2)) Enter(AttackState.DashWarning, target);
                    else FinishAttack(target);
                }
                break;
            case AttackState.Overload:
                NPC.damage = 0;
                NPC.velocity *= 0.9f;
                if (Timer >= 90) Enter(AttackState.Reposition);
                break;
        }
        if (State == startingState) NPC.ai[1]++;
        NPC.damage = ActiveDash ? NPC.defDamage : 0;
    }

    private void FinishAttack(Player target)
    {
        if (!Authority) return;
        if (Overloaded && State == AttackState.Salvo)
        {
            nextAttack = 1;
            burstIndex = 0;
            Enter(AttackState.GunWarning, target);
            return;
        }
        nextAttack = (nextAttack + 1) % 3;
        Enter(AttackState.Reposition);
    }
    private void FireRocket(Vector2 point)
    {
        if (!Authority || CountProjectiles() >= ProjectileLimit) return;
        Vector2 origin = NPC.Center + lockedDirection * 40;
        Projectile.NewProjectile(NPC.GetSource_FromAI(), origin, (point - origin).SafeNormalize(lockedDirection) * BossRocket.Speed,
            ModContent.ProjectileType<BossRocket>(), RocketDamage, 0, Main.myPlayer, NPC.whoAmI, point.X, point.Y);
    }
    private void Fire(int type, Vector2 velocity, int baseDamage)
    {
        if (!Authority || CountProjectiles() >= ProjectileLimit) return;
        // Hostile projectile hurt applies difficulty scaling itself; defDamage has already been scaled by NPC defaults.
        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + lockedDirection * 40, velocity,
            type, baseDamage, 0, Main.myPlayer, NPC.whoAmI, NPC.target);
    }
    private bool Owns(Projectile projectile) => projectile.active && (projectile.type == ModContent.ProjectileType<BossRocket>() ||
        projectile.type == ModContent.ProjectileType<BossBullet>() || projectile.type == ModContent.ProjectileType<BossBlast>()) && projectile.ai[0] == NPC.whoAmI;
    private int CountProjectiles()
    {
        int count = 0;
        for (int i = 0; i < Main.maxProjectiles; i++) if (Owns(Main.projectile[i])) count++;
        return count;
    }
    private void ClearProjectiles()
    {
        if (!Authority) return;
        for (int i = 0; i < Main.maxProjectiles; i++) if (Owns(Main.projectile[i])) Main.projectile[i].Kill();
    }
    public override bool CanHitPlayer(Player target, ref int cooldownSlot) => ActiveDash && ValidTarget(target.whoAmI);
    public override void OnKill() { ClearProjectiles(); SmasherWorld.MarkDowned(); }
    public override void FindFrame(int frameHeight)
    {
        int frame = State == AttackState.Dash ? 2 : Overloaded ? 3 : State is AttackState.Barrage or AttackState.Salvo ? 1 : 0;
        NPC.frame.Y = frame * frameHeight;
    }
    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (IsWarning)
        {
            float charge = MathHelper.Clamp(Timer / (float)WarningDuration, 0, 1);
            Color telegraph = (AimLocked ? new Color(255, 216, 95) : new Color(255, 122, 32)) * (AimLocked ? 0.65f + charge * 0.25f : 0.35f);
            if (State == AttackState.MissileWarning)
            {
                for (int i = 0; i < (Overloaded ? 5 : 3); i++)
                    DrawReticle(spriteBatch, screenPos, StrikePoint(i), BossBlast.Radius, telegraph, !AimLocked);
            }
            else if (State == AttackState.GunWarning)
            {
                for (int lane = -1; lane <= 1; lane++)
                    DrawLine(spriteBatch, screenPos, NPC.Center + lockedDirection * 40, lockedDirection.RotatedBy(lane * 0.10f), 1900,
                        telegraph * (lane == 0 ? 1 : 0.55f), lane == 0 ? 2 : 1, !AimLocked);
                DrawReticle(spriteBatch, screenPos, lockedAim, 18, telegraph, !AimLocked);
            }
            else
            {
                float length = (float)Math.Ceiling(DashLength / DashSpeed) * DashSpeed;
                DrawLine(spriteBatch, screenPos, NPC.Center, lockedDirection, length, telegraph, 3, !AimLocked);
                Vector2 edge = lockedDirection.RotatedBy(MathHelper.PiOver2) * 90;
                for (int side = -1; side <= 1; side += 2)
                    DrawLine(spriteBatch, screenPos, NPC.Center + edge * side, lockedDirection, length, telegraph * 0.3f, 1, true);
            }
        }
        if (State == AttackState.Salvo)
        {
            // Unlaunched points keep their marker; launched rockets draw theirs until impact.
            for (int i = (Timer + 7) / 8; i < (Overloaded ? 5 : 3); i++)
                DrawReticle(spriteBatch, screenPos, StrikePoint(i), BossBlast.Radius, new Color(255, 216, 95) * 0.8f, false);
        }
        if (State == AttackState.Dash)
        {
            Vector2 backward = -lockedDirection;
            for (int i = 1; i <= 3; i++)
                spriteBatch.Draw(TextureAssets.Npc[Type].Value, NPC.Center + backward * (i * 32) - screenPos, NPC.frame,
                    new Color(255, 145, 45, 0) * ((4 - i) * 0.12f), NPC.rotation, new Vector2(72, 88), NPC.scale,
                    NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
        }
        Texture2D texture = TextureAssets.Npc[Type].Value;
        // Sheet faces left. Centering each 144x176 frame aligns it with the hitbox.
        SpriteEffects flip = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        spriteBatch.Draw(texture, NPC.Center - screenPos, NPC.frame, NPC.GetAlpha(drawColor), NPC.rotation, new Vector2(72, 88), NPC.scale, flip, 0);
        return false;
    }
    private static void DrawLine(SpriteBatch batch, Vector2 screenPos, Vector2 origin, Vector2 direction, float length, Color color, float thickness, bool dashed)
    {
        if (!dashed)
        {
            batch.Draw(TextureAssets.MagicPixel.Value, origin - screenPos, new Rectangle(0, 0, 1, 1), color,
                direction.ToRotation(), new Vector2(0, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0);
            return;
        }
        for (float offset = 0; offset < length; offset += 28)
            batch.Draw(TextureAssets.MagicPixel.Value, origin + direction * offset - screenPos, new Rectangle(0, 0, 1, 1), color,
                direction.ToRotation(), new Vector2(0, 0.5f), new Vector2(Math.Min(16, length - offset), thickness), SpriteEffects.None, 0);
    }
    private static void DrawReticle(SpriteBatch batch, Vector2 screenPos, Vector2 center, float radius, Color color, bool dashed)
    {
        const int segments = 32;
        for (int i = 0; i < segments; i++)
        {
            if (dashed && i % 2 != 0) continue;
            float angle = MathHelper.TwoPi * i / segments;
            Vector2 start = center + angle.ToRotationVector2() * radius;
            Vector2 end = center + (angle + MathHelper.TwoPi / segments).ToRotationVector2() * radius;
            DrawLine(batch, screenPos, start, (end - start).SafeNormalize(Vector2.UnitX), Vector2.Distance(start, end), color, 2, false);
        }
        for (int i = 0; i < 4; i++)
        {
            Vector2 axis = (MathHelper.PiOver2 * i).ToRotationVector2();
            DrawLine(batch, screenPos, center + axis * (radius + 4), axis, 8, color, 2, false);
        }
        DrawLine(batch, screenPos, center - Vector2.UnitX * 6, Vector2.UnitX, 12, color, 1, false);
        DrawLine(batch, screenPos, center - Vector2.UnitY * 6, Vector2.UnitY, 12, color, 1, false);
    }
}
