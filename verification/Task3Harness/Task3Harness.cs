using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Terraria.GameInput;
using AdamSmasherMod.Common;

namespace Task3HarnessMod;
public class Task3Harness : Mod { }
public class Task3Checks : ModSystem
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
        bool oldFocus = Main.hasFocus;
        Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
        Main.hasFocus = true; // Headless fixture simulates allowed local gameplay input.
        if (Environment.GetEnvironmentVariable("ADAM_SMASHER_TASK3_FIX1_ONLY") == "1")
        {
            RunInputSuppressionChecks();
            Main.netMode = oldMode; Main.myPlayer = oldPlayer; Main.hasFocus = oldFocus;
            File.WriteAllLines(Path.Combine(root, "verification", "task3-fix1-runtime.txt"), output);
            Environment.Exit(failures == 0 ? 0 : 2);
            return;
        }
        Run("VFX line samples one pixel of the 1x1000 MagicPixel texture", RunLineDrawCheck);
        Run("Magic defaults and real mana consumption", () => {
            Item item = Weapon();
            Check(item.DamageType == DamageClass.Magic && item.mana > 0 && item.autoReuse, "Magic class, real mana and auto fire");
            player.manaCost = 0.5f;
            int cost = player.GetManaCost(item), before = player.statMana;
            Check(cost > 0 && player.CheckMana(item, -1, true, true) && player.statMana == before - cost, "Actual engine mana hooks consume reduced cost");
        });
        Run("Real item-use mana path charges every salvo with and without gear reduction", () => {
            Item item = Weapon();
            var canUse = typeof(Player).GetMethod("ItemCheck_CheckCanUse", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Check(canUse != null, "Verified engine item-use method exists");
            player.manaCost = 1f;
            int before = player.statMana;
            for (int use = 0; use < 10; use++)
                Check((bool)canUse.Invoke(player, new object[] { item }), "Every neutral-gear salvo can be paid");
            Check(player.statMana == before - 100, "Ten real use gates consume 100 mana after halving the cost");
            player.manaCost = 0.55f; // The supplied gear modifier; the new base cost of 10 rounds to 5.
            before = player.statMana;
            for (int use = 0; use < 10; use++)
                Check((bool)canUse.Invoke(player, new object[] { item }), "Reduced-cost salvo can be paid");
            Check(player.statMana == before - 50, "Gear hooks retain their reduced 5-mana cost rather than being bypassed");
            player.statMana = 0;
            Check(!(bool)canUse.Invoke(player, new object[] { item }), "Real item-use gate refuses an unpaid salvo");
        });
        Run("Charged shot respects reduced initial and periodic mana budget", () => {
            var canUse = typeof(Player).GetMethod("ItemCheck_CheckCanUse", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            foreach (float discount in new[] { 1f, 0.55f })
            {
                foreach (int holdTicks in new[] { 60, 120 })
                {
                    Clear(); Item item = Weapon(); player.altFunctionUse = 2; player.controlUseTile = true;
                    player.statMana = 500; player.manaCost = discount;
                    Check((bool)canUse.Invoke(player, new object[] { item }), "Engine pays initial charge use");
                    Fire(item); Projectile charge = Shots("PlasmaCharge").Single();
                    for (int tick = 0; tick < holdTicks; tick++) charge.ModProjectile.AI();
                    if (holdTicks == 60) { player.controlUseTile = false; charge.ModProjectile.AI(); }
                    int expected = (discount == 1f ? 10 : 5) * (1 + holdTicks / 20);
                    Check(player.statMana == 500 - expected, "Initial and periodic costs stay within 40/70 base or 20/35 discounted mana");
                    Check(charge.active && charge.ai[0] == 1 && charge.friendly, "Paid charge still releases normally");
                    int beforeImpact = player.statMana; charge.ModProjectile.OnHitNPC(new NPC(), default, charge.damage); charge.Kill();
                    Check(player.statMana == beforeImpact && Shots("PlasmaField").Length == 1, "Impact/field does not add a hidden payment");
                    // A fresh player resets the shared cooldown between independent budget cases.
                    player = Main.player[0] = new Player { whoAmI = 0, active = true, statLife = 500, statLifeMax2 = 500, statMana = 500, statManaMax2 = 500, Center = player.Center };
                }
            }
        });
        Run("Two distributed owner missiles; remote cannot spawn", () => {
            Item item = Weapon(); NPC a = Enemy(180, 0), b = Enemy(240, 60);
            Fire(item);
            Projectile[] shots = Shots("SmartRocket");
            Check(shots.Length == 2 && shots.All(p => p.friendly && !p.hostile && p.DamageType == DamageClass.Magic), "Exactly two friendly magic shots");
            Check(shots[0].ai[0] != shots[1].ai[0], "Two targets distributed");
            Main.myPlayer = 1; Fire(item); Main.myPlayer = 0;
            Check(Shots("SmartRocket").Length == 2, "Remote Shoot cannot duplicate salvo");
        });
        Run("Single target concentrates; invalid target safely retargets; lifetime finite", () => {
            Item item = Weapon(); NPC a = Enemy(200, 0); Fire(item);
            Projectile[] shots = Shots("SmartRocket");
            Check(shots.Length == 2 && shots.All(p => p.ai[0] == a.whoAmI + 1), "Single boss receives both rockets");
            a.active = false; NPC b = Enemy(250, 50);
            for (int i = 0; i < 13; i++) shots[0].ModProjectile.AI();
            Check(shots[0].ai[0] == b.whoAmI + 1, "Dead target replaced by valid target");
            b.dontTakeDamage = true;
            for (int i = 0; i < 13; i++) shots[0].ModProjectile.AI();
            Check(shots[0].ai[0] == 0 && float.IsFinite(shots[0].velocity.X), "Invulnerable target not pursued");
            shots[0].timeLeft = 1; shots[0].Update(shots[0].whoAmI);
            Check(!shots[0].active, "Engine expires missile");
        });
        Run("Explosion excludes primary worm group and hits collateral once", () => {
            Item item = Weapon(); NPC rootNpc = Enemy(200, 0), segment = Enemy(205, 0), secondary = Enemy(220, 20), secondSegment = Enemy(225, 20);
            segment.realLife = rootNpc.whoAmI; secondSegment.realLife = secondary.whoAmI;
            Fire(item); Projectile rocket = Shots("SmartRocket")[0];
            rocket.ModProjectile.OnHitNPC(segment, default, rocket.damage); rocket.Kill();
            Projectile[] explosions = Shots("RocketExplosion");
            Check(explosions.Length == 1, "Hit then kill creates only one explosion");
            Projectile exp = explosions[0];
            Check(exp.damage <= rocket.damage * 0.3f && exp.ModProjectile.CanHitNPC(rootNpc) == false && exp.ModProjectile.CanHitNPC(segment) == false, "Primary group excluded and collateral bounded");
            Check(exp.ModProjectile.CanHitNPC(secondary) != false, "Secondary allowed");
            exp.ModProjectile.OnHitNPC(secondary, default, exp.damage);
            Check(exp.ModProjectile.CanHitNPC(secondSegment) == false, "Collateral shared root cannot multiply damage");
        });
        foreach (string reason in new[] { "swap", "same-type copy", "death", "CC", "no mana", "undercharge", "UI" })
            Run("Charge cancel: " + reason, () => {
                Item item = Weapon(); player.altFunctionUse = 2; player.controlUseTile = true; Fire(item);
                Projectile p = Shots("PlasmaCharge").Single();
                for (int i = 0; i < 65; i++) p.ModProjectile.AI();
                int mana = player.statMana;
                switch (reason) {
                    case "swap": player.selectedItem = 1; break;
                    case "same-type copy": player.inventory[0] = new Item(item.type); break;
                    case "death": player.dead = true; break;
                    case "CC": player.webbed = true; break;
                    case "no mana": player.statMana = 0; break;
                    case "undercharge": p.ai[1] = 10; break;
                    case "UI": player.mouseInterface = true; break;
                }
                player.controlUseTile = false; p.ModProjectile.AI();
                Check(!p.active && Shots("PlasmaField").Length == 0 && Shots("PlasmaCharge").Length == 0, "Cancel before release creates no shot or field");
                Check(mana < 500, "Charging paid additional mana");
            });
        Run("Charge release, shared cooldown, remote no-spawn, one finite field", () => {
            Item item = Weapon(); player.altFunctionUse = 2; player.controlUseTile = true; Fire(item);
            Projectile p = Shots("PlasmaCharge").Single();
            player.ownedProjectileCounts[p.type] = 1;
            Check(!item.ModItem.CanUseItem(player), "Active charge blocks duplicate");
            for (int i = 0; i < 65; i++) p.ModProjectile.AI();
            Main.myPlayer = 1; player.controlUseTile = false; p.ModProjectile.AI();
            Check(p.ai[0] == 0 && Shots("PlasmaField").Length == 0, "Remote release cannot spawn");
            Main.myPlayer = 0; p.ModProjectile.AI();
            Check(p.active && p.ai[0] == 1 && p.friendly && p.damage > item.damage, "Released charge becomes heavy homing shot");
            player.altFunctionUse = 0;
            Check(item.ModItem.CanUseItem(player), "Heavy shot in flight does not block primary fire");
            player.altFunctionUse = 2;
            player.ownedProjectileCounts[p.type] = 0; player.inventory[0] = new Item(item.type);
            Check(!player.HeldItem.ModItem.CanUseItem(player), "Cooldown applies to another copy");
            NPC n = Enemy(300, 0); p.ModProjectile.OnHitNPC(n, default, p.damage); p.Kill();
            Check(Shots("PlasmaField").Length == 1, "Impact then kill creates one field");
            Projectile extra = Spawn("PlasmaCharge", 1); extra.Kill();
            Check(Shots("PlasmaField").Length == 1, "New field replaces existing owner field");
            Projectile field = Shots("PlasmaField").Single();
            Check(field.timeLeft <= 180 && field.ModProjectile.Colliding(field.Hitbox, new Rectangle((int)field.Center.X + 180, (int)field.Center.Y + 180, 1, 1)) == false, "Circle collision excludes corner");
            NPC seg = Enemy(305, 0); seg.realLife = n.whoAmI;
            field.ModProjectile.OnHitNPC(n, default, field.damage);
            Check(field.ModProjectile.CanHitNPC(seg) == false, "Field limits group hit frequency");
            for (int i = 0; i < 20; i++) field.ModProjectile.AI();
            Check(field.ModProjectile.CanHitNPC(seg) != false, "Group reopens at deliberate interval");
            field.timeLeft = 1; field.Update(field.whoAmI); Check(!field.active, "Field expires in actual engine");
        });
        Run("Maximum charge cannot hold forever; canceled OnKill never creates field", () => {
            Item item = Weapon(); player.altFunctionUse = 2; player.controlUseTile = true; Fire(item);
            Projectile p = Shots("PlasmaCharge").Single(); player.statMana = 5000;
            for (int i = 0; i < 130 && p.ai[0] == 0; i++) p.ModProjectile.AI();
            Check(p.ai[0] == 1, "Bounded charge automatically releases");
            Clear(); Weapon(); Projectile canceled = Spawn("PlasmaCharge", 0); canceled.Kill();
            Check(Shots("PlasmaField").Length == 0, "Canceled kill is not release");
        });
        Run("Remote charge-to-flight state survives holdout lifetime", () => {
            Weapon(); Projectile replica = Spawn("PlasmaCharge", 0);
            Main.myPlayer = 1;
            replica.ModProjectile.AI();
            Check(replica.timeLeft == 2, "Remote holdout exists before release snapshot");
            replica.ai[0] = 1; replica.ai[1] = 65; replica.ai[2] = 0;
            replica.ModProjectile.AI();
            Check(replica.timeLeft > 100 && replica.scale > 1 && replica.friendly && replica.tileCollide, "State packet initializes remote flight lifetime, size and collision");
            int count = Shots("PlasmaField").Length;
            replica.Kill();
            Check(Shots("PlasmaField").Length == count, "Remote impact/expiry never spawns fields");
        });
        Main.netMode = oldMode; Main.myPlayer = oldPlayer; Main.hasFocus = oldFocus;
        File.WriteAllLines(Path.Combine(root, "verification", "task3-runtime.txt"), output);
        Environment.Exit(failures == 0 ? 0 : 2);
    }
    void RunLineDrawCheck()
    {
        const System.Reflection.BindingFlags instancePrivate = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var textureType = typeof(Microsoft.Xna.Framework.Graphics.Texture2D);
        // The installed MagicPixel.xnb is 1x1000. Keep the same dimensions in a
        // CPU-only texture: no graphics device is needed because the draw hook
        // captures the production call before SpriteBatch/GPU access.
        var texture = (Microsoft.Xna.Framework.Graphics.Texture2D)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(textureType);
        GC.SuppressFinalize(texture);
        textureType.GetField("<Width>k__BackingField", instancePrivate).SetValue(texture, 1);
        textureType.GetField("<Height>k__BackingField", instancePrivate).SetValue(texture, 1000);
        var assetType = typeof(ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>);
        var asset = (ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>)Activator.CreateInstance(assetType,
            instancePrivate, null, new object[] { "Task3/MagicPixelDimensions" }, null);
        assetType.GetMethod("SubmitLoadedContent", instancePrivate).Invoke(asset, new object[] { texture, null });
        Check(asset.Value == texture && texture.Width == 1 && texture.Height == 1000, "CPU fixture retains the real 1x1000 texture dimensions");
        var line = typeof(AnnihilationPlayer).Assembly.GetType("AdamSmasherMod.Common.WeaponVisuals")
            .GetMethod("Line", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Check(line != null, "Actual production VFX line method exists");

        var oldPixel = Terraria.GameContent.TextureAssets.MagicPixel;
        bool oldServer = Main.dedServ;
        Vector2 oldScreen = Main.screenPosition;
        int draws = 0;
        Rectangle? source = null;
        Vector2 drawScale = default, drawPosition = default, drawOrigin = default;
        Microsoft.Xna.Framework.Graphics.Texture2D drawnTexture = null;
        Terraria.On_Main.hook_EntitySpriteDraw_Texture2D_Vector2_Nullable1_Color_float_Vector2_Vector2_SpriteEffects_float capture =
            (orig, drawn, position, frame, color, rotation, origin, scale, effects, depth) => {
                draws++; drawnTexture = drawn; source = frame;
                drawPosition = position; drawOrigin = origin; drawScale = scale;
                // Deliberately do not call orig: the dedicated-server fixture has no GPU.
            };
        try
        {
            Terraria.On_Main.EntitySpriteDraw_Texture2D_Vector2_Nullable1_Color_float_Vector2_Vector2_SpriteEffects_float += capture;
            Terraria.GameContent.TextureAssets.MagicPixel = asset;
            Main.dedServ = false; Main.screenPosition = new Vector2(40, 50);
            line.Invoke(null, new object[] { new Vector2(100, 200), new Vector2(118, 200), Color.White, 8f });
            Check(draws == 1 && drawnTexture == texture, "Actual Line emits exactly one MagicPixel draw");
            float drawnLength = (source?.Width ?? texture.Width) * drawScale.X;
            float drawnWidth = (source?.Height ?? texture.Height) * drawScale.Y;
            Check(MathF.Abs(drawnLength - 18f) < 0.001f && MathF.Abs(drawnWidth - 8f) < 0.001f,
                $"An 18x8 line must draw at 18x8, not the atlas size (actual {drawnLength}x{drawnWidth}; source {source?.ToString() ?? "null"})");
            Check(source == new Rectangle(0, 0, 1, 1), "Line explicitly selects the 1x1 white pixel");
            Check(drawPosition == new Vector2(60, 150) && drawOrigin == new Vector2(0, 0.5f), "Line starts at its screen coordinate and remains centered on its axis");
            Main.dedServ = true;
            line.Invoke(null, new object[] { new Vector2(100, 200), new Vector2(118, 200), Color.White, 8f });
            Main.dedServ = false;
            line.Invoke(null, new object[] { new Vector2(100, 200), new Vector2(100, 200), Color.White, 8f });
            Check(draws == 1, "Dedicated-server and zero-length lines do not emit draws");
        }
        finally
        {
            Terraria.On_Main.EntitySpriteDraw_Texture2D_Vector2_Nullable1_Color_float_Vector2_Vector2_SpriteEffects_float -= capture;
            Terraria.GameContent.TextureAssets.MagicPixel = oldPixel;
            Main.dedServ = oldServer; Main.screenPosition = oldScreen;
        }
    }
    void RunInputSuppressionChecks()
    {
        bool focus = Main.hasFocus, blocked = Main.blockInput, sign = Main.editSign, chest = Main.editChest, mouse = Main.blockMouse;
        bool locked = PlayerInput.LockGamepadTileUseButton;
        InputMode mode = PlayerInput.CurrentInputMode;
        // The headless lab has no gamepad UI navigation. Set only its backing
        // state as a fixture; production reads the public InBuildingMode API.
        var buildingField = typeof(PlayerInput).GetField("_InBuildingMode", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        bool building = PlayerInput.InBuildingMode;
        void NeutralInput()
        {
            Main.hasFocus = true; Main.blockInput = Main.editSign = Main.editChest = Main.blockMouse = false;
            PlayerInput.LockGamepadTileUseButton = false; PlayerInput.CurrentInputMode = InputMode.Keyboard;
            buildingField.SetValue(null, false);
        }
        Projectile Charged()
        {
            NeutralInput(); Item item = Weapon(); player.altFunctionUse = 2; player.controlUseTile = true;
            Fire(item); Projectile p = Shots("PlasmaCharge").Single();
            for (int tick = 0; tick < 65; tick++) p.ModProjectile.AI();
            Check(p.active && p.ai[0] == 0 && p.ai[1] >= 60, "Fixture holds a sufficient, paid charge");
            return p;
        }
        try
        {
            foreach (string flag in new[] { "focus loss", "blockInput", "editSign", "editChest", "gamepad tile lock", "building mode" })
                Run("I1 suppression cancels: " + flag, () => {
                    Projectile p = Charged();
                    switch (flag)
                    {
                        case "focus loss": Main.hasFocus = false; break;
                        case "blockInput": Main.blockInput = true; break;
                        case "editSign": Main.editSign = true; break;
                        case "editChest": Main.editChest = true; break;
                        case "gamepad tile lock": PlayerInput.CurrentInputMode = InputMode.XBoxGamepad; PlayerInput.LockGamepadTileUseButton = true; break;
                        case "building mode": buildingField.SetValue(null, true); break;
                    }
                    player.controlUseTile = false;
                    p.ModProjectile.AI();
                    Check(!p.active && p.ai[0] == 2 && !p.friendly, "Suppression cancels rather than releasing a friendly heavy shot");
                    p.ModProjectile.OnKill(0);
                    Check(Shots("PlasmaField").Length == 0 && player.GetModPlayer<AnnihilationPlayer>().PlasmaCooldownTicks == 0, "Canceled charge creates no field or cooldown");
                });
            Run("I1 ordinary real release remains valid", () => {
                Projectile p = Charged(); player.controlUseTile = false; p.ModProjectile.AI();
                Check(p.active && p.ai[0] == 1 && p.friendly && player.GetModPlayer<AnnihilationPlayer>().PlasmaCooldownTicks > 0, "Unblocked release fires heavy shot and begins cooldown");
            });
            Run("I1 gamepad lock bit alone does not cancel keyboard release", () => {
                Projectile p = Charged(); PlayerInput.LockGamepadTileUseButton = true;
                player.controlUseTile = false; p.ModProjectile.AI();
                Check(p.active && p.ai[0] == 1, "Tile lock only applies when actually using gamepad");
            });
        }
        finally
        {
            Main.hasFocus = focus; Main.blockInput = blocked; Main.editSign = sign; Main.editChest = chest; Main.blockMouse = mouse;
            PlayerInput.LockGamepadTileUseButton = locked; PlayerInput.CurrentInputMode = mode; buildingField.SetValue(null, building);
        }
    }
    void Run(string name, Action test) {
        Clear(); player = Main.player[0] = new Player { whoAmI = 0, active = true, statLife = 500, statLifeMax2 = 500, statMana = 500, statManaMax2 = 500 };
        player.Center = new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16 - 400);
        try { test(); output.Add("PASS: " + name); }
        catch (Exception e) { failures++; output.Add("FAIL: " + name + ": " + e); }
        finally { Main.myPlayer = 0; Clear(); }
    }
    Item Weapon() { Check(content.TryFind("AnnihilationProtocol", out ModItem prototype), "Weapon missing (expected RED)"); Item item = new(prototype.Type); player.inventory[0] = item; player.selectedItem = 0; Main.screenPosition = player.Center - new Vector2(300, 300); Main.mouseX = 500; Main.mouseY = 300; return item; }
    void Fire(Item item) => item.ModItem.Shoot(player, new EntitySource_ItemUse_WithAmmo(player, item, 0), player.Center, Vector2.UnitX * 18, 0, item.damage, item.knockBack);
    NPC Enemy(int x, int y) { NPC n = Main.npc[NPC.NewNPC(new EntitySource_Misc("Task3"), (int)player.Center.X + x, (int)player.Center.Y + y, NPCID.Zombie)]; n.lifeMax = n.life = 100000; n.velocity = Vector2.Zero; return n; }
    Projectile Spawn(string name, int mode) { Check(content.TryFind(name, out ModProjectile p), name + " missing"); return Main.projectile[Projectile.NewProjectile(player.GetSource_ItemUse(player.HeldItem), player.Center, Vector2.UnitX * 18, p.Type, 7200, 0, 0, mode, 0, 0)]; }
    Projectile[] Shots(string name) { Check(content.TryFind(name, out ModProjectile p), name + " missing"); return Main.projectile.Where(x => x.active && x.type == p.Type).ToArray(); }
    static void Clear() { foreach (Projectile p in Main.projectile) p.active = false; foreach (NPC n in Main.npc) n.active = false; }
    static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); }
}
