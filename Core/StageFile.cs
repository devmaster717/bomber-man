using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BombArena.Core
{
    /// <summary>
    /// Reads and writes stage data files: one "key = value" per line, '#' starts a comment. Example:
    /// <code>
    /// width = 21
    /// height = 11
    /// seed = 81234
    /// softBlocks = 60
    /// enemies = Walker:4, WallPasser:1
    /// targetSeconds = 120
    /// runnersFromExit = 3
    /// </code>
    /// </summary>
    public static class StageFile
    {
        public static StageSpec Parse(string text, int number)
        {
            var spec = new StageSpec { Number = number };
            int lineNo = 0;
            foreach (var raw in text.Split('\n'))
            {
                lineNo++;
                var line = raw.Trim();
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash).Trim();
                if (line.Length == 0) continue;

                int eq = line.IndexOf('=');
                if (eq < 0) throw new FormatException($"Stage {number} line {lineNo}: expected 'key = value'");
                var key = line.Substring(0, eq).Trim();
                var value = line.Substring(eq + 1).Trim();
                switch (key)
                {
                    case "width": spec.Width = Int(value, key, number); break;
                    case "height": spec.Height = Int(value, key, number); break;
                    case "seed": spec.Seed = ulong.Parse(value, CultureInfo.InvariantCulture); break;
                    case "softBlocks": spec.SoftBlockPercent = Int(value, key, number); break;
                    case "targetSeconds": spec.TargetSeconds = Int(value, key, number); break;
                    case "runnersFromExit": spec.RunnersFromExit = Int(value, key, number); break;
                    case "enemies": spec.Enemies = Enemies(value, number); break;
                    default: throw new FormatException($"Stage {number} line {lineNo}: unknown key '{key}'");
                }
            }

            if (!Arena.IsValidSize(spec.Width, spec.Height))
                throw new FormatException($"Stage {number}: invalid arena size {spec.Width} x {spec.Height}");
            return spec;
        }

        public static string Format(StageSpec spec)
        {
            var sb = new StringBuilder();
            sb.Append("# Stage ").Append(spec.Number).Append('\n');
            sb.Append("width = ").Append(spec.Width).Append('\n');
            sb.Append("height = ").Append(spec.Height).Append('\n');
            sb.Append("seed = ").Append(spec.Seed.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("softBlocks = ").Append(spec.SoftBlockPercent).Append('\n');
            sb.Append("enemies = ");
            for (int i = 0; i < spec.Enemies.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(spec.Enemies[i].Kind).Append(':').Append(spec.Enemies[i].Count);
            }
            sb.Append('\n');
            sb.Append("targetSeconds = ").Append(spec.TargetSeconds).Append('\n');
            sb.Append("runnersFromExit = ").Append(spec.RunnersFromExit).Append('\n');
            return sb.ToString();
        }

        /// <summary>File name for a stage: stage-001 … stage-100.</summary>
        public static string FileName(int number) => $"stage-{number:000}";

        private static int Int(string value, string key, int number) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                ? v
                : throw new FormatException($"Stage {number}: '{key}' must be a whole number, got '{value}'");

        private static List<EnemyGroup> Enemies(string value, int number)
        {
            var groups = new List<EnemyGroup>();
            foreach (var part in value.Split(','))
            {
                var p = part.Trim();
                if (p.Length == 0) continue;
                var bits = p.Split(':');
                if (bits.Length != 2 || !Enum.TryParse(bits[0].Trim(), out EnemyKind kind))
                    throw new FormatException($"Stage {number}: bad enemy entry '{p}' (use Kind:Count, e.g. Walker:3)");
                groups.Add(new EnemyGroup(kind, Int(bits[1].Trim(), "enemies", number)));
            }
            return groups;
        }
    }
}
