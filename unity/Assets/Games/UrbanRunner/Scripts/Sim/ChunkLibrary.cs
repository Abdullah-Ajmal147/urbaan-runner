using System.Collections.Generic;
using UnityEngine;

namespace UrbanRunner.Sim
{
    /// <summary>All chunk templates the generator may use.</summary>
    [CreateAssetMenu(menuName = "Urban Runner/Chunk Library")]
    public sealed class ChunkLibrary : ScriptableObject
    {
        public List<ChunkTemplate> chunks = new List<ChunkTemplate>();

        public IEnumerable<string> Validate()
        {
            if (chunks.Count == 0) yield return "library is empty";
            var tiers = new bool[4];
            foreach (var c in chunks)
            {
                if (c == null) { yield return "null chunk"; continue; }
                tiers[c.tier] = true;
                foreach (var e in c.Validate()) yield return e;
            }
            for (var t = 1; t <= 3; t++)
                if (!tiers[t]) yield return "no chunks for tier " + t;
        }
    }
}
