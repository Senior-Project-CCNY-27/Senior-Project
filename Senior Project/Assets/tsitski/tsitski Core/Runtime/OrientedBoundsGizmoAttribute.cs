using System;

namespace Tsitski.Core {
    [AttributeUsage(AttributeTargets.Field)]
    public class OrientedBoundsGizmoAttribute : Attribute {
        public readonly bool local;

        public OrientedBoundsGizmoAttribute(bool local = false) {
            this.local = local;
        }
    }
}
