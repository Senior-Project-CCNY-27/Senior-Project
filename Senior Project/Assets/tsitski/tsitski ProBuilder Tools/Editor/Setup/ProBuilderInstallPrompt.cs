using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Tsitski.ProBuilderTools {
    [InitializeOnLoad]
    internal static class ProBuilderInstallPrompt {
        private const string PromptedKey = "Tsitski.ProBuilderTools.ProBuilderPrompted";
        private const string Title = "tsitski ProBuilder Tools";
        private static AddRequest _request;
        private static int _progressId;

        static ProBuilderInstallPrompt() {
            if (!Application.isBatchMode)
                EditorApplication.update += PromptWhenIdle;
        }

        private static string InstallKey {
            get {
                foreach (string guid in AssetDatabase.FindAssets($"{nameof(ProBuilderInstallPrompt)} t:MonoScript")) {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(path) == nameof(ProBuilderInstallPrompt) && File.Exists(path))
                        return $"{PromptedKey}.{File.GetCreationTimeUtc(path).Ticks}.{File.GetLastWriteTimeUtc(path).Ticks}";
                }

                return PromptedKey;
            }
        }

        private static bool Busy => EditorApplication.isCompiling || EditorApplication.isUpdating;

        private static void PromptWhenIdle() {
            if (Busy)
                return;

            EditorApplication.update -= PromptWhenIdle;

            string key = InstallKey;
            if (SessionState.GetBool(key, false))
                return;

            SessionState.SetBool(key, true);
            if (EditorUtility.DisplayDialog(Title,
                    "tsitski ProBuilder Tools needs ProBuilder 5.0.3 or newer. Install the ProBuilder package now?",
                    "Install", "Later"))
                Install();
        }

        [MenuItem("Tools/tsitski/ProBuilder/Install ProBuilder")]
        private static void Install() {
            if (_request != null && !_request.IsCompleted)
                return;

            Debug.Log($"{Title}: installing ProBuilder...");
            _progressId = Progress.Start("Installing ProBuilder", null, Progress.Options.Indefinite);
            _request = Client.Add("com.unity.probuilder");
            EditorApplication.update += WaitForInstall;
        }

        private static void WaitForInstall() {
            if (!_request.IsCompleted) {
                EditorApplication.QueuePlayerLoopUpdate();
                return;
            }

            EditorApplication.update -= WaitForInstall;

            if (_request.Status == StatusCode.Failure) {
                Progress.Finish(_progressId, Progress.Status.Failed);
                Debug.LogError($"{Title}: installing ProBuilder failed: {_request.Error.message}");
                return;
            }

            Progress.Finish(_progressId);
            Debug.Log($"{Title}: ProBuilder {_request.Result.version} installed.");
            Client.Resolve();
            AssetDatabase.Refresh();
        }
    }
}
