using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Tsitski.Core {
    [InitializeOnLoad]
    public class TsitskiToolsPopup : EditorWindow {
        const string MenuRoot = "Tools/tsitski/";
        const float MinWidth = 220f;
        const float ButtonHeight = 24f;
        const float Padding = 4f;
        const float RowSpacing = 2f;
        const float SeparatorHeight = 6f;
        const int SeparatorPriorityGap = 10;
        const double FocusGraceSeconds = 0.25;
        const string CreateMenu = "Create";
        const string SubmenuArrow = "▶";
        const string ReviewUrl = "https://assetstore.unity.com/packages/tools/level-design/tsitski-probuilder-tools-416938";
        static readonly Color ReviewColor = new(0.78f, 0.88f, 1f, 1f);

        class Tool {
            public string shortcut;
            public Func<bool> enabled;
            public Action run;
        }

        class Node {
            public string name;
            public int priority = int.MaxValue;
            public Tool tool;
            public readonly List<Node> children = new();
        }

        static readonly List<TsitskiToolsPopup> s_Chain = new();
        static Vector2 s_LastMouseScreenPosition;
        static Vector2 s_OpenedAtScreenPosition;
        static double s_OpenedAt;
        static double s_ClosedAt;
        static GUIStyle s_ArrowStyle;
        static GUIStyle s_NumberStyle;
        static GUIStyle s_GroupStyle;
        static GUIStyle s_ButtonStyle;
        static string s_Search = "";
        const int MaxResults = 10;
        const string SearchPlaceholder = "Type to search...";

        Node _node;
        Node _openChild;
        readonly Dictionary<Node, Rect> _rows = new();
        List<(Node node, string group)> _leaves;
        float _baseWidth;
        Rect _rect;
        SceneView _origin;
        bool _closing;

        static TsitskiToolsPopup() {
            SceneView.duringSceneGui += OnSceneGui;
            EditorApplication.update += CloseOnFocusLoss;
            AssemblyReloadEvents.beforeAssemblyReload += () => CloseAll(false);
        }

        static void OnSceneGui(SceneView sceneView) {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && s_Chain.Count > 0 && EditorApplication.timeSinceStartup - s_OpenedAt >= FocusGraceSeconds)
                CloseAll(false);
            if (e.isMouse || e.type == EventType.Repaint)
                s_LastMouseScreenPosition = GUIUtility.GUIToScreenPoint(e.mousePosition);
        }

        [Shortcut("tsitski/Tools Popup", typeof(SceneView), KeyCode.X, ShortcutModifiers.Alt)]
        static void OpenFromShortcut() {
            if (s_Chain.Count > 0) {
                CloseAll(true);
                return;
            }

            if (EditorApplication.timeSinceStartup - s_ClosedAt < FocusGraceSeconds)
                return;

            Open(s_LastMouseScreenPosition);
        }

        static void Open(Vector2 screenPosition) {
            CloseAll(false);
            foreach (TsitskiToolsPopup stray in Resources.FindObjectsOfTypeAll<TsitskiToolsPopup>())
                stray.Close();

            Node root = BuildTree();
            if (root.children.Count == 0)
                return;

            root.name = "tsitski";
            s_Search = "";
            s_OpenedAtScreenPosition = screenPosition;
            TsitskiToolsPopup popup = Show(root, new Rect(screenPosition, Vector2.zero), false);
            popup._origin = SceneView.lastActiveSceneView;
            popup._leaves = Leaves(root, "", new List<(Node, string)>());
            popup.Focus();
        }

        static TsitskiToolsPopup Show(Node node, Rect anchor, bool beside) {
            TsitskiToolsPopup popup = CreateInstance<TsitskiToolsPopup>();
            popup._node = node;
            popup.wantsMouseMove = true;

            Vector2 size = new(Width(node), EstimateHeight(node, s_Chain.Count == 0));
            popup._baseWidth = size.x;
            Rect rect = new(beside ? anchor.xMax : anchor.x, anchor.y, size.x, size.y);
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            if (main.Contains(anchor.position)) {
                if (rect.xMax > main.xMax)
                    rect.x = beside ? anchor.xMin - size.x : main.xMax - size.x;
                if (rect.yMax > main.yMax)
                    rect.y = main.yMax - size.y;
                rect.x = Mathf.Max(rect.x, main.x);
                rect.y = Mathf.Max(rect.y, main.y);
            }

            s_Chain.Add(popup);
            s_OpenedAt = EditorApplication.timeSinceStartup;
            popup.ShowPopup();
            popup._rect = rect;
            popup.position = rect;
            return popup;
        }

        static Node BuildTree() {
            Node root = new();
            Dictionary<string, MethodInfo> validators = new();
            List<(string path, int priority, Tool tool)> found = new();

            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<MenuItem>()) {
                foreach (MenuItem item in method.GetCustomAttributes<MenuItem>()) {
                    if (!item.menuItem.StartsWith(MenuRoot))
                        continue;

                    (string path, string shortcut) = SplitShortcut(item.menuItem);
                    if (item.validate) {
                        validators[path] = method;
                        continue;
                    }

                    string menuPath = path;
                    found.Add((path.Substring(MenuRoot.Length), item.priority, new Tool {
                        shortcut = shortcut,
                        run = () => EditorApplication.ExecuteMenuItem(menuPath),
                        enabled = () => !validators.TryGetValue(menuPath, out MethodInfo validate) || Invoke(validate)
                    }));
                }
            }

            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<TsitskiMenuAttribute>()) {
                if (!method.IsStatic || method.GetParameters().Length > 0) {
                    Debug.LogWarning($"[tsitski] [TsitskiMenu] on {method.DeclaringType?.Name}.{method.Name} is ignored: it has to be static without parameters.");
                    continue;
                }

                foreach (TsitskiMenuAttribute item in method.GetCustomAttributes<TsitskiMenuAttribute>()) {
                    MethodInfo validate = string.IsNullOrEmpty(item.validate) ? null
                        : method.DeclaringType?.GetMethod(item.validate, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    found.Add((item.path.Trim('/'), item.priority, new Tool {
                        shortcut = item.shortcut,
                        run = () => method.Invoke(null, null),
                        enabled = () => validate == null || Invoke(validate)
                    }));
                }
            }

            HashSet<string> added = new();
            foreach ((string path, int priority, Tool tool) in found) {
                if (!added.Add(path))
                    continue;

                Node node = root;
                node.priority = Mathf.Min(node.priority, priority);
                string[] parts = path.Split('/');
                for (int i = 0; i < parts.Length; i++) {
                    bool leaf = i == parts.Length - 1;
                    Node child = leaf ? null : node.children.FirstOrDefault(c => c.tool == null && c.name == parts[i]);
                    if (child == null) {
                        child = new Node { name = parts[i] };
                        node.children.Add(child);
                    }

                    child.priority = Mathf.Min(child.priority, priority);
                    if (leaf)
                        child.tool = tool;
                    node = child;
                }
            }

            Sort(root);
            return root;
        }

        static void Sort(Node node) {
            node.children.Sort((a, b) => {
                int order = Rank(a).CompareTo(Rank(b));
                if (order != 0)
                    return order;
                return a.priority != b.priority ? a.priority.CompareTo(b.priority) : string.CompareOrdinal(a.name, b.name);
            });
            foreach (Node child in node.children)
                Sort(child);
        }

        static int Rank(Node node) => node.tool != null ? 2 : node.name == CreateMenu ? 0 : 1;

        static bool Invoke(MethodInfo validate) {
            try {
                object[] args = validate.GetParameters().Length == 1 ? new object[] { new MenuCommand(null) } : null;
                return validate.Invoke(null, args) is true;
            }
            catch {
                return false;
            }
        }

        static (string path, string shortcut) SplitShortcut(string menuItem) {
            int space = menuItem.LastIndexOf(' ');
            if (space < 0)
                return (menuItem, "");

            string last = menuItem.Substring(space + 1);
            if (last.Length < 2 || "%#&_".IndexOf(last[0]) < 0)
                return (menuItem, "");

            string shortcut = last
                .Replace("%", "Ctrl+").Replace("#", "Shift+").Replace("&", "Alt+").TrimStart('_')
                .Replace("DEL", "Delete");
            return (menuItem.Substring(0, space), shortcut);
        }

        static bool Separated(Node previous, Node next) {
            if (previous == null || next.tool == null)
                return false;
            return previous.tool == null || next.priority - previous.priority > SeparatorPriorityGap;
        }

        static float Width(Node node) {
            float width = MinWidth;
            foreach (Node child in node.children)
                width = Mathf.Max(width, EditorStyles.label.CalcSize(new GUIContent(Label(child))).x + Padding * 2 + 48f);
            return width;
        }

        static List<(Node, string)> Leaves(Node node, string group, List<(Node, string)> into) {
            foreach (Node child in node.children) {
                if (child.tool != null)
                    into.Add((child, group));
                else
                    Leaves(child, group.Length == 0 ? child.name : $"{group} / {child.name}", into);
            }
            return into;
        }

        static float SearchRowWidth(Node node, string group) {
            s_GroupStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            return EditorStyles.label.CalcSize(new GUIContent(Label(node))).x + s_GroupStyle.CalcSize(new GUIContent(group)).x + Padding * 2 + 64f;
        }

        List<(Node node, string group)> Results() {
            string query = s_Search.Trim().ToLowerInvariant();
            return _leaves
                .Select(leaf => (leaf, score: Score(leaf.node.name, leaf.group, query)))
                .Where(result => result.score >= 0)
                .OrderBy(result => result.score)
                .ThenBy(result => result.leaf.node.name.Length)
                .ThenBy(result => result.leaf.node.name, StringComparer.Ordinal)
                .Take(MaxResults)
                .Select(result => result.leaf)
                .ToList();
        }

        static int Score(string name, string group, string query) {
            string lower = name.ToLowerInvariant();
            if (lower.StartsWith(query))
                return 0;
            if (lower.Contains(" " + query))
                return 1;
            if (lower.Contains(query))
                return 2;

            string full = $"{group} {name}".ToLowerInvariant();
            if (query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).All(full.Contains))
                return 3;

            int matched = 0;
            foreach (char c in lower)
                if (matched < query.Length && c == query[matched])
                    matched++;
            return matched == query.Length ? 4 : -1;
        }

        static float EstimateHeight(Node node, bool root) {
            float height = Padding * 2 + EditorGUIUtility.singleLineHeight + node.children.Count * (ButtonHeight + RowSpacing);
            if (root)
                height += ButtonHeight + RowSpacing;
            for (int i = 1; i < node.children.Count; i++)
                if (Separated(node.children[i - 1], node.children[i]))
                    height += SeparatorHeight;
            return height + (root && ShowReview ? ButtonHeight + 8f : 0f);
        }

        static string Label(Node node) =>
            node.tool == null || string.IsNullOrEmpty(node.tool.shortcut) ? node.name : $"{node.name}   ({node.tool.shortcut})";

        int Depth => s_Chain.IndexOf(this);

        void OnGUI() {
            if (_node == null || Depth < 0) {
                Close();
                return;
            }

            Event e = Event.current;
            if (focusedWindow == this)
                EditorGUIUtility.editingTextField = true;

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) {
                if (s_Search.Length > 0) {
                    s_Search = "";
                    e.Use();
                    Repaint();
                    return;
                }
                CloseAll(true);
                GUIUtility.ExitGUI();
            }

            if (e.type == EventType.KeyDown)
                HandleKey(e);

            if (e.type == EventType.MouseMove)
                Repaint();

            if (e.type == EventType.Repaint)
                DrawBorder(new Rect(0f, 0f, position.width, position.height));

            GUILayout.BeginArea(new Rect(Padding, Padding, position.width - Padding * 2, position.height - Padding * 2));
            GUILayout.Label(_node.name, EditorStyles.boldLabel);

            if (Depth == 0) {
                DrawSearchBar();
                if (s_Search.Length > 0) {
                    DrawResults();
                    FitSize(_rect.width, GUILayoutUtility.GetLastRect().yMax + Padding * 2f);
                    GUILayout.EndArea();
                    return;
                }
            }

            FitWidth(_baseWidth);

            Node previous = null;
            for (int i = 0; i < _node.children.Count; i++) {
                Node child = _node.children[i];
                if (Separated(previous, child))
                    GUILayout.Space(SeparatorHeight);
                previous = child;

                if (child.tool == null)
                    DrawSubmenu(child, e);
                else
                    DrawTool(child, e);

                Rect row = GUILayoutUtility.GetLastRect();
                if (e.type == EventType.Repaint)
                    _rows[child] = GUIUtility.GUIToScreenRect(row);
                DrawNumber(row, i);
            }

            if (Depth == 0 && ShowReview)
                DrawReview();

            FitSize(_rect.width, GUILayoutUtility.GetLastRect().yMax + Padding * 2f);
            GUILayout.EndArea();
        }

        void DrawSearchBar() {
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.toolbarSearchField, GUILayout.Height(ButtonHeight - 4f), GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint)
                return;

            bool empty = s_Search.Length == 0;
            Color color = GUI.color;
            if (empty)
                GUI.color = new Color(color.r, color.g, color.b, color.a * 0.5f);
            EditorStyles.toolbarSearchField.Draw(rect, new GUIContent(empty ? SearchPlaceholder : s_Search + "|"), false, false, false, !empty);
            GUI.color = color;
        }

        void DrawResults() {
            List<(Node node, string group)> results = Results();
            FitWidth(Mathf.Max(_baseWidth, results.Count == 0 ? 0f : results.Max(result => SearchRowWidth(result.node, result.group))));
            if (results.Count == 0) {
                GUILayout.Label("No matches", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(ButtonHeight));
                return;
            }

            s_GroupStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            for (int i = 0; i < results.Count; i++) {
                DrawTool(results[i].node, Event.current);
                Rect row = GUILayoutUtility.GetLastRect();
                DrawNumber(row, i);
                if (Event.current.type == EventType.Repaint) {
                    Color color = GUI.color;
                    GUI.color = new Color(color.r, color.g, color.b, color.a * 0.6f);
                    GUI.Label(new Rect(row.x, row.y, row.width - 6f, row.height), results[i].group, s_GroupStyle);
                    GUI.color = color;
                }
            }
        }

        static void DrawBorder(Rect rect) {
            Color color = EditorGUIUtility.isProSkin ? new Color(0.1f, 0.1f, 0.1f, 1f) : new Color(0.5f, 0.5f, 0.5f, 1f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        void DrawTool(Node node, Event e) {
            using (new EditorGUI.DisabledScope(!node.tool.enabled())) {
                if (GUILayout.Button(Label(node), ButtonStyle, GUILayout.Height(ButtonHeight)))
                    Run(node);
            }
        }

        static void Run(Node node) {
            Action run = node.tool.run;
            SceneView origin = s_Chain[0]._origin;
            Vector2 cursor = s_OpenedAtScreenPosition;
            CloseAll(true);
            EditorApplication.delayCall += () => TsitskiCreateMenu.RunAtCursor(origin, cursor, run);
            GUIUtility.ExitGUI();
        }

        static GUIStyle ButtonStyle => s_ButtonStyle ??= new GUIStyle(GUI.skin.button) {
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(24, 6, 2, 3)
        };

        static void DrawNumber(Rect row, int index) {
            if (index > 9 || Event.current.type != EventType.Repaint)
                return;

            s_NumberStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            Color color = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * 0.6f);
            GUI.Label(new Rect(row.x + 6f, row.y, 16f, row.height), ((index + 1) % 10).ToString(), s_NumberStyle);
            GUI.color = color;
        }

        void HandleKey(Event e) {
            if (e.keyCode == KeyCode.X && e.alt) {
                e.Use();
                CloseAll(true);
                GUIUtility.ExitGUI();
            }

            TsitskiToolsPopup root = s_Chain[0];
            char c = e.character;
            bool typing = c != 0 && !char.IsControl(c) && !char.IsDigit(c) && !e.alt && !e.control && !e.command &&
                          !(c == ' ' && s_Search.Length == 0);
            if (typing) {
                e.Use();
                s_Search += c;
                root.CloseChildren();
                root.Focus();
                root.Repaint();
                return;
            }

            if (s_Search.Length > 0 && this == root) {
                HandleSearchKey(e);
                return;
            }

            if (e.keyCode is KeyCode.Backspace or KeyCode.LeftArrow && Depth > 0) {
                TsitskiToolsPopup parent = s_Chain[Depth - 1];
                e.Use();
                parent.CloseChildren();
                parent.Focus();
                GUIUtility.ExitGUI();
            }

            int index = e.keyCode switch {
                >= KeyCode.Alpha1 and <= KeyCode.Alpha9 => e.keyCode - KeyCode.Alpha1,
                >= KeyCode.Keypad1 and <= KeyCode.Keypad9 => e.keyCode - KeyCode.Keypad1,
                KeyCode.Alpha0 or KeyCode.Keypad0 => 9,
                _ => -1
            };
            if (index < 0 || index >= _node.children.Count)
                return;

            Node node = _node.children[index];
            e.Use();
            if (node.tool != null) {
                if (node.tool.enabled())
                    Run(node);
                return;
            }

            if (!_rows.TryGetValue(node, out Rect row))
                return;
            TsitskiToolsPopup child = OpenChild(node, row);
            child.Focus();
        }

        void HandleSearchKey(Event e) {
            if (e.keyCode == KeyCode.Backspace) {
                e.Use();
                s_Search = e.control || e.alt ? "" : s_Search.Substring(0, s_Search.Length - 1);
                Repaint();
                return;
            }

            List<(Node node, string group)> results = Results();
            int index = e.keyCode switch {
                KeyCode.Return or KeyCode.KeypadEnter => results.FindIndex(result => result.node.tool.enabled()),
                >= KeyCode.Alpha1 and <= KeyCode.Alpha9 => e.keyCode - KeyCode.Alpha1,
                >= KeyCode.Keypad1 and <= KeyCode.Keypad9 => e.keyCode - KeyCode.Keypad1,
                KeyCode.Alpha0 or KeyCode.Keypad0 => 9,
                _ => -2
            };
            if (index == -2)
                return;

            e.Use();
            if (index >= 0 && index < results.Count && results[index].node.tool.enabled())
                Run(results[index].node);
        }

        void DrawSubmenu(Node node, Event e) {
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, ButtonStyle, GUILayout.Height(ButtonHeight), GUILayout.ExpandWidth(true));
            bool open = _openChild == node;
            bool clicked = GUI.Toggle(rect, open, node.name, ButtonStyle) != open;

            s_ArrowStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(rect.x, rect.y, rect.width - 6f, rect.height), SubmenuArrow, s_ArrowStyle);

            bool hovered = e.type == EventType.MouseMove && rect.Contains(e.mousePosition);
            if ((clicked || hovered) && !open)
                OpenChild(node, GUIUtility.GUIToScreenRect(rect));
        }

        TsitskiToolsPopup OpenChild(Node node, Rect screenRow) {
            CloseChildren();
            _openChild = node;
            Rect anchor = new(_rect.x, screenRow.y - Padding - EditorGUIUtility.singleLineHeight - RowSpacing, _rect.width, 0f);
            TsitskiToolsPopup child = Show(node, anchor, true);
            Focus();
            Repaint();
            return child;
        }

        void CloseChildren() {
            _openChild = null;
            int depth = Depth;
            for (int i = s_Chain.Count - 1; i > depth; i--) {
                TsitskiToolsPopup child = s_Chain[i];
                s_Chain.RemoveAt(i);
                if (child) {
                    child._closing = true;
                    child.Close();
                }
            }
            Repaint();
        }

        static void CloseOnFocusLoss() {
            if (s_Chain.Count == 0 || EditorApplication.timeSinceStartup - s_OpenedAt < FocusGraceSeconds)
                return;

            EditorWindow focused = focusedWindow;
            if (focused && !(focused is TsitskiToolsPopup) && !(focused is SceneView))
                CloseAll(false);
        }

        static void CloseAll(bool returnFocus) {
            if (s_Chain.Count == 0)
                return;

            s_ClosedAt = EditorApplication.timeSinceStartup;
            EditorGUIUtility.editingTextField = false;
            SceneView origin = s_Chain[0] ? s_Chain[0]._origin : null;
            TsitskiToolsPopup[] chain = s_Chain.ToArray();
            s_Chain.Clear();
            foreach (TsitskiToolsPopup popup in chain) {
                if (!popup || popup._closing)
                    continue;
                popup._closing = true;
                popup.Close();
            }

            if (!returnFocus)
                return;

            EditorApplication.delayCall += () => {
                if (!origin)
                    return;

                origin.Focus();
                foreach (KeyCode key in new[] { KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.X })
                    origin.SendEvent(new Event { type = EventType.KeyUp, keyCode = key });
                origin.Repaint();
            };
        }

        void OnDestroy() {
            if (!_closing && s_Chain.Contains(this))
                CloseAll(false);
        }

        static bool ShowReview {
            get {
                TsitskiSettings settings = TsitskiSettings.Instance;
                return !settings || settings.showReviewButton;
            }
        }

        void DrawReview() {
            GUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope()) {
                Color background = GUI.backgroundColor;
                GUI.backgroundColor = ReviewColor;
                GUIStyle style = new(GUI.skin.button) { fontStyle = FontStyle.Bold };
                bool review = GUILayout.Button("Leave A Review :)", style, GUILayout.Height(ButtonHeight));
                bool hide = GUILayout.Button("×", style, GUILayout.Width(ButtonHeight), GUILayout.Height(ButtonHeight));
                GUI.backgroundColor = background;

                if (review) {
                    Application.OpenURL(ReviewUrl);
                    CloseAll(true);
                    GUIUtility.ExitGUI();
                }

                if (hide) {
                    TsitskiSettings settings = TsitskiSettings.Instance;
                    if (settings) {
                        Undo.RecordObject(settings, "Hide Review Button");
                        settings.showReviewButton = false;
                        EditorUtility.SetDirty(settings);
                        AssetDatabase.SaveAssetIfDirty(settings);
                    }
                    Repaint();
                    GUIUtility.ExitGUI();
                }
            }
        }

        void FitWidth(float width) {
            if (_baseWidth > 0f)
                FitSize(width, _rect.height);
        }

        void FitSize(float width, float height) {
            if (Event.current.type != EventType.Repaint || Mathf.Abs(_rect.width - width) < 1f && Mathf.Abs(_rect.height - height) < 1f)
                return;

            _rect.width = width;
            _rect.height = height;
            minSize = maxSize = _rect.size;
            position = _rect;
        }

        public static void FitHeight(EditorWindow window, float height) {
            if (Event.current.type != EventType.Repaint || Mathf.Abs(window.position.height - height) < 1f)
                return;

            Rect rect = window.position;
            rect.height = height;
            window.minSize = new Vector2(rect.width, height);
            window.maxSize = new Vector2(rect.width, height);
            window.position = rect;
        }
    }
}
