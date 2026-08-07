using UnityEngine;

namespace Overdraft.Core
{
    /// <summary>
    /// The only place colours are defined (docs/04 §1).
    /// Cyan = you, red = death, gold = money, white = safe. Four colours, learnable in three
    /// seconds, language-independent.
    /// <para>
    /// GOLD APPEARS ON EXACTLY ONE THING: the CHARGE number. If gold ever shows up on a second
    /// element, the CHARGE number stops being the loudest thing on screen and the compulsion
    /// loop weakens. This is enforced by review, not by code.
    /// </para>
    /// </summary>
    public static class Palette
    {
        public static readonly Color Void = Hex("07070C");
        public static readonly Color Grid = Hex("141C2E");
        public static readonly Color Ion = Hex("3BF4FF");
        public static readonly Color Bank = Hex("FFC53B");
        public static readonly Color Threat = Hex("FF3B5C");
        public static readonly Color Splitter = Hex("FF7A3B");
        public static readonly Color Hazard = Hex("B03BFF");
        public static readonly Color Banked = Hex("FFFFFF");
        public static readonly Color Warn = Hex("FF3BE1");

        /// <summary>Multiplies a colour's RGB while leaving alpha alone, for HDR emission.</summary>
        public static Color Intensity(Color c, float multiplier)
        {
            return new Color(c.r * multiplier, c.g * multiplier, c.b * multiplier, c.a);
        }

        static Color Hex(string rrggbb)
        {
            // Parsed rather than stored as floats so the constants above stay eyeball-checkable
            // against the art spec. Runs once per field at type initialisation.
            int r = System.Convert.ToInt32(rrggbb.Substring(0, 2), 16);
            int g = System.Convert.ToInt32(rrggbb.Substring(2, 2), 16);
            int b = System.Convert.ToInt32(rrggbb.Substring(4, 2), 16);
            const float inv = 1f / 255f;
            return new Color(r * inv, g * inv, b * inv, 1f);
        }
    }
}
