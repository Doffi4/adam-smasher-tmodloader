using System.IO;
using AdamSmasherMod.Content.Items;
using AdamSmasherMod.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AdamSmasherMod;

public class AdamSmasher : Mod
{
    public const byte SummonRequest = 1;

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        // The network layer supplies whoAmI. No player index is accepted from the packet.
        if (Main.netMode != NetmodeID.Server || reader.BaseStream.Position == reader.BaseStream.Length)
            return;
        if (reader.ReadByte() == SummonRequest)
            TrySummon(whoAmI);
    }

    public static bool CanSummon(Player player)
    {
        if (!player.active || player.dead || player.HeldItem.type != ModContent.ItemType<ArasakaBeacon>())
            return false;
        foreach (NPC npc in Main.ActiveNPCs)
            if (npc.boss)
                return false;
        return true;
    }

    public static bool TrySummon(int playerIndex)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || playerIndex < 0 || playerIndex >= Main.maxPlayers)
            return false;
        if (!CanSummon(Main.player[playerIndex]))
            return false;
        NPC.SpawnOnPlayer(playerIndex, ModContent.NPCType<AdamSmasherBoss>());
        return true;
    }
}
