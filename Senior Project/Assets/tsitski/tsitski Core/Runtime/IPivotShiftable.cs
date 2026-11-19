using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tsitski.Core {
    public interface IPivotShiftable {
        bool CanShiftPivot(out string reason);
        void ShiftLocalPoints(Vector3 offset);
        void ApplyPivotShift();
    }

    public static class PivotShift {
        public static List<Vector3> Shifted(List<Vector3> points, Vector3 offset) => points?.Select(p => p + offset).ToList();
    }
}
