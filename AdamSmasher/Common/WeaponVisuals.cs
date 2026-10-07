using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace AdamSmasherMod.Common;

// Local drawing/particles only; no SpriteBatch state changes or gameplay RNG.
internal static class WeaponVisuals
{
    internal static readonly Color RedGlow = new(255, 45, 32, 0);
    internal static readonly Color WhiteGlow = new(255, 225, 215, 0);

    internal static bool Visible(Vector2 center, float padding = 320) => !Main.dedServ &&
        center.X >= Main.screenPosition.X - padding && center.X <= Main.screenPosition.X + Main.screenWidth + padding &&
        center.Y >= Main.screenPosition.Y - padding && center.Y <= Main.screenPosition.Y + Main.screenHeight + padding;

    internal static void Line(Vector2 start, Vector2 end, Color color, float width)
    {
        if (Main.dedServ) return;
        Vector2 delta = end - start;
        if (delta.LengthSquared() < 0.01f) return;
        // MagicPixel is a 1x1000 strip, not a 1x1 texture. Scale only its white pixel.
        Main.EntitySpriteDraw(TextureAssets.MagicPixel.Value, start - Main.screenPosition, new Rectangle(0, 0, 1, 1), color,
            delta.ToRotation(), new Vector2(0, 0.5f), new Vector2(delta.Length(), width), SpriteEffects.None);
    }

    internal static void Arc(Vector2 center, float radius, float rotation, float sweep, Color color, float width, int segments = 8)
    {
        if (Main.dedServ) return;
        Vector2 previous = center + rotation.ToRotationVector2() * radius;
        for (int i = 1; i <= segments; i++)
        {
            Vector2 next = center + (rotation + sweep * i / segments).ToRotationVector2() * radius;
            Line(previous, next, color, width);
            previous = next;
        }
    }

    internal static void Spark(Vector2 center, float angle, float length, float opacity)
    {
        Vector2 axis = angle.ToRotationVector2() * length;
        Line(center - axis, center + axis, RedGlow * opacity, 3);
        Line(center - axis * 0.65f, center + axis * 0.65f, WhiteGlow * opacity, 1);
    }

    internal static void Trail(Projectile projectile, int age, float width)
    {
        if (!Visible(projectile.Center)) return;
        int count = Math.Min(age, projectile.oldPos.Length);
        Vector2 newer = projectile.Center;
        for (int i = 1; i < count; i++)
        {
            Vector2 oldPosition = projectile.oldPos[i];
            if (oldPosition == Vector2.Zero) break;
            Vector2 older = oldPosition + projectile.Size / 2;
            // A remote correction/teleport must not leave a screen-spanning beam.
            if (Vector2.DistanceSquared(newer, older) > 96 * 96) break;
            float fade = 1 - i / (float)count;
            Line(older, newer, RedGlow * (fade * 0.48f), width * fade);
            Line(older, newer, WhiteGlow * (fade * 0.65f), Math.Max(1, width * fade * 0.24f));
            newer = older;
        }
    }

    internal static void Particle(Vector2 position, Vector2 velocity, float scale = 0.9f)
    {
        if (!Visible(position)) return;
        Dust dust = Dust.NewDustPerfect(position, DustID.RedTorch, velocity, 0, Color.White, scale);
        dust.noGravity = true;
        dust.noLight = true;
    }

    internal static void Burst(Vector2 center, int count, float speed, float radius = 0, float phase = 0)
    {
        if (!Visible(center)) return;
        for (int i = 0; i < count; i++)
        {
            Vector2 direction = (phase + MathHelper.TwoPi * i / count).ToRotationVector2();
            Particle(center + direction * radius, direction * speed * (0.75f + 0.25f * MathF.Sin(i * 2.4f)), 1.1f);
        }
    }
}
