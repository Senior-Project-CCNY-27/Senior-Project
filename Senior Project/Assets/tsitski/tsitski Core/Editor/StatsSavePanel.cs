using System.IO;
using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    public static class StatsSavePanel {
        private static string FolderKey => "Tsitski.ProBuilderTools.StatsSaveFolder." + Application.dataPath;

        public static string Show(string title, string defaultName, string message) {
            string folder = EditorPrefs.GetString(FolderKey, "Assets");
            if (!AssetDatabase.IsValidFolder(folder))
                folder = "Assets";

            string path = EditorUtility.SaveFilePanelInProject(title, defaultName, "asset", message, folder);

            if (!string.IsNullOrEmpty(path))
                EditorPrefs.SetString(FolderKey, Path.GetDirectoryName(path)?.Replace('\\', '/'));

            return path;
        }
    }
}
