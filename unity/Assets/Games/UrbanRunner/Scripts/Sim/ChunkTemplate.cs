using System;
using System.Collections.Generic;
using UnityEngine;

namespace UrbanRunner.Sim
{
    /// <summary>
    /// A hand-authored chunk of rows (HANDOVER §3.4). Each row is a 3-character lane pattern:
    ///   '.' empty · 'J' crate (jump) · 'S' banner (slide) · 'B' stall · 'V' van · 'C' rolling cart
    /// and a gap in beats before the row (1 beat = <see cref="TrackRules.BeatS"/> × speed).
    /// Sparks are placed by the generator in the gaps; a row ending in '*' asks for a spark line in its gap.
    /// </summary>
    [CreateAssetMenu(menuName = "Urban Runner/Chunk Template")]
    public sealed class ChunkTemplate : ScriptableObject
    {
        [Range(1, 3)] public int tier = 1;
        [Tooltip("Relative chance of being picked among chunks of the same tier.")]
        [Min(0.1f)] public float weight = 1f;
        public List<Row> rows = new List<Row>();

        [Serializable]
        public struct Row
        {
            [Tooltip("3 chars: . J S B V C")] public string lanes;
            [Range(1, 4)] public int gapBeats;
            public bool sparks;
        }

        public static Obstacle Parse(char c)
        {
            switch (c)
            {
                case '.': return Obstacle.None;
                case 'J': return Obstacle.Crate;
                case 'S': return Obstacle.Banner;
                case 'B': return Obstacle.Stall;
                case 'V': return Obstacle.Van;
                case 'C': return Obstacle.Cart;
                default: throw new FormatException("unknown lane char '" + c + "'");
            }
        }

        /// <summary>Authoring checks: pattern syntax, a passable lane per row, carts only from tier 3.</summary>
        public IEnumerable<string> Validate()
        {
            if (rows == null || rows.Count == 0) yield return name + ": no rows";
            if (rows == null) yield break;
            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.lanes == null || r.lanes.Length != TrackRules.LaneCount)
                {
                    yield return $"{name} row {i}: pattern must be {TrackRules.LaneCount} chars";
                    continue;
                }
                var passable = false;
                for (var l = 0; l < r.lanes.Length; l++)
                {
                    Obstacle o;
                    string error = null;
                    try { o = Parse(r.lanes[l]); }
                    catch (FormatException e) { error = e.Message; o = Obstacle.None; }
                    if (error != null) { yield return $"{name} row {i}: {error}"; continue; }
                    if (TrackRules.IsPassable(o)) passable = true;
                    if (o == Obstacle.Cart && tier < 3) yield return $"{name} row {i}: carts are tier 3+";
                }
                if (!passable) yield return $"{name} row {i}: no passable lane";
                if (r.gapBeats < 1) yield return $"{name} row {i}: gapBeats must be ≥ 1";
            }
        }
    }
}
