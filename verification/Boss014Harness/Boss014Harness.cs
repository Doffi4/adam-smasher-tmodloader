using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AdamSmasherMod.Content.NPCs;
using AdamSmasherMod.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Boss014HarnessMod;
public class Boss014Harness : Mod { }

// Real NPC AI and Projectile.Update run in the isolated tML server. Geometry results
// count eligible intersections, not full-set player DPS or a claim of in-game survival.
public class Boss014Checks : ModSystem
{
    private readonly List<string> output = new();
    private int failures;
    private Player player;
    private int BlastType => ModLoader.GetMod("AdamSmasher").TryFind<ModProjectile>("BossBlast", out var blast) ? blast.Type : -1;
    private bool IsAttack(Projectile p) => p.active && (p.type == ModContent.ProjectileType<BossBullet>() || p.type == ModContent.ProjectileType<BossRocket>() || p.type == BlastType);
    public override void PostWorldLoad()
    {
        string root = Environment.GetEnvironmentVariable("ADAM_SMASHER_LAB_ROOT");
        if (!Main.dedServ || string.IsNullOrEmpty(root)) return;
        player = Main.player[0] = new Player { whoAmI = 0, active = true, statLife = 1_000_000, statLifeMax2 = 1_000_000 };
        Run("Off-screen boss remains eligible to draw target warnings", () =>
        {
            NPC boss = NewBoss(); boss.Center = player.Center + new Vector2(1800, 0);
            Rectangle screen = new((int)player.Center.X - 400, (int)player.Center.Y - 225, 800, 450);
            Check(!screen.Intersects(boss.Hitbox), "Fixture boss is outside player screen");
            Check(NPCID.Sets.MustAlwaysDraw[boss.type], "Engine DrawNPCs must reach PreDraw for off-screen warnings");
        });
        Run("Off-screen delivery rocket remains eligible to draw visible strike marker", () =>
        {
            int type = ModContent.ProjectileType<BossRocket>();
            Rectangle screen = new((int)player.Center.X - 400, (int)player.Center.Y - 225, 800, 450);
            Rectangle body = new((int)player.Center.X + 1800, (int)player.Center.Y, 26, 12);
            Rectangle oldGate = screen; oldGate.Inflate(480, 480);
            Check(!oldGate.Intersects(body), "Default draw margin hides this delivery rocket");
            int margin = ProjectileID.Sets.DrawScreenCheckFluff[type];
            Check(margin >= AdamSmasherBoss.EngagementRange + 330 && margin <= 4000, "Bounded draw margin covers farthest strike marker");
            screen.Inflate(margin, margin);
            Check(screen.Intersects(body), "Engine DrawProj can reach marker PreDraw");
        });
        Run("Normal defaults and phase transition preserve injured HP/defense", DefaultsAndPhase);
        foreach (bool overloaded in new[] { false, true })
        {
            string phase = overloaded ? "phase2" : "phase1";
            Run(phase + " reposition36/24 gives every attack warning", () =>
            {
                NPC boss = NewBoss(overloaded); int ticks = 0;
                while (Model(boss).State == AdamSmasherBoss.AttackState.Reposition && ticks < 100) { Step(boss); ticks++; }
                Check(ticks == (overloaded ? 25 : 37) && Model(boss).State == AdamSmasherBoss.AttackState.MissileWarning, "Reposition timer36/24 enters missile warning");
            });
            foreach (var state in new[] { AdamSmasherBoss.AttackState.GunWarning, AdamSmasherBoss.AttackState.MissileWarning, AdamSmasherBoss.AttackState.DashWarning })
                Run(phase + " " + state + " tracks early and locks final24 ticks", () => WarningLock(overloaded, state));
            Run(phase + " each gun burst has fresh warning and three 12tick volleys", () => GunBursts(overloaded));
            Run(phase + " fixed missile strike geometry and direct follow-up", () => MissilePoints(overloaded));
            Run(phase + " dash reaches through target with synchronized duration", () => DashThrough(overloaded));
            foreach (var state in new[] { AdamSmasherBoss.AttackState.GunWarning, AdamSmasherBoss.AttackState.MissileWarning, AdamSmasherBoss.AttackState.DashWarning })
                Run(phase + " " + state + " stationary pressure and 7px/t escape", () => PressureAndEscape(overloaded, state));
        }
        Run("Real rocket Update creates damaging blast on impact only", BlastImpact);
        Run("Blast circle radius110 and exactly8 damaging updates", BlastWindow);
        Run("Client cannot aim, change phase, spawn or start dash", ClientAuthority);
        Run("Shared cap20 includes blast; phase/death/retire cleanup creates no blasts", CapAndCleanup);
        output.Add("SCOPE: Eligible collision intersections use real Projectile.Update/Colliding and NPC contact gates. No invulnerability fixture; no full-set HP/DPS assertion.");
        File.WriteAllLines(Path.Combine(root, "verification", "boss014-runtime.txt"), output);
        Environment.Exit(failures == 0 ? 0 : 2);
    }
    private void Run(string name, Action test)
    {
        int mode = Main.netMode, gameMode = Main.GameMode;
        try { Main.netMode = NetmodeID.Server; Main.GameMode = 0; Reset(); test(); output.Add("PASS: " + name); }
        catch (Exception e) { failures++; output.Add("FAIL: " + name + ": " + e); }
        finally { Main.netMode = NetmodeID.Server; Reset(); Main.netMode = mode; Main.GameMode = gameMode; }
    }
    private void Reset()
    {
        // Remove owners first: cleanup Kill cannot be mistaken for a real impact.
        foreach (NPC n in Main.npc.Where(n => n.active && n.ModNPC is AdamSmasherBoss)) n.active = false;
        foreach (Projectile p in Main.projectile.Where(IsAttack)) p.Kill();
        player.active = true; player.dead = false; player.immune = false; player.immuneTime = 0;
        player.statLife = player.statLifeMax2 = 1_000_000;
        player.Center = new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16 - 800);
    }
    private NPC NewBoss(bool overloaded = false)
    {
        int index = NPC.NewNPC(new EntitySource_Misc("Boss014"), (int)player.Center.X + 400, (int)player.Center.Y - 260, ModContent.NPCType<AdamSmasherBoss>());
        NPC boss = Main.npc[index]; boss.target = 0;
        if (overloaded) { boss.ai[2] = 1; boss.life = boss.lifeMax / 2 - 123; }
        return boss;
    }
    private static AdamSmasherBoss Model(NPC boss) => (AdamSmasherBoss)boss.ModNPC;
    private static T Property<T>(NPC boss, string name)
    {
        var property = boss.ModNPC.GetType().GetProperty(name);
        Check(property != null, "Missing new combat contract: " + name);
        return (T)property.GetValue(boss.ModNPC);
    }
    private void Step(NPC boss, Action<Projectile> onSpawn = null, Action<Projectile> afterUpdate = null)
    {
        boss.ModNPC.AI(); boss.position += boss.velocity;
        Check(float.IsFinite(boss.velocity.X) && float.IsFinite(boss.velocity.Y), "Finite NPC velocity");
        Check(Main.projectile.Count(IsAttack) <= 20, "Shared projectile cap20 includes blast");
        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile p = Main.projectile[i];
            if (!IsAttack(p)) continue;
            if ((p.type == ModContent.ProjectileType<BossBullet>() && p.timeLeft == 100) || (p.type == ModContent.ProjectileType<BossRocket>() && p.timeLeft == 150)) onSpawn?.Invoke(p);
            p.Update(i);
            afterUpdate?.Invoke(p);
            Check(!p.active || float.IsFinite(p.velocity.X) && float.IsFinite(p.velocity.Y), "Finite projectile velocity");
        }
    }
    private void Reach(NPC boss, AdamSmasherBoss.AttackState state)
    {
        for (int tick = 0; tick < 2400 && Model(boss).State != state; tick++) Step(boss);
        Check(Model(boss).State == state, "Natural attack cycle reaches " + state);
    }
    private static int WarningDuration(bool phase2, AdamSmasherBoss.AttackState state) => state == AdamSmasherBoss.AttackState.MissileWarning ? (phase2 ? 42 : 48) : (phase2 ? 36 : 42);
    private void Lock(NPC boss, AdamSmasherBoss.AttackState state, bool phase2)
    {
        Reach(boss, state);
        int boundary = WarningDuration(phase2, state) - 24;
        while (Model(boss).State == state && boss.ai[1] <= boundary) Step(boss);
        Check(Model(boss).State == state && Property<bool>(boss, "AimLocked"), "Warning enters final locked interval before attack");
    }
    private void DefaultsAndPhase()
    {
        NPC boss = NewBoss();
        Check(boss.lifeMax == 18_000_000 && boss.defense == 110 && boss.defDamage == 600, "Normal HARD boss starts at18M HP,110 defense,600 contact");
        int injury = boss.lifeMax / 2 - 777; boss.life = injury; int defense = boss.defense;
        Step(boss);
        Check(Model(boss).State == AdamSmasherBoss.AttackState.Overload && Model(boss).Overloaded, "Half HP triggers overload");
        for (int tick = 0; tick < 88; tick++) { Step(boss); Check(!Main.projectile.Any(IsAttack) && boss.damage == 0, "Quiet phase transition"); }
        for (int tick = 0; tick < 600; tick++) Step(boss);
        Check(boss.life == injury && boss.defense == defense, "AI cannot heal or adapt defense");
    }
    private void WarningLock(bool phase2, AdamSmasherBoss.AttackState state)
    {
        NPC boss = NewBoss(phase2); Reach(boss, state);
        Vector2 first = Property<Vector2>(boss, "LockedAim");
        int boundary = WarningDuration(phase2, state) - 24;
        while (Model(boss).State == state && boss.ai[1] <= boundary)
        {
            player.Center += new Vector2(0, 3); Step(boss);
            Check(boss.damage == 0, "Tracking warning cannot cause contact damage");
        }
        Check(Model(boss).State == state, "Warning retains a24tick escape interval");
        Vector2 locked = Property<Vector2>(boss, "LockedAim"), direction = Model(boss).LockedDirection;
        Check(Vector2.Distance(first, locked) > 20 && Vector2.Distance(locked, player.Center) < 0.01f, "Warning updates target during early tracking, including boundary");
        int remaining = 0;
        while (Model(boss).State == state && remaining < 30)
        {
            player.Center += new Vector2(7, 0); Step(boss); remaining++;
            Check(Property<Vector2>(boss, "LockedAim") == locked && Model(boss).LockedDirection == direction, "Locked aim and direction cannot follow escape movement");
        }
        Check(remaining == 24, $"Exactly24 locked updates before attack, observed {remaining}");
    }
    private void GunBursts(bool phase2)
    {
        NPC boss = NewBoss(phase2); Reach(boss, AdamSmasherBoss.AttackState.GunWarning);
        int warnings = 1, bullets = 0, frame = 0;
        var volleys = new List<int>(); var sizes = new List<int>();
        AdamSmasherBoss.AttackState previous = Model(boss).State;
        for (; frame < 600; frame++)
        {
            int spawned = 0;
            Step(boss, p => { if (p.type == ModContent.ProjectileType<BossBullet>()) { spawned++; bullets++; Check(p.damage == 350 && Math.Abs(p.velocity.Length() - 24) < 0.01f, "Gun bullet damage350 and speed24"); } });
            if (spawned > 0) { volleys.Add(frame); sizes.Add(spawned); }
            var current = Model(boss).State;
            if (current == AdamSmasherBoss.AttackState.GunWarning && previous != current) warnings++;
            if (current is AdamSmasherBoss.AttackState.Reposition or AdamSmasherBoss.AttackState.DashWarning) break;
            previous = current;
        }
        int bursts = phase2 ? 3 : 2;
        Check(frame < 600 && warnings == bursts && bullets == bursts * 9, $"{bursts} individually warned bursts of9 bullets; warnings={warnings}, bullets={bullets}");
        Check(sizes.All(n => n == 3) && volleys.Count == bursts * 3, "Each volley has three lanes");
        for (int burst = 0; burst < bursts; burst++)
            Check(volleys[burst * 3 + 1] - volleys[burst * 3] == 12 && volleys[burst * 3 + 2] - volleys[burst * 3 + 1] == 12, "Volley cadence12 ticks");
    }
    private void MissilePoints(bool phase2)
    {
        NPC boss = NewBoss(phase2); Lock(boss, AdamSmasherBoss.AttackState.MissileWarning, phase2);
        Vector2 locked = Property<Vector2>(boss, "LockedAim");
        var points = new List<Vector2>(); var frames = new List<int>();
        bool sawSalvo = false; int tick;
        for (tick = 0; tick < 180; tick++)
        {
            int frame = tick;
            Step(boss, p => { if (p.type == ModContent.ProjectileType<BossRocket>()) { points.Add(new Vector2(p.ai[1], p.ai[2])); frames.Add(frame); Check(p.damage == 450 && Math.Abs(p.velocity.Length() - 20) < 0.01f, "Rocket damage450 and speed20"); } });
            if (Model(boss).State == AdamSmasherBoss.AttackState.Salvo) sawSalvo = true;
            if (sawSalvo && Model(boss).State != AdamSmasherBoss.AttackState.Salvo) break;
        }
        var expected = new List<Vector2> { locked, locked + new Vector2(-180, 0), locked + new Vector2(180, 0) };
        if (phase2) { expected.Add(locked + new Vector2(0, -180)); expected.Add(locked + new Vector2(0, 180)); }
        Check(points.Count == expected.Count && expected.All(point => points.Any(actual => Vector2.Distance(actual, point) < 0.01f)), "All3/5 explicitly telegraphed strike points are launched");
        for (int i = 1; i < frames.Count; i++) Check(frames[i] - frames[i - 1] == 8, "Missile launches spaced8 ticks");
        Check(Model(boss).State == (phase2 ? AdamSmasherBoss.AttackState.GunWarning : AdamSmasherBoss.AttackState.Reposition), "Phase2 salvo goes directly into a fresh gun warning");
    }
    private void DashThrough(bool phase2)
    {
        NPC boss = NewBoss(phase2); Lock(boss, AdamSmasherBoss.AttackState.DashWarning, phase2);
        Vector2 target = Property<Vector2>(boss, "LockedAim");
        Reach(boss, AdamSmasherBoss.AttackState.Dash);
        Vector2 start = boss.Center - boss.velocity;
        float speed = phase2 ? 50 : 44;
        int expected = (int)Math.Ceiling(Math.Clamp(Vector2.Distance(start, target) + 180, 600, 1000) / speed);
        Check(Math.Abs(boss.velocity.Length() - speed) < 0.01f && Property<int>(boss, "DashDuration") == expected, "Distance-derived dash duration and speed44/50");
        Vector2 direction = Model(boss).LockedDirection;
        bool contact = false;
        while (Model(boss).State == AdamSmasherBoss.AttackState.Dash)
        {
            int slot = 0;
            if (boss.ModNPC.CanHitPlayer(player, ref slot) && boss.Hitbox.Intersects(player.Hitbox)) contact = true;
            Step(boss);
        }
        Check(contact && Vector2.Dot(boss.Center - target, direction) >= 100, "Dash crosses stationary target and reaches beyond it");
        int recovery = 0;
        while (Model(boss).State == AdamSmasherBoss.AttackState.Recovery && recovery < 30) { Step(boss); recovery++; }
        Check(recovery == (phase2 ? 17 : 21), "Dash recovery20/16 timer followed by fresh warning");
        NPC replica = NewBoss(phase2);
        CopyBoss(boss, replica);
        Check(Property<int>(replica, "DashDuration") == Property<int>(boss, "DashDuration") && Property<Vector2>(replica, "LockedAim") == Property<Vector2>(boss, "LockedAim"), "Dash duration and target survive ExtraAI round-trip");
    }
    private int Geometry(bool phase2, AdamSmasherBoss.AttackState warning, bool escape)
    {
        NPC boss = NewBoss(phase2); Lock(boss, warning, phase2);
        int intersections = 0; bool entered = false;
        var active = warning == AdamSmasherBoss.AttackState.GunWarning ? AdamSmasherBoss.AttackState.Barrage : warning == AdamSmasherBoss.AttackState.MissileWarning ? AdamSmasherBoss.AttackState.Salvo : AdamSmasherBoss.AttackState.Dash;
        for (int tick = 0; tick < 150; tick++)
        {
            // Perpendicular flight away from the final locked warning, speed7 pixels/tick.
            if (escape) player.Center += new Vector2(0, 7);
            Step(boss, afterUpdate: p =>
            {
                // Rockets are delivery vehicles; critical missile damage is counted at blast.
                if (p.type == ModContent.ProjectileType<BossRocket>()) return;
                bool damaging = p.damage > 0 && p.ModProjectile.CanDamage() != false && p.ModProjectile.CanHitPlayer(player);
                bool intersects = p.ModProjectile.Colliding(p.Hitbox, player.Hitbox) ?? p.Hitbox.Intersects(player.Hitbox);
                if (damaging && intersects) intersections++;
            });
            int slot = 0;
            if (boss.damage > 0 && boss.ModNPC.CanHitPlayer(player, ref slot) && boss.Hitbox.Intersects(player.Hitbox)) intersections++;
            if (Model(boss).State == active) entered = true;
            // Isolate the first warned attack; freeze state once its delivery finished so
            // a subsequent warning doesn't track the escape fixture a second time.
            if (entered && Model(boss).State != active) { boss.ai[0] = (int)AdamSmasherBoss.AttackState.Overload; boss.ai[1] = 0; }
        }
        boss.active = false;
        foreach (Projectile p in Main.projectile.Where(IsAttack)) p.Kill();
        return intersections;
    }
    private void PressureAndEscape(bool phase2, AdamSmasherBoss.AttackState warning)
    {
        int stationary = Geometry(phase2, warning, false);
        Reset();
        int escape = Geometry(phase2, warning, true);
        output.Add($"DATA: {(phase2 ? "phase2" : "phase1")} {warning} eligible intersections stationary={stationary},7px/t escape={escape}");
        Check(stationary > 0, "Stationary target encounters real collision geometry");
        Check(escape == 0, "Moving after lock can avoid critical damage geometry");
    }
    private void BlastImpact()
    {
        Check(BlastType >= 0, "BossBlast exists for actual impact damage");
        NPC boss = NewBoss(); Vector2 strike = boss.Center + new Vector2(1000, 0);
        int index = Projectile.NewProjectile(new EntitySource_Misc("Boss014Impact"), strike - new Vector2(50, 0), Vector2.UnitX * 20, ModContent.ProjectileType<BossRocket>(), 450, 0, Main.myPlayer, boss.whoAmI, strike.X, strike.Y);
        Projectile rocket = Main.projectile[index];
        int rocketType = ModContent.ProjectileType<BossRocket>();
        // NewProjectile may reuse the killed rocket's slot and its mutable object.
        // Observe the pool's rocket/blast identities, not a stale reference's active flag.
        for (int tick = 0; tick < 5 && rocket.active && rocket.type == rocketType; tick++) rocket.Update(index);
        Projectile blast = Main.projectile.FirstOrDefault(p => p.active && p.type == BlastType && p.ai[0] == boss.whoAmI);
        Check(!Main.projectile.Any(p => p.active && p.type == rocketType && p.ai[0] == boss.whoAmI) && blast != null && Vector2.Distance(blast.Center, strike) < 0.01f && blast.damage == 450, "Real Update removes rocket at exact fixed strike and creates owned damaging blast");
        blast.Kill();
        index = Projectile.NewProjectile(new EntitySource_Misc("Boss014Cleanup"), boss.Center, Vector2.UnitX * 20, ModContent.ProjectileType<BossRocket>(), 450, 0, Main.myPlayer, boss.whoAmI, strike.X, strike.Y);
        Main.projectile[index].Kill();
        Check(!Main.projectile.Any(p => p.active && p.type == BlastType), "Cleanup Kill does not manufacture an impact explosion");
    }
    private void BlastWindow()
    {
        Check(BlastType >= 0, "BossBlast exists"); NPC boss = NewBoss();
        Vector2 center = boss.Center + new Vector2(1000, 0);
        int index = Projectile.NewProjectile(new EntitySource_Misc("Boss014Blast"), center, Vector2.Zero, BlastType, 450, 0, Main.myPlayer, boss.whoAmI, 110);
        Projectile blast = Main.projectile[index];
        Check(blast.ModProjectile.Colliding(blast.Hitbox, new Rectangle((int)center.X + 109, (int)center.Y, 1, 1)) == true, "Circle reaches109 pixels");
        Check(blast.ModProjectile.Colliding(blast.Hitbox, new Rectangle((int)center.X + 111, (int)center.Y, 1, 1)) == false, "Circle excludes111 pixels");
        Check(blast.ModProjectile.Colliding(blast.Hitbox, new Rectangle((int)center.X + 85, (int)center.Y + 85, 1, 1)) == false, "Blast uses circle rather than bounding square");
        int damaging = 0, total = 0;
        while (blast.active && total < 40) { if (blast.ModProjectile.CanDamage() != false && blast.ModProjectile.CanHitPlayer(player)) damaging++; blast.Update(index); total++; }
        Check(damaging == 8 && total == 24, $"Blast damages8 ticks then visual-only to24; damaging={damaging},total={total}");
    }
    private static void CopyBoss(NPC source, NPC target)
    {
        Array.Copy(source.ai, target.ai, source.ai.Length); target.position = source.position; target.velocity = source.velocity; target.target = source.target;
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) source.ModNPC.SendExtraAI(writer);
        stream.Position = 0;
        using var reader = new BinaryReader(stream); target.ModNPC.ReceiveExtraAI(reader);
    }
    private void ClientAuthority()
    {
        NPC boss = NewBoss(); Lock(boss, AdamSmasherBoss.AttackState.GunWarning, false);
        NPC replica = NewBoss(); CopyBoss(boss, replica);
        Vector2 aim = Property<Vector2>(replica, "LockedAim"), direction = Model(replica).LockedDirection;
        int before = Main.projectile.Count(IsAttack); replica.life = 1;
        Main.netMode = NetmodeID.MultiplayerClient;
        for (int tick = 0; tick < 100; tick++) { player.Center += new Vector2(2, 3); replica.ModNPC.AI(); }
        Check(!Model(replica).Overloaded && Property<Vector2>(replica, "LockedAim") == aim && Model(replica).LockedDirection == direction, "Client cannot own aim or phase transition");
        foreach (var state in new[] { AdamSmasherBoss.AttackState.Salvo, AdamSmasherBoss.AttackState.Barrage, AdamSmasherBoss.AttackState.DashWarning })
        {
            replica.ai[0] = (int)state; replica.ai[1] = 100; replica.velocity = Vector2.Zero;
            for (int tick = 0; tick < 60; tick++) replica.ModNPC.AI();
            Check(Main.projectile.Count(IsAttack) == before && Model(replica).State == state, "Client cannot advance attack state or spawn attacks");
            if (state == AdamSmasherBoss.AttackState.DashWarning) Check(replica.velocity == Vector2.Zero, "Client waits for authoritative dash start");
        }
        Main.netMode = NetmodeID.Server;
    }
    private void CapAndCleanup()
    {
        Check(BlastType >= 0, "BossBlast exists");
        foreach (string cleanup in new[] { "phase", "death", "retire" })
        {
            Reset(); NPC boss = NewBoss();
            for (int i = 0; i < 19; i++) Projectile.NewProjectile(new EntitySource_Misc("Boss014Cap"), boss.Center, Vector2.Zero, BlastType, 450, 0, Main.myPlayer, boss.whoAmI, 110);
            Vector2 strike = boss.Center + new Vector2(1000, 0);
            Projectile.NewProjectile(new EntitySource_Misc("Boss014CapRocket"), boss.Center, Vector2.UnitX * 20, ModContent.ProjectileType<BossRocket>(), 450, 0, Main.myPlayer, boss.whoAmI, strike.X, strike.Y);
            boss.ai[0] = (int)AdamSmasherBoss.AttackState.Barrage; boss.ai[1] = 0; boss.ModNPC.AI();
            Check(Main.projectile.Count(IsAttack) == 20 && !Main.projectile.Any(p => p.active && p.type == ModContent.ProjectileType<BossBullet>()), "Visual blasts and rockets consume shared20-slot attack cap");
            if (cleanup == "phase") { boss.life = boss.lifeMax / 2 - 1; boss.ModNPC.AI(); }
            else if (cleanup == "death") boss.StrikeInstantKill();
            else { player.dead = true; boss.ModNPC.AI(); }
            Check(!Main.projectile.Any(IsAttack), cleanup + " removes all bullets/rockets/blasts without new explosions");
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
