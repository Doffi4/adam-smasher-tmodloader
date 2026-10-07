using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Terraria.GameInput;

namespace Task6Harness;
public class Task6Harness : Mod {
    public override void Load() {
        if (Environment.GetEnvironmentVariable("ADAM_SMASHER_LAB_MODE") != "load") On_Player.ManageSpecialBiomeVisuals += SuppressBiomeVisuals;
    }
    public override void Unload() => On_Player.ManageSpecialBiomeVisuals -= SuppressBiomeVisuals;
    static void SuppressBiomeVisuals(On_Player.orig_ManageSpecialBiomeVisuals orig, Player player, string biomeName, bool inZone, Vector2 activationSource) { }
}
public class BenchTarget : ModNPC
{
    public override string Texture => "Terraria/Images/NPC_1";
    public override void SetDefaults() {
        NPC.width = NPC.height = 80; NPC.lifeMax = 100000000; NPC.damage = NPC.defense = 0;
        NPC.noGravity = NPC.noTileCollide = true; NPC.knockBackResist = 0;
        NPC.friendly = false; NPC.chaseable = true; NPC.realLife = -1;
    }
    public override void AI() { NPC.velocity = Vector2.Zero; NPC.timeLeft = 1000; }
    public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone) => CombatBench.Hit(projectile, hit, damageDone);
}
public class BenchInput : ModPlayer
{
    public override void SetControls() { if (CombatBench.Active && Player.whoAmI == 0) { CombatBench.Controls++; Apply(); } }
    public override bool PreItemCheck() { if (CombatBench.Active && Player.whoAmI == 0) { CombatBench.ItemChecks++; Apply(); } return true; }
    void Apply() { Player.controlUseItem = true; Player.controlUseTile = false; Player.controlLeft = Player.controlRight = Player.controlJump = false; }
}
public class CombatBench : ModSystem
{
    public static bool Active;
    public static int Controls, ItemChecks;
    static long damage; static int hits;
    static readonly List<string> output = new();
    static readonly Dictionary<string,int> hitTypes = new();
    int age, mode, who; bool ready; uint previous; int target; Vector2 anchor;
    static string Root => Environment.GetEnvironmentVariable("ADAM_SMASHER_LAB_ROOT");
    public override void PostWorldLoad() {
        if (string.IsNullOrEmpty(Root)) return;
        if (Environment.GetEnvironmentVariable("ADAM_SMASHER_LAB_MODE") == "load") { LoadSmoke(); return; }
        try {
            Main.GameMode = 0; Main.zenithWorld = false;
            foreach (var npc in Main.npc) npc.active = false;
            foreach (var p in Main.projectile) p.active = false;
            foreach (var p in Main.player) p.active = false;
            anchor = new Vector2(Main.spawnTileX * 16, (Main.spawnTileY - 30) * 16);
            Main.player[0] = new Player { whoAmI = 0, active = true, Center = anchor, statLife = 500, statLifeMax = 500, statMana = 200, statManaMax = 200, name = "Bench" };
            Main.player[0].inventory[0] = new Item(ModLoader.GetMod("CalamityMod").Find<ModItem>("TheDanceofLight").Type);
            target = NPC.NewNPC(new EntitySource_Misc("Task6Combat"), (int)anchor.X+120, (int)anchor.Y, ModContent.NPCType<BenchTarget>());
            Main.npc[target].Center = anchor + new Vector2(120, 0);
            output.Add("POC: Dance of Light; no armor/accessories/buffs/prefix; mana200/no refill; Normal/non-zenith; target80x80 defense0 distance120; minimal Calamity+Music+AdamSmasher+Task6Harness");
            output.Add($"TARGET: chaseable={Main.npc[target].CanBeChasedBy()}, life={Main.npc[target].life}; dedServ={Main.dedServ}");
            ready = true;
            File.WriteAllLines(Path.Combine(Root,"verification/task6-combat-runtime.txt"),output);
            Main.OnTickForThirdPartySoftwareOnly += EngineTick;
        } catch (Exception e) { Finish("FAIL setup: "+e, 2); }
    }
    void LoadSmoke() {
        int oldMode=Main.netMode, oldPlayer=Main.myPlayer;
        var lines=new List<string>(); int exit=0;
        try {
            Main.netMode=NetmodeID.SinglePlayer; Main.myPlayer=0;
            Mod content=ModLoader.GetMod("AdamSmasher");
            lines.Add("LOADED: "+string.Join(", ",ModLoader.Mods.Select(x=>x.Name+"@"+x.Version)));
            foreach(var n in Main.npc) n.active=false;
            foreach(var p in Main.projectile) p.active=false;
            Player player=Main.player[0]=new Player { whoAmI=0,active=true,Center=new Vector2(Main.spawnTileX*16,Main.spawnTileY*16) };
            Item beacon=player.inventory[0]=new Item(content.Find<ModItem>("ArasakaBeacon").Type);
            if(beacon.consumable || !beacon.ModItem.CanUseItem(player) || beacon.ModItem.UseItem(player)!=true) throw new Exception("Summon failed");
            int bossType=content.Find<ModNPC>("AdamSmasherBoss").Type;
            NPC boss=Main.npc.First(x=>x.active&&x.type==bossType);
            if(beacon.ModItem.CanUseItem(player) || beacon.ModItem.UseItem(player)!=false) throw new Exception("Duplicate gate failed");
            lines.Add($"PASS: actual reusable summon + duplicate gate; boss life={boss.lifeMax} defense={boss.defense}; explicit headless resource load");
            for(int tick=0;tick<360;tick++) boss.UpdateNPC(boss.whoAmI);
            lines.Add("PASS: boss full NPC updates360; owned hostile projectiles="+Main.projectile.Count(x=>x.active&&x.ModProjectile?.Mod==content));
            player.active=false;
            for(int tick=0;tick<65;tick++) if(boss.active) boss.UpdateNPC(boss.whoAmI);
            if(boss.active || Main.projectile.Any(x=>x.active&&x.hostile&&x.ModProjectile?.Mod==content)) throw new Exception("Retire cleanup failed");
            player.active=true;
            int other=NPC.NewNPC(new EntitySource_Misc("Task6Smoke"),(int)player.Center.X+100,(int)player.Center.Y,NPCID.KingSlime);
            if(beacon.ModItem.CanUseItem(player)) throw new Exception("Other boss gate failed");
            Main.npc[other].active=false;
            if(Main.recipe.Take(Recipe.numRecipes).Count(x=>x.createItem.type==beacon.type)!=1) throw new Exception("Recipe registration failed");
            foreach(string name in new[]{"AnnihilationProtocol","ArasakaMantisBlades","SmasherBag"}) if(new Item(content.Find<ModItem>(name).Type).IsAir) throw new Exception("Missing item "+name);
            lines.Add("PASS: retire/projectile cleanup, other boss gate, exactly one beacon recipe, final weapons/bag defaults");
            lines.Add("NOTE: NPC Update smoke only; no Player combat tick or DPS/network/GPU claim");
        } catch(Exception e) { exit=2; lines.Add("FAIL: "+e); }
        finally { Main.netMode=oldMode; Main.myPlayer=oldPlayer; }
        string resultName=Environment.GetEnvironmentVariable("ADAM_SMASHER_FULLSET")=="1"?"task6-fullset-runtime.txt":"task6-smoke-runtime.txt";
        File.WriteAllLines(Path.Combine(Root,"verification",resultName),lines); Environment.Exit(exit);
    }
    void EngineTick() {
        if (!ready) return;
        try {
            if (age == 0) {
                Main.player[0] = new Player { whoAmI = 0, active = true, Center = anchor, statLife = 500, statLifeMax = 500, statMana = 200, statManaMax = 200, name = "Bench" };
                Main.player[0].inventory[0] = new Item(ModLoader.GetMod("CalamityMod").Find<ModItem>("TheDanceofLight").Type);
            }
            PreUpdateEntities();
            typeof(Main).GetMethod("DoUpdateInWorld", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Main.instance, new object[] { Stopwatch.StartNew() });
        } catch (Exception e) { Finish("FAIL full engine tick: "+e,2); }
        finally { Main.netMode=mode; Main.myPlayer=who; Active=false; }
    }
    public override void PreUpdateEntities() {
        if (!ready) return;
        mode = Main.netMode; who = Main.myPlayer;
        Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0; Main.ignoreErrors = false; Main.hasFocus = true; Main.blockInput = Main.blockMouse = Main.editChest = Main.editSign = false;
        Main.screenPosition = anchor - new Vector2(400,300); Main.mouseX = 520; Main.mouseY = 300;
        Main.player[0].active = true; Main.player[0].Center = anchor; Main.player[0].velocity = Vector2.Zero;
        previous = Main.GameUpdateCount; Active = true;
    }
    public override void PreUpdatePlayers() {
        if (ready && age == 0) output.Add($"PRE PLAYERS: active={Main.player[0].active}; myPlayer={Main.myPlayer}; netMode={Main.netMode}; dead={Main.player[0].dead}");
    }
    public override void PostUpdateEverything() {
        if (!ready) return;
        Active = false;
        Main.netMode = mode; Main.myPlayer = who;
        age++;
        if (Main.GameUpdateCount != previous+1) { Finish("FAIL natural clock",2); return; }
        if (age == 1 || age%60 == 0) {
            Player p = Main.player[0];
            output.Add($"TICK {age}: clock={Main.GameUpdateCount}; controls={Controls}; itemChecks={ItemChecks}; itemTime={p.itemTime}; animation={p.itemAnimation}; channel={p.channel}; mana={p.statMana}; activeProj={Main.projectile.Count(x=>x.active)}; hits={hits}; damage={damage}; lifeDelta={Main.npc[target].lifeMax-Main.npc[target].life}");
            File.WriteAllLines(Path.Combine(Root,"verification/task6-combat-runtime.txt"),output);
        }
        if (age >= 300) Finish(hits>0 && damage==Main.npc[target].lifeMax-Main.npc[target].life && Controls>=300 && ItemChecks>=300 ? "PASS actual natural-loop collision + strike + hit hook + HP delta" : "FAIL incomplete natural hit pipeline", hits>0 && damage==Main.npc[target].lifeMax-Main.npc[target].life && Controls>=300 && ItemChecks>=300 ? 0 : 2);
    }
    public static void Hit(Projectile p, NPC.HitInfo hit, int done) {
        if (!Active || p.owner != 0) return;
        hits++; damage+=done;
        string key = p.ModProjectile?.FullName ?? p.type.ToString(); hitTypes[key] = hitTypes.GetValueOrDefault(key)+1;
        if (hits<=12) output.Add($"HIT: type={key}; damageDone={done}; crit={hit.Crit}; clock={Main.GameUpdateCount}");
    }
    void Finish(string message,int exit) {
        Active=false; ready=false; Main.netMode=NetmodeID.Server; Main.myPlayer=255;
        output.Add(message); output.Add("HIT TYPES: "+string.Join(", ",hitTypes.Select(x=>$"{x.Key}={x.Value}")));
        File.WriteAllLines(Path.Combine(Root,"verification/task6-combat-runtime.txt"),output);
        Environment.Exit(exit);
    }
}
