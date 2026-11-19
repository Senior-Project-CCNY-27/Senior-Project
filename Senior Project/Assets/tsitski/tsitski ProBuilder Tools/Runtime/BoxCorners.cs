using System;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [Serializable]
    public struct BoxCorners {
        public float topLeft, topRight, bottomLeft, bottomRight;

        public BoxCorners(float topLeft, float topRight, float bottomLeft, float bottomRight) {
            this.topLeft = topLeft;
            this.topRight = topRight;
            this.bottomLeft = bottomLeft;
            this.bottomRight = bottomRight;
        }

        public bool any => topLeft > 0f || topRight > 0f || bottomLeft > 0f || bottomRight > 0f;
    }
}
