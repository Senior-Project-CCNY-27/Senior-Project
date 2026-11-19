using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    public static class PointClipboard {
        private const string Prefix2D = "tsitski-points-2d:";
        private const string Prefix3D = "tsitski-points-3d:";
        private const char PathSeparator = '|';

        private static string Buffer => EditorGUIUtility.systemCopyBuffer ?? "";
        private static string PathMarker<T>() => $"{PathSeparator}tsitski-path:{typeof(T).Name}:";

        public static bool has2D => Buffer.StartsWith(Prefix2D);
        public static bool has3D => Buffer.StartsWith(Prefix3D);

        public static bool HasPath<T>() => Buffer.Contains(PathMarker<T>());

        public static void Copy(IEnumerable<Vector2> points) =>
            EditorGUIUtility.systemCopyBuffer = Prefix2D + string.Join(";", points.Select(p => Format(p.x, p.y)));

        public static void Copy(IEnumerable<Vector3> points) =>
            EditorGUIUtility.systemCopyBuffer = Format3D(points);

        public static void CopyPath<T>(IEnumerable<Vector3> points, T path) =>
            EditorGUIUtility.systemCopyBuffer = Format3D(points) + PathMarker<T>() + JsonUtility.ToJson(path);

        public static T PastePath<T>() where T : class {
            string buffer = Buffer, marker = PathMarker<T>();
            int index = buffer.IndexOf(marker, System.StringComparison.Ordinal);
            return index < 0 ? null : JsonUtility.FromJson<T>(buffer[(index + marker.Length)..]);
        }

        public static List<Vector2> Paste2D() =>
            Parse(Prefix2D, 2).Select(v => new Vector2(v[0], v[1])).ToList();

        public static List<Vector3> Paste3D() =>
            Parse(Prefix3D, 3).Select(v => new Vector3(v[0], v[1], v[2])).ToList();

        public static bool IsCopy(Event e) => e.commandName == "Copy";

        public static bool IsPaste(Event e) => e.commandName == "Paste";

        private static string Format3D(IEnumerable<Vector3> points) =>
            Prefix3D + string.Join(";", points.Select(p => Format(p.x, p.y, p.z)));

        private static string Format(params float[] values) =>
            string.Join(",", values.Select(v => v.ToString("R", CultureInfo.InvariantCulture)));

        private static IEnumerable<float[]> Parse(string prefix, int components) {
            string buffer = Buffer;
            if (!buffer.StartsWith(prefix))
                yield break;

            foreach (string entry in buffer[prefix.Length..].Split(PathSeparator)[0].Split(';')) {
                string[] parts = entry.Split(',');
                if (parts.Length != components)
                    continue;

                float[] values = new float[components];
                bool valid = true;
                for (int i = 0; i < components && valid; i++)
                    valid = float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]);

                if (valid)
                    yield return values;
            }
        }
    }
}
