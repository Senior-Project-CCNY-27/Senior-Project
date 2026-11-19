using System;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CreateAssetMenu(menuName = "tsitski ProBuilder Tools/Stairs Stats", fileName = "Stairs Stats")]
    public class TsitskiProBuilderStairsStats : ScriptableObject {
        [Header("Steps")]
        [Tooltip(Tooltips.StairsWidth)] [Min(TsitskiProBuilderStairs.MinSize)] public float width = 1.2f;
        [Tooltip(Tooltips.StepHeight)] [Min(TsitskiProBuilderStairs.MinSize)] public float stepHeight = 0.18f;
        [Tooltip(Tooltips.Solid)] public bool solid = true;
        [Tooltip(Tooltips.StairsThickness)] [Min(TsitskiProBuilderStairs.MinSize)] public float thickness = 0.15f;

        [Header("Cutter")]
        [Tooltip(Tooltips.GenerateCutter)] public bool generateCutter;
        [Tooltip(Tooltips.CutterHeadroom)] [Min(TsitskiProBuilderStairs.MinSize)] public float cutterHeadroom = 2.1f;

        [Header("Trims")]
        [Tooltip(Tooltips.LeftTrim)] public TsitskiProBuilderTrimStats leftTrim;
        [Tooltip(Tooltips.RightTrim)] public TsitskiProBuilderTrimStats rightTrim;
        [Tooltip(Tooltips.BottomTrimForwardOffset)] public float bottomTrimForwardOffset;
        [Tooltip(Tooltips.BottomTrimUpwardOffset)] public float bottomTrimUpwardOffset;
        [Tooltip(Tooltips.TopTrimForwardOffset)] public float topTrimForwardOffset;
        [Tooltip(Tooltips.TopTrimUpwardOffset)] public float topTrimUpwardOffset;

        [Header("Collider")]
        [Tooltip(Tooltips.SmoothCollider)] public bool smoothCollider;

        [Header("Materials")]
        [Tooltip(Tooltips.StairsTopStepMaterial)] [UnityEngine.Serialization.FormerlySerializedAs("topMaterial")] public Material topStepMaterial;
        [Tooltip(Tooltips.StairsForwardStepMaterial)] public Material forwardStepMaterial;
        [Tooltip(Tooltips.StairsSideMaterial)] [UnityEngine.Serialization.FormerlySerializedAs("bottomMaterial")] public Material sideMaterial;

        public static event Action<TsitskiProBuilderStairsStats> Changed;

        public void NotifyChanged() => Changed?.Invoke(this);

        private void OnValidate() => Changed?.Invoke(this);
    }
}
