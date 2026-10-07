using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using AdamSmasherMod.Common;
using AdamSmasherMod.Content.NPCs;
using AdamSmasherMod.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;

namespace Task2HarnessMod;
public class Task2Harness : Mod { }
public class Task2Checks : ModSystem
{
    private static readonly HashSet<int> SeenStates = new();
    private static int rockets, bullets, maximum, dashes;
    private static bool IsAttack(Projectile p) => p.active && (p.type == ModContent.ProjectileType<BossRocket>() || p.type == ModContent.ProjectileType<BossBullet>() ||
        ModLoader.GetMod("AdamSmasher").TryFind<ModProjectile>("BossBlast", out var blast) && p.type == blast.Type);
    public override void PostWorldLoad()
    {
        string root = Environment.GetEnvironmentVariable("ADAM_SMASHER_LAB_ROOT");
        if (!Main.dedServ || string.IsNullOrEmpty(root)) return;
        if (Environment.GetEnvironmentVariable("ADAM_SMASHER_FIX1_ONLY") == "1") { RunFix1(root); return; }
        string result = Path.Combine(root, "verification", "task2-runtime.txt");
        try
        {
            Player player = Main.player[0] = new Player { whoAmI = 0, active = true, statLife = 500, statLifeMax2 = 500 };
            player.Center = new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16);
            int index = NPC.NewNPC(new EntitySource_Misc("Task2Checks"), (int)player.Center.X + 400, (int)player.Center.Y - 260, ModContent.NPCType<AdamSmasherBoss>());
            NPC boss = Main.npc[index];
            boss.target = 0;
            AdamSmasherBoss modBoss = (AdamSmasherBoss)boss.ModNPC;
            int originalDefense = boss.defense;
            RunCycle(boss, player, 1600);
            Check(rockets >= 4 && bullets >= 15 && dashes >= 2, "Phase1 has salvo, full barrage, short dash series");
            Check(SeenStates.Contains((int)AdamSmasherBoss.AttackState.MissileWarning) && SeenStates.Contains((int)AdamSmasherBoss.AttackState.GunWarning) && SeenStates.Contains((int)AdamSmasherBoss.AttackState.DashWarning), "All attacks have telegraph states");
            string phase1 = $"PASS: phase1 rockets={rockets}, bullets={bullets}, dashes={dashes}, maximum={maximum}\n";
            int phase1Rockets = rockets, phase1Bullets = bullets, phase1Dashes = dashes;
            int damagedLife = boss.lifeMax / 2 - 123;
            boss.life = damagedLife;
            Tick(boss, player);
            Check(modBoss.Overloaded && modBoss.State == AdamSmasherBoss.AttackState.Overload && boss.life == damagedLife, "Phase at50% preserves actual damaged HP");
            Check(!Main.projectile.Any(IsAttack) && boss.damage == 0, "Transition cleans old attacks and causes no contact damage");
            RunCycle(boss, player, 88);
            Check(!Main.projectile.Any(IsAttack), "Quiet transition has no hostile spawns");
            rockets = bullets = maximum = dashes = 0;
            RunCycle(boss, player, 1600);
            Check(rockets >= 8 && bullets >= 18 && dashes >= 3, "Overload has denser mixed barrage and three dashes");
            Check(boss.life == damagedLife && boss.defense == originalDefense, "AI never heals or adapts defense");
            string phase2 = $"PASS: phase2 rockets={rockets}, bullets={bullets}, dashes={dashes}, maximum={maximum}\n";
            Projectile timed = Main.projectile[Projectile.NewProjectile(new EntitySource_Misc("Task2Lifetime"), boss.Center, Vector2.UnitX, ModContent.ProjectileType<BossBullet>(), 100, 0, Main.myPlayer, boss.whoAmI)];
            timed.timeLeft = 1;
            timed.Update(timed.whoAmI);
            Check(!timed.active, "Real engine removes bullet at lifetime end");
            Vector2 strike = boss.Center + Vector2.UnitY * 600;
            Projectile ballistic = Main.projectile[Projectile.NewProjectile(new EntitySource_Misc("Task2FixedStrike"), boss.Center, Vector2.UnitY * 20, ModContent.ProjectileType<BossRocket>(), 100, 0, Main.myPlayer, boss.whoAmI, strike.X, strike.Y)];
            ballistic.ModProjectile.AI();
            Check(Vector2.Distance(ballistic.velocity, Vector2.UnitY * 20) < 0.001f, "Fixed strike rocket maintains its target trajectory");
            ballistic.Center = boss.Center + new Vector2(3000, 0);
            ballistic.ModProjectile.AI();
            Check(!ballistic.active, "Rocket removed before3200 range");
            // Invalid server target is safely reacquired; no invalid Main.player access.
            boss.target = -1;
            Tick(boss, player);
            Check(boss.target == 0, "Invalid target safely reacquired");
            // Capture an actual server warning and its ExtraAI, then exercise client-only branch.
            for (int tick = 0; tick < 1000 && modBoss.State != AdamSmasherBoss.AttackState.GunWarning; tick++) Tick(boss, player);
            Check(modBoss.State == AdamSmasherBoss.AttackState.GunWarning, "Actual gun warning reached");
            Vector2 locked = modBoss.LockedDirection;
            using MemoryStream data = new();
            using (BinaryWriter writer = new(data, System.Text.Encoding.UTF8, true)) modBoss.SendExtraAI(writer);
            int secondIndex = NPC.NewNPC(new EntitySource_Misc("Task2Network"), (int)boss.Center.X, (int)boss.Center.Y, boss.type);
            NPC replica = Main.npc[secondIndex];
            Array.Copy(boss.ai, replica.ai, boss.ai.Length);
            replica.target = 0;
            data.Position = 0;
            using (BinaryReader reader = new(data, System.Text.Encoding.UTF8, true)) replica.ModNPC.ReceiveExtraAI(reader);
            Check(((AdamSmasherBoss)replica.ModNPC).LockedDirection == locked, "Locked aim ExtraAI round-trip");
            int activeBefore = Main.projectile.Count(IsAttack);
            int oldMode = Main.netMode;
            try
            {
                Main.netMode = NetmodeID.MultiplayerClient;
                for (int tick = 0; tick < 200; tick++) { player.position.X += 1; replica.ModNPC.AI(); }
                Check(Main.projectile.Count(IsAttack) == activeBefore, "Client AI cannot create barrage projectiles");
                Check(((AdamSmasherBoss)replica.ModNPC).LockedDirection == locked, "Client target movement cannot recompute locked aim");
                replica.ai[0] = (int)AdamSmasherBoss.AttackState.Barrage;
                replica.ai[1] = 0;
                for (int tick = 0; tick < 80; tick++) replica.ModNPC.AI();
                Check(Main.projectile.Count(IsAttack) == activeBefore, "Client active barrage cannot create attacks");
                replica.ai[0] = (int)AdamSmasherBoss.AttackState.DashWarning;
                replica.ai[1] = 100;
                replica.velocity = Vector2.Zero;
                replica.ModNPC.AI();
                Check(replica.velocity == Vector2.Zero, "Client cannot start a dash before the authoritative state packet");
            }
            finally { Main.netMode = oldMode; replica.active = false; }
            player.dead = true;
            Tick(boss, player);
            Check(modBoss.State == AdamSmasherBoss.AttackState.Retire && !Main.projectile.Any(IsAttack), "All dead stops attacks and removes projectiles immediately");
            RunCycle(boss, player, 61);
            Check(!boss.active, "Retire completes without awarding victory");
            player.dead = false;
            player.Center = new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16);
            index = NPC.NewNPC(new EntitySource_Misc("Task2Resummon"), (int)player.Center.X + 400, (int)player.Center.Y - 260, boss.type);
            boss = Main.npc[index];
            boss.target = 0;
            rockets = bullets = 0;
            RunCycle(boss, player, 150);
            Check(rockets + bullets > 0, "New boss launches actual attacks after retirement");
            boss.StrikeInstantKill();
            Check(!Main.projectile.Any(IsAttack) && SmasherWorld.Downed, "Real death cleans attacks and keeps Downed mark");
            Player secondPlayer = Main.player[1] = new Player { whoAmI = 1, active = true, statLife = 500, statLifeMax2 = 500, immune = true, immuneTime = 10000 };
            secondPlayer.Center = player.Center;
            index = NPC.NewNPC(new EntitySource_Misc("Task2TwoPlayers"), (int)player.Center.X + 400, (int)player.Center.Y - 260, ModContent.NPCType<AdamSmasherBoss>());
            NPC multiplayerBoss = Main.npc[index];
            multiplayerBoss.target = 0;
            rockets = bullets = maximum = dashes = 0;
            RunCycle(multiplayerBoss, player, 1600);
            Check(rockets == phase1Rockets && bullets == phase1Bullets && dashes == phase1Dashes, "Two active server players receive same single attack stream");
            string twoPlayers = $"PASS: two active server players rockets={rockets}, bullets={bullets}, dashes={dashes}; matches one-player stream\n";
            multiplayerBoss.StrikeInstantKill();
            secondPlayer.active = false;
            Projectile orphan = Main.projectile[Projectile.NewProjectile(new EntitySource_Misc("Task2Orphan"), player.Center, Vector2.UnitX, ModContent.ProjectileType<BossRocket>(), 100, 0, Main.myPlayer, Main.maxNPCs)];
            orphan.ModProjectile.AI();
            Check(!orphan.active, "Orphan missile cannot damage players");
            index = NPC.NewNPC(new EntitySource_Misc("Task2Gone"), (int)player.Center.X, (int)player.Center.Y, ModContent.NPCType<AdamSmasherBoss>());
            boss = Main.npc[index];
            boss.target = Main.maxPlayers;
            player.active = false;
            boss.ModNPC.AI();
            Check(((AdamSmasherBoss)boss.ModNPC).State == AdamSmasherBoss.AttackState.Retire, "Inactive/missing targets safely retire");
            boss.active = false;
            player.active = true;
            index = NPC.NewNPC(new EntitySource_Misc("Task2Range"), (int)player.Center.X + 4000, (int)player.Center.Y, ModContent.NPCType<AdamSmasherBoss>());
            boss = Main.npc[index];
            boss.target = 0;
            boss.ModNPC.AI();
            Check(((AdamSmasherBoss)boss.ModNPC).State == AdamSmasherBoss.AttackState.Retire, "Out-of-range target safely retires");
            boss.active = false;
            player.active = false;
            File.WriteAllText(result, phase1 + phase2 + twoPlayers + "PASS: quiet phase transition preserves HP/defense; telegraph/contact gates; real projectile AI/lifetime/fixed strike/range; invalid target reacquisition; client no-spawn, server-only dash start and locked aim ExtraAI; death/retire cleanup; resummon and orphan safety\n");
            Environment.Exit(0);
        }
        catch (Exception e) { File.WriteAllText(result, "FAIL: " + e + "\n"); Mod.Logger.Error("TASK2 FAIL", e); Environment.Exit(2); }
    }
    private void RunFix1(string root)
    {
        var output = new List<string>();
        int failures = 0;
        Player player = Main.player[0] = new Player { whoAmI = 0, active = true, statLife = 500, statLifeMax2 = 500 };
        player.Center = new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16);
        void Run(string name, Action test)
        {
            try { test(); output.Add("PASS: " + name); }
            catch (Exception e) { failures++; output.Add("FAIL: " + name + ": " + e); }
            finally
            {
                foreach (Projectile p in Main.projectile.Where(IsAttack)) p.Kill();
                foreach (NPC npc in Main.npc.Where(n => n.active && n.ModNPC is AdamSmasherBoss)) npc.active = false;
                player.active = true; player.dead = false;
            }
        }
        foreach (int type in new[] { ModContent.ProjectileType<BossBullet>(), ModContent.ProjectileType<BossRocket>() })
            Run("I1 " + (type == ModContent.ProjectileType<BossBullet>() ? "bullet" : "rocket") + " Normal/Expert/Master CombinedHooks", () =>
            {
                int originalMode = Main.GameMode;
                var damage = new List<float>();
                try
                {
                    for (int mode = 0; mode <= 2; mode++)
                    {
                        Main.GameMode = mode;
                        NPC boss = NewCheckBoss(player);
                        boss.ai[0] = (int)(type == ModContent.ProjectileType<BossBullet>() ? AdamSmasherBoss.AttackState.Barrage : AdamSmasherBoss.AttackState.Salvo);
                        boss.ai[1] = 0;
                        boss.ModNPC.AI();
                        Projectile shot = Main.projectile.First(p => p.active && p.type == type && p.ai[0] == boss.whoAmI);
                        Player.HurtModifiers modifiers = new();
                        CombinedHooks.ModifyHitByProjectile(player, shot, ref modifiers);
                        damage.Add(modifiers.SourceDamage.ApplyTo(shot.damage * 2));
                        output.Add($"DATA: type={type}, mode={mode}, defDamage={boss.defDamage}, raw={shot.damage}, sourceBeforeDefense={damage[^1]}");
                        boss.StrikeInstantKill();
                    }
                    Check(Math.Abs(damage[1] / damage[0] - 2) < 0.001f && Math.Abs(damage[2] / damage[0] - 3) < 0.001f, "Difficulty SourceDamage must scale1/2/3 once");
                }
                finally { Main.GameMode = originalMode; }
            });
        foreach (bool targetDies in new[] { false, true })
            Run("I2 fixed strike server/client trajectory " + (targetDies ? "target dies" : "target moves"), () =>
            {
                NPC boss = NewCheckBoss(player);
                Vector2 strike = boss.Center + new Vector2(1000, 0);
                Projectile server = Main.projectile[Projectile.NewProjectile(new EntitySource_Misc("Fix1ServerRocket"), boss.Center, Vector2.UnitX * 20, ModContent.ProjectileType<BossRocket>(), 450, 0, Main.myPlayer, boss.whoAmI, strike.X, strike.Y)];
                Projectile client = new(); client.SetDefaults(server.type);
                CopyProjectile(server, client);
                int originalMode = Main.netMode;
                try
                {
                    for (int tick = 0; tick < 30; tick++)
                    {
                        player.dead = targetDies && tick >= 12;
                        player.Center = boss.Center + new Vector2(500 + tick * 2, 300 + tick * 9);
                        Main.netMode = NetmodeID.Server;
                        server.netUpdate = false;
                        server.ModProjectile.AI();
                        server.position += server.velocity;
                        Main.netMode = NetmodeID.MultiplayerClient;
                        client.ModProjectile.AI();
                        client.position += client.velocity;
                        Check(server.ai[1] == strike.X && server.ai[2] == strike.Y, "Player movement/loss cannot rewrite a fixed strike point");
                        Check(Vector2.Distance(server.velocity, Vector2.UnitX * 20) < 0.001f, "Server rocket cannot home onto a moved player");
                        Check(Vector2.Distance(server.velocity, client.velocity) < 0.001f, $"Fixed rocket velocity synchronized at tick {tick}");
                        Check(Vector2.Distance(server.position, client.position) < 0.001f, "Server/client fixed trajectories stay aligned without follow-up packets");
                    }
                }
                finally { Main.netMode = originalMode; player.dead = false; }
            });
        Run("Minor client dash damping once until authoritative Recovery", () =>
        {
            NPC boss = NewCheckBoss(player);
            for (int tick = 0; tick < 1200 && ((AdamSmasherBoss)boss.ModNPC).State != AdamSmasherBoss.AttackState.Dash; tick++)
            {
                boss.ModNPC.AI(); boss.position += boss.velocity;
            }
            Check(((AdamSmasherBoss)boss.ModNPC).State == AdamSmasherBoss.AttackState.Dash, "Natural dash reached for authoritative duration fixture");
            var durationProperty = boss.ModNPC.GetType().GetProperty("DashDuration");
            Check(durationProperty != null, "Dash exposes authoritative duration for client expiry");
            int duration = (int)durationProperty.GetValue(boss.ModNPC);
            boss.ai[0] = (int)AdamSmasherBoss.AttackState.Dash;
            boss.ai[1] = duration;
            boss.velocity = Vector2.UnitX * 44;
            int originalMode = Main.netMode;
            try
            {
                Main.netMode = NetmodeID.MultiplayerClient;
                boss.ModNPC.AI();
                Vector2 afterFirst = boss.velocity;
                boss.ModNPC.AI();
                Check(boss.velocity == afterFirst, "Client may damp once, not every delayed dash tick");
                Check(boss.damage == 0, "Expired client dash remains non-damaging");
            }
            finally { Main.netMode = originalMode; }
        });
        File.WriteAllLines(Path.Combine(root, "verification", "task2-fix1-runtime.txt"), output);
        Environment.Exit(failures == 0 ? 0 : 2);
    }
    private static NPC NewCheckBoss(Player player)
    {
        player.Center = new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16);
        int index = NPC.NewNPC(new EntitySource_Misc("Task2Fix1"), (int)player.Center.X + 400, (int)player.Center.Y - 260, ModContent.NPCType<AdamSmasherBoss>());
        NPC boss = Main.npc[index]; boss.target = 0; return boss;
    }
    private static void CopyProjectile(Projectile server, Projectile client)
    {
        client.active = server.active;
        client.position = server.position;
        client.velocity = server.velocity;
        Array.Copy(server.ai, client.ai, server.ai.Length);
    }
    private static void RunCycle(NPC boss, Player player, int ticks) { for (int tick = 0; tick < ticks; tick++) Tick(boss, player); }
    private static void Tick(NPC boss, Player player)
    {
        var previous = ((AdamSmasherBoss)boss.ModNPC).State;
        boss.ModNPC.AI();
        var state = ((AdamSmasherBoss)boss.ModNPC).State;
        SeenStates.Add((int)state);
        if (state == AdamSmasherBoss.AttackState.Dash && previous != state) dashes++;
        int slot = 0;
        bool contact = boss.ModNPC.CanHitPlayer(player, ref slot);
        if (state != AdamSmasherBoss.AttackState.Dash) Check(!contact && boss.damage == 0, "Contact damage only during active dash");
        boss.position += boss.velocity;
        boss.ModNPC.FindFrame(176);
        Check(boss.frame.Y is 0 or 176 or 352 or 528, "Animation frame valid for prepared four-frame sheet");
        Check(float.IsFinite(boss.velocity.X) && float.IsFinite(boss.velocity.Y), "Boss finite movement");
        int count = Main.projectile.Count(IsAttack);
        maximum = Math.Max(maximum, count);
        Check(count <= AdamSmasherBoss.ProjectileLimit, "Hostile projectile cap");
        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile p = Main.projectile[i];
            if (!IsAttack(p)) continue;
            // Hostile server projectiles can reuse slot identity. Initial lifetime identifies each real spawn before Update consumes a tick.
            if (p.type == ModContent.ProjectileType<BossRocket>() && p.timeLeft == 150) rockets++;
            if (p.type == ModContent.ProjectileType<BossBullet>() && p.timeLeft == 100) bullets++;
            player.immune = true;
            player.immuneTime = 1000;
            p.Update(i);
            Check(!p.active || (p.timeLeft <= 150 && float.IsFinite(p.velocity.X) && float.IsFinite(p.velocity.Y)), "Projectile finite lifetime and velocity");
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
