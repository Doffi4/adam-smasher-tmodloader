using System;
using System.IO;
using System.Linq;
using AdamSmasherMod.Common;
using AdamSmasherMod.Content.Items;
using AdamSmasherMod.Content.NPCs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.DataStructures;
using SmasherMod = AdamSmasherMod.AdamSmasher;

namespace Task1HarnessMod;

public class Task1Harness : Mod { }

public class Task1Checks : ModSystem
{
    private bool ran;

    public override void PostWorldLoad()
    {
        ran = false;
        RunChecks();
    }

    private void RunChecks()
    {
        string root = Environment.GetEnvironmentVariable("ADAM_SMASHER_LAB_ROOT");
        if (ran || !Main.dedServ || string.IsNullOrEmpty(root))
            return;
        ran = true;
        string profile = Path.Combine(root, "lab", "profile") + Path.DirectorySeparatorChar;
        string result = Path.Combine(root, "verification", "task1-runtime.txt");
        try
        {
            Check(Path.GetFullPath(Main.worldPathName).StartsWith(profile, StringComparison.OrdinalIgnoreCase), "World is isolated");
            Check(ModLoader.HasMod("CalamityMod"), "Calamity loaded");
            Check(ModLoader.HasMod("AdamSmasher"), "AdamSmasher loaded");
            if (File.Exists(result))
            {
                Check(SmasherWorld.Downed, "Downed restored from real .twld on second server launch");
                File.AppendAllText(result, "PASS: Downed persisted across server restart\n");
            }
            else
            {
                Check(!SmasherWorld.Downed, "New world starts undefeated");
                // Dedicated-server slots predate mod loading; create a full player with ModPlayers initialized.
                Player player = Main.player[0] = new Player { whoAmI = 0 };
                player.active = true;
                player.dead = false;
                player.statLife = player.statLifeMax2 = 500;
                player.Center = new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16);
                int type = ModContent.NPCType<AdamSmasherBoss>();
                Check(!NPCID.Sets.MPAllowedEnemies[type], "Vanilla request cannot bypass the custom server gate");
                SendSummonRequest(0);
                Check(!NPC.AnyNPCs(type), "Player without a held beacon cannot summon");
                player.inventory[player.selectedItem].SetDefaults(ModContent.ItemType<ArasakaBeacon>());
                foreach (int sender in new[] { -1, Main.maxPlayers, 1 })
                    SendSummonRequest(sender);
                player.dead = true;
                SendSummonRequest(0);
                player.dead = false;
                Check(!NPC.AnyNPCs(type), "Invalid/inactive/dead sender cannot summon");
                int otherBossIndex = NPC.NewNPC(new EntitySource_Misc("Task1ServerGate"), (int)player.Center.X, (int)player.Center.Y, NPCID.EyeofCthulhu);
                Check(Main.npc[otherBossIndex].boss, "Negative test has another active boss");
                SendSummonRequest(0);
                Check(!NPC.AnyNPCs(type), "Server packet refuses summon during another boss fight");
                Main.npc[otherBossIndex].active = false;
                int oldMode = Main.netMode;
                int oldPlayer = Main.myPlayer;
                // Exercise the actual summon item's single-player branch inside the isolated server world.
                Main.netMode = NetmodeID.SinglePlayer;
                Main.myPlayer = 0;
                ArasakaBeacon beacon = ModContent.GetInstance<ArasakaBeacon>();
                try
                {
                    Check(beacon.CanUseItem(player), "Beacon can summon in clear world");
                    beacon.UseItem(player);
                }
                finally { Main.netMode = oldMode; Main.myPlayer = oldPlayer; }
                NPC boss = Main.npc.Single(n => n.active && n.type == type);
                Check(!beacon.CanUseItem(player), "Second summon blocked");
                SendSummonRequest(0);
                Check(Main.npc.Count(n => n.active && n.type == type) == 1, "Duplicate packet cannot summon a second boss");
                boss.ModNPC.AI();
                Check(float.IsFinite(boss.velocity.X) && float.IsFinite(boss.velocity.Y), "Boss AI updates");
                boss.StrikeInstantKill();
                Check(SmasherWorld.Downed && !boss.active, "Real NPC death records victory");
                SendSummonRequest(0);
                NPC serverSpawn = Main.npc.Single(n => n.active && n.type == type);
                Check(serverSpawn.target == 0, "Server packet spawns on its actual sender");
                serverSpawn.StrikeInstantKill();
                SmasherWorld system = ModContent.GetInstance<SmasherWorld>();
                using MemoryStream data = new();
                using (BinaryWriter writer = new(data, System.Text.Encoding.UTF8, true)) system.NetSend(writer);
                SmasherWorld.Downed = false;
                data.Position = 0;
                using (BinaryReader reader = new(data, System.Text.Encoding.UTF8, true)) system.NetReceive(reader);
                Check(SmasherWorld.Downed, "World network round-trip");
                player.active = false;
                WorldFile.SaveWorld();
                File.WriteAllText(result, "PASS: load, summon item, duplicate prevention, NPC AI, actual death, network round-trip, world save\nPASS: server summon gate rejects another boss, duplicate, missing held beacon and invalid/inactive/dead sender; actual sender spawns successfully\n");
            }
            Mod.Logger.Info("TASK1 HARNESS PASS");
            Environment.Exit(0);
        }
        catch (Exception e)
        {
            Mod.Logger.Error("TASK1 HARNESS FAIL", e);
            File.AppendAllText(result, "FAIL: " + e + "\n");
            Environment.Exit(2);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void SendSummonRequest(int sender)
    {
        using MemoryStream payload = new(new[] { SmasherMod.SummonRequest });
        using BinaryReader reader = new(payload);
        ModContent.GetInstance<SmasherMod>().HandlePacket(reader, sender);
    }
}
