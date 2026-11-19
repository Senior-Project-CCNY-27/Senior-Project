using System;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [Serializable]
    public struct AxisToggles {
        public bool x, y, z;

        public AxisToggles(bool x, bool y, bool z) {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }
}
