// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osuTK;

namespace osu.Game.Rulesets.Mania.Configuration
{
    /// <summary>
    /// Touch rectangles in normalised screen coordinates, independent of the note columns.
    /// Layouts are stored separately for each key count and orientation.
    /// </summary>
    public class ManiaTouchLayout
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        [JsonIgnore]
        public Vector2 Position => new Vector2(X, Y);

        [JsonIgnore]
        public Vector2 Size => new Vector2(Width, Height);

        [JsonIgnore]
        public bool IsValid => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Width) && float.IsFinite(Height)
                               && X >= 0 && Y >= 0 && Width >= 0.025f && Height >= 0.025f
                               && X + Width <= 1.0001f && Y + Height <= 1.0001f;

        public static string Key(int count, bool portrait) => $"{count}-{(portrait ? "portrait" : "landscape")}";

        public static Dictionary<string, ManiaTouchLayout[]> Read(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, ManiaTouchLayout[]>>(json) ?? new Dictionary<string, ManiaTouchLayout[]>();
            }
            catch (JsonException)
            {
                return new Dictionary<string, ManiaTouchLayout[]>();
            }
        }

        public static ManiaTouchLayout[]? Get(string json, int count, bool portrait)
        {
            if (!Read(json).TryGetValue(Key(count, portrait), out var zones) || zones == null || zones.Length != count
                || zones.Any(z => z == null || !z.IsValid))
                return null;

            return zones;
        }

        public static ManiaTouchLayout[] CreateDefault(int count) => Enumerable.Range(0, count).Select(i => new ManiaTouchLayout
        {
            X = (float)i / count + 0.005f,
            Y = 0.5f,
            Width = 1f / count - 0.01f,
            Height = 0.5f,
        }).ToArray();

        public void Move(Vector2 position, bool snap)
        {
            if (snap)
                position = new Vector2(MathF.Round(position.X * 20) / 20, MathF.Round(position.Y * 20) / 20);

            X = Math.Clamp(position.X, 0, 1 - Width);
            Y = Math.Clamp(position.Y, 0, 1 - Height);
        }
    }
}
