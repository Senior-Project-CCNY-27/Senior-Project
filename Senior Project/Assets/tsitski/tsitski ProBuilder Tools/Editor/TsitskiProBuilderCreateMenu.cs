using Tsitski.Core;
using UnityEditor;

namespace Tsitski.ProBuilderTools {
    internal static class TsitskiProBuilderCreateMenu {
        private const string Root = "Tools/tsitski/ProBuilder/Create/";
        private const string GameObjectRoot = "GameObject/tsitski ProBuilder Tools/";

        [MenuItem(Root + "Polygon", false, 900)]
        [MenuItem(GameObjectRoot + "Polygon", false, 10)]
        private static void Polygon(MenuCommand command) => TsitskiCreateMenu.Create<TsitskiProBuilderPolygon>("ProBuilder Polygon", command);

        [MenuItem(Root + "Floor Plan", false, 901)]
        [MenuItem(GameObjectRoot + "Floor Plan", false, 11)]
        private static void FloorPlan(MenuCommand command) => TsitskiCreateMenu.Create<TsitskiProBuilderFloorPlan>("ProBuilder Floor Plan", command);

        [MenuItem(Root + "Roof", false, 902)]
        [MenuItem(GameObjectRoot + "Roof", false, 12)]
        private static void Roof(MenuCommand command) => TsitskiCreateMenu.Create<TsitskiProBuilderRoof>("ProBuilder Roof", command);

        [MenuItem(Root + "Trim", false, 903)]
        [MenuItem(GameObjectRoot + "Trim", false, 13)]
        private static void Trim(MenuCommand command) => TsitskiCreateMenu.Create<TsitskiProBuilderTrim>("ProBuilder Trim", command);

        [MenuItem(Root + "Stairs", false, 904)]
        [MenuItem(GameObjectRoot + "Stairs", false, 14)]
        private static void Stairs(MenuCommand command) => TsitskiCreateMenu.Create<TsitskiProBuilderStairs>("ProBuilder Stairs", command);

        [MenuItem(Root + "Pillar", false, 905)]
        [MenuItem(GameObjectRoot + "Pillar", false, 15)]
        private static void Pillar(MenuCommand command) => TsitskiCreateMenu.Create<TsitskiProBuilderPillar>("ProBuilder Pillar", command);

        [MenuItem(Root + "Cutter", false, 906)]
        [MenuItem(GameObjectRoot + "Cutter", false, 16)]
        private static void Cutter(MenuCommand command) => TsitskiCreateMenu.Create<TsitskiProBuilderCutter>("ProBuilder Cutter", command);

        [MenuItem(Root + "Path", false, 907)]
        [MenuItem(GameObjectRoot + "Path", false, 17)]
        private static void Path(MenuCommand command) => TsitskiCreateMenu.Create<TsitskiProBuilderPath>("ProBuilder Path", command);
    }
}
