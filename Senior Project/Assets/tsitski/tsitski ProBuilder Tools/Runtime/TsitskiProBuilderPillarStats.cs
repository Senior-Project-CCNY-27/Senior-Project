using System;
using System.Collections.Generic;
using UnityEngine;
using Tsitski.Core;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.ProBuilderTools {
    [CreateAssetMenu(menuName = "tsitski ProBuilder Tools/Pillar Stats", fileName = "Pillar Stats")]
    public class TsitskiProBuilderPillarStats : ScriptableObject {
        [Header("Shape")]
        [Tooltip(Tooltips.PillarWidth)] [Min(0.001f)] public float width = 0.3f;
        [Tooltip(Tooltips.PillarShape)] public TsitskiProBuilderPillar.Shape shape;

        [Header("Placement")]
        [Tooltip(Tooltips.AutoSnapToPath)] public bool autoSnapToPath = true;
        [Tooltip(Tooltips.SnapToFlats)] public bool snapToFlats;
        [Tooltip(Tooltips.GenerateDownwards)] public bool generateDownwards;

        [Header("Corner")]
        [Tooltip(Tooltips.CornerLength)] [Min(TsitskiProBuilderPillar.MinCornerLength)] public float cornerLength = 0.5f;
        [Tooltip(Tooltips.PillarRoundBevel)] [Min(0)] public int roundBevel;

        [Header("Profile")]
        [Tooltip(Tooltips.BottomProfile)] public PillarProfile bottomProfile = PillarProfile.DefaultBottom();
        [Tooltip(Tooltips.BottomHeight)] [Min(0f)] public float bottomHeight = TsitskiProBuilderPillar.DefaultBottomHeight;
        [Tooltip(Tooltips.CenterProfile)] public PillarProfile centerProfile = PillarProfile.DefaultCenter();
        [Tooltip(Tooltips.StretchCenter)] public bool stretchCenter;
        [Tooltip(Tooltips.CenterHeight)] [Min(TsitskiProBuilderPillar.MinCenterHeight)] public float centerHeight = TsitskiProBuilderPillar.DefaultCenterHeight;
        [Tooltip(Tooltips.TopProfile)] public PillarProfile topProfile = PillarProfile.DefaultTop();
        [Tooltip(Tooltips.TopHeight)] [Min(0f)] public float topHeight = TsitskiProBuilderPillar.DefaultTopHeight;

        [Header("Mesh")]
        [Tooltip(Tooltips.PillarSides)] [Min(TsitskiProBuilderPillar.MinSides)] public int sides = 12;
        [Tooltip(Tooltips.RoundingSegments)] [Min(1)] public int roundingSegments = 8;
        [Tooltip(Tooltips.SmoothingAngle)] [Range(0f, 180f)] public float smoothingAngle = 40f;

        [SerializeField] [HideInInspector] [FormerlySerializedAs("profile")] private List<Vector2> legacyProfile = new();
        [SerializeField] [HideInInspector] [FormerlySerializedAs("profileRadii")] private List<float> legacyProfileRadii = new();
        [SerializeField] [HideInInspector] private float lastWidth = -1f;

        [Header("Material")]
        [Tooltip(Tooltips.GeneratorMaterial)] public Material material;

        public static event Action<TsitskiProBuilderPillarStats> Changed;

        public void NotifyChanged() {
            SyncWidth();
            Changed?.Invoke(this);
        }

        public void SyncWidth() => PillarProfile.SyncWidth(ref width, ref lastWidth, bottomProfile, centerProfile, topProfile);

        private void OnEnable() => MigrateLegacyProfile();

        private void OnValidate() {
            MigrateLegacyProfile();
            bottomHeight = Mathf.Max(0f, bottomHeight);
            topHeight = Mathf.Max(0f, topHeight);
            centerHeight = Mathf.Max(TsitskiProBuilderPillar.MinCenterHeight, centerHeight);
            SyncWidth();
            Changed?.Invoke(this);
        }

        private void MigrateLegacyProfile() {
            if (legacyProfile is not { Count: >= 2 })
                return;

            if (PillarProfile.Split(legacyProfile, legacyProfileRadii, bottomHeight, topHeight,
                    out bottomProfile, out centerProfile, out topProfile, out bottomHeight, out float center, out topHeight))
                centerHeight = Mathf.Max(TsitskiProBuilderPillar.MinCenterHeight, center);

            legacyProfile = new List<Vector2>();
            legacyProfileRadii = new List<float>();
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }
}
