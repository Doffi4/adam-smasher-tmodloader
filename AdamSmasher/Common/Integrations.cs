using System;
using System.Collections.Generic;
using AdamSmasherMod.Content.Items;
using AdamSmasherMod.Content.NPCs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace AdamSmasherMod.Common;

public class Integrations : ModSystem
{
    // Checklist ordering only; this does not express lore or a measured difficulty ranking.
    public const float ChecklistProgression = 25f;

    public override void PostSetupContent()
    {
        if (!ModLoader.TryGetMod("BossChecklist", out Mod checklist)) return;
        var extra = new Dictionary<string, object> {
            ["spawnItems"] = ModContent.ItemType<ArasakaBeacon>(),
            ["spawnInfo"] = Mod.GetLocalization("BossChecklist.SpawnInfo"),
            ["customPortrait"] = (Action<SpriteBatch, Rectangle, Color>)DrawPortrait
        };
        object result = checklist.Call("LogBoss", Mod, "AdamSmasher", ChecklistProgression,
            (Func<bool>)(() => SmasherWorld.Downed), ModContent.NPCType<AdamSmasherBoss>(), extra);
        if (result is not string status || status != "Success")
            Mod.Logger.Warn($"BossChecklist registration returned {result ?? "null"}; expected LogBoss API success.");
    }

    private static void DrawPortrait(SpriteBatch batch, Rectangle area, Color color)
    {
        Texture2D texture = ModContent.Request<Texture2D>("AdamSmasher/Content/NPCs/AdamSmasherPortrait").Value;
        float scale = Math.Min(area.Width / (float)texture.Width, area.Height / (float)texture.Height);
        batch.Draw(texture, new Vector2(area.Center.X, area.Center.Y), null, color, 0f,
            new Vector2(texture.Width, texture.Height) / 2f, scale, SpriteEffects.None, 0f);
    }
}
