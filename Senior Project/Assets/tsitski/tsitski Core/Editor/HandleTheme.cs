using UnityEngine;

namespace Tsitski.Core {
    public static class HandleTheme {
        public static readonly Color Path = new(1f, 0.4f, 0.1f, 1f);
        public static readonly Color Profile = new(1f, 0.25f, 0.55f, 1f);
        public static readonly Color Selected = new(1f, 0.95f, 0.3f, 1f);
        public static readonly Color Secondary = new(0.3f, 0.85f, 1f, 1f);
        public static readonly Color Guide = new(1f, 1f, 1f, 0.5f);

        public static Color Faded(Color color, float alpha) => new(color.r, color.g, color.b, color.a * alpha);
    }
}
