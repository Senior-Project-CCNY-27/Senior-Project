using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(TsitskiProBuilderTrimStats))]
    public class TsitskiProBuilderTrimStatsEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            TsitskiInspector.DrawProperties(serializedObject);
            if (!serializedObject.isEditingMultipleObjects) {
                TsitskiProBuilderTrimStats stats = (TsitskiProBuilderTrimStats)target;
                ProfilePreview.Draw(TsitskiProBuilderTrim.BuildProfile(stats.profile, stats.profileRadii, stats.profileMirror, stats.roundingSegments),
                    true, stats.profile, edited => TsitskiProBuilderTrimEditor.ApplySnap(stats, edited),
                    new[] {
                        new GraphPoints {
                            points = stats.profile, apply = edited => TsitskiProBuilderTrimEditor.ApplySnap(stats, edited, "Edit Trim Profile"),
                            radii = stats.profileRadii, applyRadii = radii => TsitskiProBuilderTrimEditor.ApplyRadii(stats, radii)
                        }
                    });
            }
        }
    }
}
