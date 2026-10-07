using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;

namespace Task4HarnessMod;
public class Task4Harness : Mod { }
public class Task4Checks : ModSystem
{
    Player player;
    Mod content;
    readonly List<string> output = new();
    int failures;
    public override void PostWorldLoad()
    {
        string root = Environment.GetEnvironmentVariable("ADAM_SMASHER_LAB_ROOT");
        if (!Main.dedServ || string.IsNullOrEmpty(root)) return;
        content = ModLoader.GetMod("AdamSmasher");
        int oldMode = Main.netMode, oldPlayer = Main.myPlayer;
        Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
        if (Environment.GetEnvironmentVariable("ADAM_SMASHER_TASK4_FIX1_ONLY") == "1")
        {
            RunFix1Checks();
            Main.netMode = oldMode; Main.myPlayer = oldPlayer;
            File.WriteAllLines(Path.Combine(root, "verification", "task4-fix1-runtime.txt"), output);
            Environment.Exit(failures == 0 ? 0 : 2); return;
        }
        RunRampChecks();
        if (Environment.GetEnvironmentVariable("ADAM_SMASHER_TASK4_RAMP_ONLY") == "1")
        {
            Main.netMode = oldMode; Main.myPlayer = oldPlayer;
            File.WriteAllLines(Path.Combine(root, "verification", "task4-ramp-runtime.txt"), output);
            Environment.Exit(failures == 0 ? 0 : 2); return;
        }
        Run("Genuine melee defaults and no contact damage", () => {
            Item item = Weapon();
            Check(item.DamageType == DamageClass.Melee && item.damage >= 2400 && item.noMelee && item.noUseGraphic && item.autoReuse && item.mana == 0, "Melee stats and projectile-only strikes");
        });
        Run("Alternating combo crosses on third, loops, reset by idle", () => {
            Item item = Weapon();
            foreach (int mode in new[] { 0, 1, 2, 0 }) {
                Fire(item); Projectile p = Shots("MantisSlash").Single();
                Check(p.ai[0] == mode && p.damage == (mode == 2 ? item.damage * 3 / 2 : item.damage), "Correct combo and third-hit damage"); p.Kill();
            }
            for (int i = 0; i < 60; i++) Helper().PostUpdate();
            Fire(item); Check(Shots("MantisSlash").Single().ai[0] == 0, "Idle resets combo");
        });
        Run("Combo resets on copy switch and death", () => {
            Item item = Weapon(); Fire(item); Shots("MantisSlash").Single().Kill();
            player.inventory[0] = item = new Item(item.type); Helper().PostUpdate(); Fire(item);
            Check(Shots("MantisSlash").Single().ai[0] == 0, "Copy switch resets sequence"); Shots("MantisSlash").Single().Kill();
            Helper().UpdateDead(); Fire(item); Check(Shots("MantisSlash").Single().ai[0] == 0, "Death resets sequence");
        });
        Run("One owner slash; remote spawn denied", () => {
            Item item = Weapon(); Fire(item); Fire(item);
            Check(Shots("MantisSlash").Length == 1 && !item.ModItem.CanUseItem(player), "Duplicate slash blocked");
            Shots("MantisSlash").Single().Kill(); Main.myPlayer = 1; Fire(item); Main.myPlayer = 0;
            Check(Shots("MantisSlash").Length == 0, "Remote cannot create slash");
        });
        Run("Actual attack-speed animation controls slash duration", () => {
            Item item = Weapon(); player.ApplyItemAnimation(item); Fire(item); Projectile normal = Shots("MantisSlash").Single();
            float duration = normal.ai[2]; normal.Kill();
            player.GetAttackSpeed(DamageClass.Melee) = 2; player.ApplyItemAnimation(item); Fire(item); Projectile fast = Shots("MantisSlash").Single();
            Check(duration >= 10 && fast.ai[2] < duration && fast.ai[2] == player.itemAnimationMax, "Engine animation and accelerated slash match");
            for (int i = 0; i < 20 && fast.active; i++) fast.ModProjectile.AI();
            Check(!fast.active, "Accelerated slash terminates");
        });
        Run("Arc collision rejects invisible square and rear", () => {
            Item item = Weapon(); Fire(item); Projectile p = Shots("MantisSlash").Single();
            for (int i = 0; i < 6; i++) p.ModProjectile.AI();
            Rectangle Hit(int x, int y) => new((int)p.Center.X + x, (int)p.Center.Y + y, 10, 10);
            Check(p.ModProjectile.Colliding(p.Hitbox, Hit(110, 0)) == true, "Blade intersects visible forward arc");
            Check(p.ModProjectile.Colliding(p.Hitbox, Hit(-120, 0)) == false && p.ModProjectile.Colliding(p.Hitbox, Hit(160, 160)) == false, "Rear and corner excluded");
        });
        Run("Very slow melee animation is not cut short by default lifetime", () => {
            Item item = Weapon(); player.GetAttackSpeed(DamageClass.Melee) = 0.05f; player.ApplyItemAnimation(item); Fire(item);
            Projectile p = Shots("MantisSlash").Single();
            Check(p.ai[2] > 120 && p.timeLeft >= p.ai[2], "Lifetime covers the engine animation even under severe attack-speed reduction");
        });
        Run("Crossing and worm group hit only once in one slash", () => {
            Item item = Weapon(); Fire(item); Projectile p = Shots("MantisSlash").Single(); NPC n = Enemy(), seg = Enemy(); seg.realLife = n.whoAmI;
            p.ModProjectile.OnHitNPC(seg, default, p.damage);
            Check(p.ModProjectile.CanHitNPC(n) == false, "Segment and root share gate");
            for (int i = 0; i < 20; i++) Helper().PostUpdate();
            Check(p.ModProjectile.CanHitNPC(n) == false, "One swing never re-hits same HP group");
        });
        Run("Twin throw shares group interval with slash and copies", () => {
            Item item = Weapon(); player.altFunctionUse = 2; Fire(item); Projectile[] pair = Shots("MantisThrow"); NPC n = Enemy(), seg = Enemy(); seg.realLife = n.whoAmI;
            Check(pair.Length == 2 && pair.All(p => p.friendly && p.DamageType == DamageClass.Melee), "Two friendly melee blades");
            pair[0].ModProjectile.OnHitNPC(n, default, pair[0].damage);
            Check(pair[1].ModProjectile.CanHitNPC(seg) == false, "Second blade cannot double hit shared HP");
            for (int i = 0; i < 7; i++) Helper().PostUpdate(); Check(pair[1].ModProjectile.CanHitNPC(seg) == false, "Group closed for seven ticks");
            Helper().PostUpdate(); Check(pair[1].ModProjectile.CanHitNPC(seg) != false, "Group opens at eight ticks");
            player.altFunctionUse = 0; Fire(item); Projectile slash = Shots("MantisSlash").Single();
            pair[1].ModProjectile.OnHitNPC(seg, default, pair[1].damage); Check(slash.ModProjectile.CanHitNPC(n) == false, "Both modes share interval");
        });
        Run("Repeat throw and remote cannot exceed pair", () => {
            Item item = Weapon(); player.altFunctionUse = 2; Fire(item); Fire(item);
            Check(Shots("MantisThrow").Length == 2 && !item.ModItem.CanUseItem(player), "Throw while pair active blocked");
            player.inventory[0] = item = new Item(item.type); Fire(item); Check(Shots("MantisThrow").Length == 2, "Copy cannot bypass active pair");
            foreach (Projectile p in Shots("MantisThrow")) p.Kill(); Main.myPlayer = 1; Fire(item); Main.myPlayer = 0;
            Check(Shots("MantisThrow").Length == 0, "Remote cannot spawn twin throw");
        });
        Run("Synced target, spin, finite boomerang without remote MouseWorld", () => {
            Item item = Weapon(); player.altFunctionUse = 2; Fire(item); Projectile p = Shots("MantisThrow")[0];
            float x = p.ai[1], y = p.ai[2]; Main.myPlayer = 1; Main.mouseX = -90000; Main.mouseY = 90000;
            float rot = p.rotation; p.ModProjectile.AI();
            Check(p.ai[1] == x && p.ai[2] == y && p.rotation != rot && p.extraUpdates == 1, "Remote uses synced point and spins"); Main.myPlayer = 0;
            int steps = 0; while (p.active && steps++ < 400) { p.ModProjectile.AI(); p.position += p.velocity; }
            Check(!p.active && steps < 400, "Returns to owner within finite flight");
            Projectile last = Shots("MantisThrow").FirstOrDefault(); if (last != null) { last.timeLeft = 1; last.Update(last.whoAmI); Check(!last.active, "Actual engine expiry terminates blade"); }
        });
        Run("Projectile snapshots restore remote swing and throw without local cursor", () => {
            Item item = Weapon(); Fire(item); Projectile slash = Shots("MantisSlash").Single();
            player.altFunctionUse = 2; Fire(item); Projectile thrown = Shots("MantisThrow")[0];
            for (int i = 0; i < 4; i++) { slash.ModProjectile.AI(); thrown.ModProjectile.AI(); }
            byte[] Snapshot(Projectile p) { using MemoryStream stream = new(); using BinaryWriter writer = new(stream); p.ModProjectile.SendExtraAI(writer); return stream.ToArray(); }
            byte[] slashData = Snapshot(slash), throwData = Snapshot(thrown);
            foreach (Projectile p in Main.projectile) p.active = false;
            Main.myPlayer = 1; Main.mouseX = -12345; player.selectedItem = 1;
            Projectile Replica(Projectile source, byte[] data) {
                Projectile copy = new(); copy.SetDefaults(source.type); copy.owner = 0; copy.active = true; copy.Center = source.Center; copy.velocity = source.velocity;
                copy.ai[0] = source.ai[0]; copy.ai[1] = source.ai[1]; copy.ai[2] = source.ai[2];
                using MemoryStream stream = new(data); using BinaryReader reader = new(stream); copy.ModProjectile.ReceiveExtraAI(reader); copy.ModProjectile.AI(); return copy;
            }
            Projectile s = Replica(slash, slashData), t = Replica(thrown, throwData);
            Check(s.active && s.localAI[0] == 5 && s.ai[1] == 0, $"Remote swing restored age, aim, and ignores remote selected slot (active={s.active}, age={s.localAI[0]}, angle={s.ai[1]}, owner={s.owner}, local={Main.myPlayer})");
            Check(t.active && t.ai[1] == thrown.ai[1] && t.ai[2] == thrown.ai[2] && Shots("MantisThrow").Length == 0, "Replica uses target snapshot without spawning child blades");
            Main.myPlayer = 0;
        });
        Run("Engine combat accepts one twin hit for overlapping shared HP segments", () => {
            Item item = Weapon(); player.altFunctionUse = 2; Fire(item); Projectile[] pair = Shots("MantisThrow"); NPC n = Enemy(), seg = Enemy(); seg.realLife = n.whoAmI;
            foreach (NPC target in new[] { n, seg }) { target.defense = 0; target.knockBackResist = 0; target.position = player.Center + new Vector2(90, -20); }
            foreach (Projectile p in pair) { p.Center = n.Center; p.rotation = 0; }
            MantisHitObserver.Hits = 0; int before = n.life;
            pair[0].Damage(); pair[1].Damage();
            Check(MantisHitObserver.Hits == 1 && n.life < before, "Actual Projectile.Damage pipeline records one accepted group hit and HP loss");
        });
        foreach (string reason in new[] { "swap", "copy", "death", "CC" })
            Run("Cancel active attacks: " + reason, () => {
                Item item = Weapon(); Fire(item); player.altFunctionUse = 2; Fire(item);
                if (reason == "swap") player.selectedItem = 1;
                if (reason == "copy") player.inventory[0] = new Item(item.type);
                if (reason == "death") player.dead = true;
                if (reason == "CC") player.webbed = true;
                foreach (Projectile p in Main.projectile.Where(p => p.active).ToArray()) p.ModProjectile.AI();
                Check(Shots("MantisSlash").Length + Shots("MantisThrow").Length == 0, "Canceled attacks die without children");
            });
        Run("Movement, dash and immunity untouched", () => {
            Item item = Weapon(); player.velocity = new Vector2(4, -3); player.dashType = 2; player.dashDelay = 7; player.immune = false; player.immuneTime = 0;
            Fire(item); player.altFunctionUse = 2; Fire(item);
            foreach (Projectile p in Main.projectile.Where(p => p.active).ToArray()) p.ModProjectile.AI();
            Check(player.velocity == new Vector2(4, -3) && player.dashType == 2 && player.dashDelay == 7 && !player.immune && player.immuneTime == 0, "No movement or invulnerability writes");
        });
        Main.netMode = oldMode; Main.myPlayer = oldPlayer;
        File.WriteAllLines(Path.Combine(root, "verification", "task4-runtime.txt"), output);
        Environment.Exit(failures == 0 ? 0 : 2);
    }
    void RunRampChecks()
    {
        bool oldFocus = Main.hasFocus, oldMouse = Main.blockMouse, oldInput = Main.blockInput, oldSign = Main.editSign, oldChest = Main.editChest;
        Main.hasFocus = true; Main.blockMouse = Main.blockInput = Main.editSign = Main.editChest = false;
        try
        {
        // Break caught: charge advances per swing/extra update, grows unbounded, or changes cadence.
        Run("Held melee ramp is time based, capped, and keeps three-hit cadence", () => {
            Item item = Weapon(); player.controlUseItem = true; Fire(item);
            Projectile first = Shots("MantisSlash").Single(); float duration = first.ai[2];
            Check(first.damage == 3000, "First swing starts at base damage"); first.Kill();
            for (int i = 0; i < 60; i++) Helper().PostUpdate();
            Fire(item); Projectile half = Shots("MantisSlash").Single();
            Check(half.damage == 3750 && half.ai[2] == duration, "One second adds 25 percent damage without accelerating animation"); half.Kill();
            for (int i = 0; i < 60; i++) Helper().PostUpdate();
            foreach (int expected in new[] { 4500, 4500, 6750, 4500 }) {
                Fire(item); Projectile p = Shots("MantisSlash").Single();
                Check(p.damage == expected && p.ai[2] == duration, "Peak strength preserves combo multipliers and cadence"); p.Kill();
            }
            for (int i = 0; i < 600; i++) Helper().PostUpdate();
            Fire(item); Check(Shots("MantisSlash").Single().damage == 4500, "Long hold cannot exceed 50 percent ramp damage");
        });
        // Break caught: active swing reads live player charge or broadphase clips the expanded arc.
        Run("Every swing snapshots its damage and visible collision geometry", () => {
            Item item = Weapon(); player.controlUseItem = true; Fire(item); Projectile initial = Shots("MantisSlash").Single();
            for (int i = 0; i < 120; i++) Helper().PostUpdate();
            initial.localAI[0] = 6;
            Rectangle Hit(Projectile p, int x, int y) => new((int)p.Center.X + x, (int)p.Center.Y + y, 4, 4);
            Check(initial.damage == 3000 && initial.ModProjectile.Colliding(initial.Hitbox, Hit(initial, 300, 0)) == false, "Already spawned swing stays at base strength");
            initial.Kill(); Fire(item); Projectile peak = Shots("MantisSlash").Single(); peak.localAI[0] = 6;
            Check(peak.ModProjectile.Colliding(peak.Hitbox, Hit(peak, 300, 0)) == true && peak.Hitbox.Intersects(Hit(peak, 300, 0)), "Peak arc reaches 300 pixels and broadphase contains its tip");
            Check(peak.ModProjectile.Colliding(peak.Hitbox, Hit(peak, 335, 0)) == false && peak.ModProjectile.Colliding(peak.Hitbox, Hit(peak, -250, 0)) == false,
                "Outside tip and invisible rear remain harmless");
            player.controlUseItem = false; Helper().PostUpdate();
            Check(peak.damage == 4500 && peak.ModProjectile.Colliding(peak.Hitbox, Hit(peak, 300, 0)) == true, "Release resets future attacks without shrinking the existing swing");
        });
        foreach (string reason in new[] { "release", "right control", "right mode", "swap", "copy", "death", "CC", "no items", "UI", "focus", "blocked mouse", "blocked input", "sign", "chest" })
            Run("Held ramp resets on " + reason, () => {
                Item item = Weapon(); player.controlUseItem = true; Fire(item); Shots("MantisSlash").Single().Kill();
                for (int i = 0; i < 120; i++) Helper().PostUpdate();
                Fire(item); Projectile charged = Shots("MantisSlash").Single();
                Check(charged.damage == 4500, "Reset fixture reaches peak strength before " + reason); charged.Kill();
                if (reason == "release") player.controlUseItem = false;
                if (reason == "right control") player.controlUseTile = true;
                if (reason == "right mode") player.altFunctionUse = 2;
                if (reason == "swap") player.selectedItem = 1;
                if (reason == "copy") player.inventory[0] = new Item(item.type);
                if (reason == "death") { player.dead = true; Helper().UpdateDead(); }
                if (reason == "CC") player.webbed = true;
                if (reason == "no items") player.noItems = true;
                if (reason == "UI") player.mouseInterface = true;
                if (reason == "focus") Main.hasFocus = false;
                if (reason == "blocked mouse") Main.blockMouse = true;
                if (reason == "blocked input") Main.blockInput = true;
                if (reason == "sign") Main.editSign = true;
                if (reason == "chest") Main.editChest = true;
                Helper().PostUpdate();
                player.controlUseItem = true; player.controlUseTile = false; player.altFunctionUse = 0; player.selectedItem = 0;
                player.dead = player.webbed = player.noItems = player.mouseInterface = false; item = player.inventory[0];
                Main.hasFocus = true; Main.blockMouse = Main.blockInput = Main.editSign = Main.editChest = false;
                Fire(item); Check(Shots("MantisSlash").Single().damage == 3000, "Next swing restarts at base strength after " + reason);
            });
        // Break caught: throw keeps a full ramp alive or charge bypasses the shared HP-group interval.
        Run("Throw resets held ramp and peak slashes retain the eight-tick root gate", () => {
            Item item = Weapon(); player.controlUseItem = true; Fire(item); Shots("MantisSlash").Single().Kill();
            for (int i = 0; i < 120; i++) Helper().PostUpdate();
            Fire(item); Projectile peak = Shots("MantisSlash").Single(); Check(peak.damage == 4500, "Group gate fixture reaches peak strength");
            NPC root = Enemy(), segment = Enemy(); segment.realLife = root.whoAmI;
            peak.ModProjectile.OnHitNPC(segment, default, peak.damage); peak.Kill(); Fire(item); Projectile next = Shots("MantisSlash").Single();
            Check(next.ModProjectile.CanHitNPC(root) == false, "Charged consecutive slash cannot bypass root interval");
            for (int i = 0; i < 7; i++) Helper().PostUpdate();
            Check(next.ModProjectile.CanHitNPC(root) == false, "Charged group stays closed through seven ticks"); Helper().PostUpdate();
            Check(next.ModProjectile.CanHitNPC(root) != false, "Charged group reopens at eight ticks"); next.Kill();
            player.altFunctionUse = 2; Fire(item); player.altFunctionUse = 0; Fire(item); Projectile restart = Shots("MantisSlash").Single();
            Check(restart.damage == 3000 && restart.ai[0] == 0, "Right-click throw clears ramp and combo immediately");
        });
        // Break caught: received slash recomputes charge from remote input or loses expanded width/lifetime.
        Run("Late remote snapshot restores ramp geometry and remaining lifetime independently", () => {
            Item item = Weapon(); player.controlUseItem = true; Fire(item); Shots("MantisSlash").Single().Kill();
            for (int i = 0; i < 120; i++) Helper().PostUpdate();
            player.GetAttackSpeed(DamageClass.Melee) = 0.0625f; player.ApplyItemAnimation(item); Fire(item);
            Projectile owner = Shots("MantisSlash").Single();
            for (int i = 0; i < 96; i++) owner.ModProjectile.AI();
            using MemoryStream stream = new(); using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true)) owner.ModProjectile.SendExtraAI(writer);
            player.controlUseItem = false; Helper().PostUpdate(); Main.myPlayer = 1; player.selectedItem = 1; Main.mouseX = -50000;
            Projectile replica = new(); replica.SetDefaults(owner.type); replica.owner = 0; replica.active = true;
            replica.Center = owner.Center; replica.extraUpdates = 1; replica.damage = owner.damage;
            replica.ai[0] = owner.ai[0]; replica.ai[1] = owner.ai[1]; replica.ai[2] = owner.ai[2];
            stream.Position = 0; using (BinaryReader reader = new(stream)) replica.ModProjectile.ReceiveExtraAI(reader);
            Rectangle forward = new((int)replica.Center.X + 300, (int)replica.Center.Y, 4, 4);
            Check(replica.damage == 4500 && replica.ModProjectile.Colliding(replica.Hitbox, forward) == true && replica.Hitbox.Intersects(forward),
                "Remote preserves charged damage and broadphase geometry despite empty local charge/input");
            int ticks = 0; while (replica.active && ticks++ < 100) replica.Update(replica.whoAmI);
            Check(!replica.active && ticks == 96, "Charged remote with extra updates expires exactly at remaining 96 ticks"); Main.myPlayer = 0;
        });
        }
        finally
        {
            Main.hasFocus = oldFocus; Main.blockMouse = oldMouse; Main.blockInput = oldInput; Main.editSign = oldSign; Main.editChest = oldChest;
        }
    }
    void RunFix1Checks()
    {
        // Break caught: comboIdle used to expire during a valid unfinished swing.
        foreach (float speed in new[] { 1f, 0.05f })
            Run("I1 full sequential swing combo at speed " + speed, () => {
                Item item = Weapon(); player.GetAttackSpeed(DamageClass.Melee) = speed;
                foreach (int mode in new[] { 0, 1, 2, 0 })
                {
                    player.ApplyItemAnimation(item); Fire(item); Projectile p = Shots("MantisSlash").Single();
                    Check(p.ai[0] == mode, "Full consecutive swings retain 0/1/2/0 combo");
                    int elapsed = 0, duration = (int)p.ai[2];
                    while (p.active && elapsed++ < duration + 2) { Helper().PostUpdate(); p.Update(p.whoAmI); }
                    Check(!p.active && elapsed >= duration, "Actual engine counts down full swing duration");
                }
                for (int i = 0; i < 44; i++) Helper().PostUpdate();
                player.ApplyItemAnimation(item); Fire(item);
                Check(Shots("MantisSlash").Single().ai[0] == 1, "44 idle ticks after completion still retain combo");
                Projectile last = Shots("MantisSlash").Single(); int bound = (int)last.ai[2] + 2;
                for (int i = 0; last.active && i < bound; i++) { Helper().PostUpdate(); last.Update(last.whoAmI); }
                for (int i = 0; i < 45; i++) Helper().PostUpdate();
                player.ApplyItemAnimation(item); Fire(item);
                Check(Shots("MantisSlash").Single().ai[0] == 0, "45 idle ticks after completion reset combo");
            });
        // Break caught: SyncProjectile sets defaults and receives ExtraAI, never owner OnSpawn/timeLeft.
        foreach ((int elapsed, int extraUpdates) in new[] { (0, 0), (60, 0), (191, 0), (192, 0), (60, 1) })
            Run($"I2 slow remote remaining lifetime at age {elapsed}, extraUpdates {extraUpdates}", () => {
                Item item = Weapon(); player.GetAttackSpeed(DamageClass.Melee) = 0.0625f; player.ApplyItemAnimation(item); Fire(item);
                Projectile owner = Shots("MantisSlash").Single();
                Check(owner.ai[2] == 192, "Fixture uses actual engine slow 192-tick animation");
                for (int i = 0; i < elapsed; i++) { Helper().PostUpdate(); owner.Update(owner.whoAmI); }
                using MemoryStream stream = new(); using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true)) owner.ModProjectile.SendExtraAI(writer);
                Main.myPlayer = 1;
                Projectile replica = new(); replica.SetDefaults(owner.type); replica.owner = 0; replica.active = true;
                replica.Center = owner.Center; replica.velocity = owner.velocity; replica.extraUpdates = extraUpdates;
                replica.ai[0] = owner.ai[0]; replica.ai[1] = owner.ai[1]; replica.ai[2] = owner.ai[2];
                stream.Position = 0; using (BinaryReader reader = new(stream)) replica.ModProjectile.ReceiveExtraAI(reader);
                Check(elapsed < 192 ? replica.active : !replica.active, "Expired snapshot stays dead, valid snapshot restores remaining lifetime");
                int ticks = 0;
                while (replica.active && ticks++ < 194) replica.Update(replica.whoAmI);
                Check(!replica.active && ticks == 192 - elapsed, $"Remote engine countdown completes exactly at synced attack end (ticks={ticks}, age={replica.localAI[0]}, left={replica.timeLeft}, active={replica.active}, duration={replica.ai[2]})");
                float age = replica.localAI[0];
                replica.ModProjectile.AI(); Check(!replica.active && replica.localAI[0] >= age, "Completion cannot become an infinite holdout");
                Main.myPlayer = 0;
            });
    }
    void Run(string name, Action test) {
        Clear(); player = Main.player[0] = new Player { whoAmI = 0, active = true, statLife = 500, statLifeMax2 = 500 };
        player.Center = new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16 - 400);
        bool focus = Main.hasFocus, mouse = Main.blockMouse, input = Main.blockInput, sign = Main.editSign, chest = Main.editChest;
        try { test(); output.Add("PASS: " + name); } catch (Exception e) { failures++; output.Add("FAIL: " + name + ": " + e); }
        finally { Main.myPlayer = 0; Main.hasFocus = focus; Main.blockMouse = mouse; Main.blockInput = input; Main.editSign = sign; Main.editChest = chest; Clear(); }
    }
    ModPlayer Helper() { ModPlayer proto = content.GetContent<ModPlayer>().Single(p => p.Name == "MantisPlayer"); return player.GetModPlayer(proto); }
    Item Weapon() { Check(content.TryFind("ArasakaMantisBlades", out ModItem prototype), "Weapon missing (expected RED)"); Item item = new(prototype.Type); player.inventory[0] = item; player.selectedItem = 0; Main.screenPosition = player.Center - new Vector2(300, 300); Main.mouseX = 500; Main.mouseY = 300; return item; }
    void Fire(Item item) => item.ModItem.Shoot(player, new EntitySource_ItemUse_WithAmmo(player, item, 0), player.Center, Vector2.UnitX * 18, 0, item.damage, item.knockBack);
    NPC Enemy() { NPC n = Main.npc[NPC.NewNPC(new EntitySource_Misc("Task4"), (int)player.Center.X + 110, (int)player.Center.Y, NPCID.Zombie)]; n.lifeMax = n.life = 100000; return n; }
    Projectile[] Shots(string name) { Check(content.TryFind(name, out ModProjectile p), name + " missing"); return Main.projectile.Where(x => x.active && x.type == p.Type).ToArray(); }
    static void Clear() { foreach (Projectile p in Main.projectile) p.active = false; foreach (NPC n in Main.npc) n.active = false; }
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
public class MantisHitObserver : GlobalNPC
{
    public static int Hits;
    public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
    {
        // Suppress client font-dependent numbers only; the combat path and damage remain unchanged.
        if (projectile.ModProjectile?.Name == "MantisThrow") modifiers.HideCombatText();
    }
    public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
    {
        if (projectile.ModProjectile?.Name == "MantisThrow") Hits++;
    }
}
