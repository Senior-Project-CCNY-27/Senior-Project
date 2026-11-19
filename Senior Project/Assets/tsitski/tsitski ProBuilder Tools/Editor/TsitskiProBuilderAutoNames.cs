using Tsitski.Core;
using UnityEditor;

namespace Tsitski.ProBuilderTools {
    [InitializeOnLoad]
    internal static class TsitskiProBuilderAutoNames {
        static TsitskiProBuilderAutoNames() => TsitskiSettingsWindow.NamingAllGenerators += NameAll;

        private static void NameAll() {
            foreach (TsitskiProBuilderCutter cutter in UnityCompat.FindAll<TsitskiProBuilderCutter>())
                TsitskiSettings.AutoName(cutter, cutter.stats, "ProBuilder Cutter");
            foreach (TsitskiProBuilderTrim trim in UnityCompat.FindAll<TsitskiProBuilderTrim>())
                TsitskiSettings.AutoName(trim, trim.stats, "ProBuilder Trim");
            foreach (TsitskiProBuilderStairs stairs in UnityCompat.FindAll<TsitskiProBuilderStairs>())
                TsitskiSettings.AutoName(stairs, stairs.stats, "ProBuilder Stairs");
            foreach (TsitskiProBuilderPillar pillar in UnityCompat.FindAll<TsitskiProBuilderPillar>())
                TsitskiSettings.AutoName(pillar, pillar.stats, "ProBuilder Pillar");
            foreach (TsitskiProBuilderFloorPlan plan in UnityCompat.FindAll<TsitskiProBuilderFloorPlan>())
                TsitskiSettings.AutoName(plan, plan.stats, "ProBuilder Floor Plan");
            foreach (TsitskiProBuilderRoof roof in UnityCompat.FindAll<TsitskiProBuilderRoof>())
                TsitskiSettings.AutoName(roof, roof.stats, "ProBuilder Roof");
            foreach (TsitskiProBuilderPolygon polygon in UnityCompat.FindAll<TsitskiProBuilderPolygon>())
                TsitskiSettings.AutoName(polygon, polygon.stats, "ProBuilder Polygon");
            foreach (TsitskiProBuilderPath path in UnityCompat.FindAll<TsitskiProBuilderPath>())
                TsitskiSettings.AutoName(path, path.stats, "ProBuilder Path");
        }
    }
}
