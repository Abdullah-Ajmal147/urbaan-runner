using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UrbanRunner.Sim;

namespace UrbanRunner.Editor
{
    /// <summary>
    /// Hand-authored chunk patterns. <see cref="EnsureAssets"/> turns them into ChunkTemplate assets once;
    /// after that the assets are the source of truth and can be tuned in the Inspector.
    /// Row syntax: "lanes/gapBeats[*]", e.g. "J.B/2*" = crate left, empty middle, stall right, 2 beats before it, sparks in the gap.
    /// </summary>
    public static class ChunkCatalog
    {
        public const string Folder = "Assets/Games/UrbanRunner/Data/Chunks";
        public const string LibraryPath = "Assets/Games/UrbanRunner/Data/ChunkLibrary.asset";

        static readonly (string name, int tier, float weight, string rows)[] Patterns =
        {
            // Tier 1 (0–500 m): single obstacles, one action at a time, generous gaps.
            ("t1_crate_center", 1, 1f, ".J./2*"),
            ("t1_banner_center", 1, 1f, ".S./2*"),
            ("t1_stall_left", 1, 1f, "B../2*"),
            ("t1_stall_right", 1, 1f, "..B/2*"),
            ("t1_crates_row", 1, 1f, "JJJ/2*"),
            ("t1_banners_row", 1, 1f, "SSS/2*"),
            ("t1_slalom", 1, 1f, "B../2 ..B/2*"),
            ("t1_gate", 1, 1f, "B.B/2*"),
            ("t1_mixed", 1, 1f, ".J./2 .S./2*"),
            ("t1_tram_ramp", 1, 1.2f, ".R./2*"),
            ("t1_tram_side", 1, 1f, "T.R/2*"),
            // Tier 2 (500–1,500 m): combinations and tighter gaps.
            ("t2_tram_line", 2, 1.2f, "TRT/2 .T./1*"),
            ("t2_roof_hop", 2, 1f, "R.T/2* .J./1"),
            ("t2_trams_banner", 2, 1f, "TSR/2*"),
            ("t2_van_crate", 2, 1f, "V.J/2* .B./1"),
            ("t2_zigzag", 2, 1f, "BB./2 .BB/1* B.B/1"),
            ("t2_jump_slide", 2, 1f, "JJJ/2 SSS/1*"),
            ("t2_van_lane", 2, 1f, "V../2* V.J/1 V../1"),
            ("t2_corridor", 2, 1f, "B.B/2 BSB/1 BJB/1*"),
            ("t2_choice", 2, 1f, "JBS/2*"),
            ("t2_vans", 2, 1f, ".VV/2 VV./1*"),
            ("t2_stalls_sparks", 2, 1f, "B.B/1* .B./1 B.B/1*"),
            ("t2_banner_gate", 2, 1f, "SBS/2* BJB/1"),
            // Tier 3 (1,500 m+): rolling carts and dense sequences.
            ("t3_cart_center", 3, 1f, ".C./2* C.C/1"),
            ("t3_carts_sides", 3, 1f, "C.C/2* .J./1"),
            ("t3_cart_van", 3, 1f, "CV./2 ..C/1*"),
            ("t3_gauntlet", 3, 1f, "BSB/2 JBC/1 SB./1*"),
            ("t3_triple_mix", 3, 1f, "JSC/2* CBJ/1"),
            ("t3_vans_cart", 3, 1f, "VV./2 .CV/1* B.J/1"),
            ("t3_tram_maze", 3, 1f, "RTT/2 T.C/1* .TR/1"),
            ("t3_long", 3, 0.7f, "B.C/1 .BB/1* J.V/1 SSB/1 .CB/1*"),
        };

        [MenuItem("GameFactory/Urban Runner/Regenerate Missing Chunk Assets")]
        public static void EnsureAssetsMenu() => EnsureAssets();

        /// <summary>Creates any missing chunk assets and (re)builds the library asset. Never overwrites a chunk.</summary>
        public static ChunkLibrary EnsureAssets()
        {
            Directory.CreateDirectory(Folder);
            var chunks = new List<ChunkTemplate>();
            foreach (var p in Patterns)
            {
                var path = $"{Folder}/{p.name}.asset";
                var chunk = AssetDatabase.LoadAssetAtPath<ChunkTemplate>(path);
                if (chunk == null)
                {
                    chunk = Build(p.name, p.tier, p.weight, p.rows);
                    AssetDatabase.CreateAsset(chunk, path);
                }
                chunks.Add(chunk);
            }
            // Also keep any extra chunks a designer added to the folder.
            foreach (var guid in AssetDatabase.FindAssets("t:ChunkTemplate", new[] { Folder }))
            {
                var c = AssetDatabase.LoadAssetAtPath<ChunkTemplate>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null && !chunks.Contains(c)) chunks.Add(c);
            }

            var lib = AssetDatabase.LoadAssetAtPath<ChunkLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<ChunkLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            lib.chunks = chunks.OrderBy(c => c.tier).ThenBy(c => c.name).ToList();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();

            var errors = lib.Validate().ToList();
            if (errors.Count > 0) throw new System.Exception("chunk library invalid:\n" + string.Join("\n", errors));
            return lib;
        }

        /// <summary>Builds a template in memory from the compact syntax (also used by tests).</summary>
        public static ChunkTemplate Build(string name, int tier, float weight, string rows)
        {
            var c = ScriptableObject.CreateInstance<ChunkTemplate>();
            c.name = name;
            c.tier = tier;
            c.weight = weight;
            foreach (var token in rows.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = token.Split('/');
                var sparks = parts[1].EndsWith("*");
                c.rows.Add(new ChunkTemplate.Row
                {
                    lanes = parts[0],
                    gapBeats = int.Parse(parts[1].TrimEnd('*')),
                    sparks = sparks,
                });
            }
            return c;
        }
    }
}
