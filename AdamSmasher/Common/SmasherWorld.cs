using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AdamSmasherMod.Common;

public class SmasherWorld : ModSystem
{
    public static bool Downed;

    public override void ClearWorld() => Downed = false;

    public override void SaveWorldData(TagCompound tag)
    {
        if (Downed)
            tag["downedAdamSmasher"] = true;
    }

    public override void LoadWorldData(TagCompound tag) => Downed = tag.GetBool("downedAdamSmasher");
    public override void NetSend(BinaryWriter writer) => writer.Write(Downed);
    public override void NetReceive(BinaryReader reader) => Downed = reader.ReadBoolean();

    public static void MarkDowned()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        Downed = true;
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.WorldData);
    }
}
