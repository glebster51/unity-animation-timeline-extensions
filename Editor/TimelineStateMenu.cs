using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ExtensionsForAnimationWondows
{
    // Timeline's IMGUI gear has no public menu-extension callback. Put an IMGUI
    // button at that same location, populate its standard menu through Timeline's
    // helpers, and append the state commands. Package sources remain untouched.
    [InitializeOnLoad]
    internal static class TimelineStateMenu
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Instance | BindingFlags.Static;
        private static readonly Type WindowType = typeof(TimelineEditor).Assembly.GetType("UnityEditor.Timeline.TimelineWindow");
        private static readonly Dictionary<EditorWindow, IMGUIContainer> Buttons = new Dictionary<EditorWindow, IMGUIContainer>();
        private static double nextScan;
        private static bool failed;

        static TimelineStateMenu()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
        }

        private static void Update()
        {
            if (failed || !ExtensionPreferences.TimelineStateMenu)
            {
                Dispose();
                return;
            }
            try { UpdateWindows(); }
            catch (Exception exception)
            {
                failed = true;
                Dispose();
                Debug.LogWarning("Timeline state menu disabled: " + exception.GetBaseException().Message);
            }
        }

        private static void UpdateWindows()
        {
            if (WindowType == null || EditorApplication.isCompiling ||
                EditorApplication.timeSinceStartup < nextScan)
                return;
            nextScan = EditorApplication.timeSinceStartup + 0.5d;
            foreach (EditorWindow closed in Buttons.Keys.Where(w => !w).ToArray())
            {
                Buttons[closed].RemoveFromHierarchy();
                Buttons.Remove(closed);
            }

            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll(WindowType).OfType<EditorWindow>())
            {
                if (!Buttons.TryGetValue(window, out IMGUIContainer button) || button.parent == null)
                {
                    var constants = WindowType.Assembly.GetType("UnityEditor.Timeline.WindowConstants");
                    float width = Convert.ToSingle(UnityEditorBridge.Read(constants, "cogButtonWidth"));
                    float height = Convert.ToSingle(UnityEditorBridge.Read(constants, "timeAreaYPosition"));
                    button = new IMGUIContainer(() => DrawButton(window)) { name = "timeline-state-options" };
                    button.style.position = Position.Absolute;
                    button.style.top = 0;
                    button.style.right = 0;
                    button.style.width = width;
                    button.style.height = height;
                    window.rootVisualElement.Add(button);
                    Buttons[window] = button;
                }
                object mode = UnityEditorBridge.Read(window, "currentMode");
                string options = UnityEditorBridge.Read(UnityEditorBridge.Read(mode, "headerState"), "options")?.ToString();
                object state = UnityEditorBridge.Read(window, "state");
                bool visible = UnityEditorBridge.Read(UnityEditorBridge.Read(state, "editSequence"), "asset") is TimelineAsset && options != "Hidden";
                button.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                button.SetEnabled(options != "Disabled");
            }
        }

        private static void DrawButton(EditorWindow window)
        {
            if (!window)
                return;
            Rect rect = Buttons[window].contentRect;
            rect.position = Vector2.zero;
            var styleType = WindowType.Assembly.GetType("UnityEditor.Timeline.DirectorStyles");
            var icon = UnityEditorBridge.Read(styleType, "optionsCogIcon") as GUIContent ?? EditorGUIUtility.IconContent("_Popup");
            if (!EditorGUI.DropdownButton(rect, icon, FocusType.Keyboard, EditorStyles.toolbarButton))
                return;

            var menu = new GenericMenu();
            AddNativeItems(window, menu);
            menu.AddSeparator("");
            if (TimelineStateClipboard.CanCopy(window))
                menu.AddItem(new GUIContent("CopyState"), false, () => TimelineStateClipboard.Copy(window));
            else
                menu.AddDisabledItem(new GUIContent("CopyState"));
            if (TimelineStateClipboard.CanApply(window))
                menu.AddItem(new GUIContent("ApplyState"), false, () => TimelineStateClipboard.Apply(window));
            else
                menu.AddDisabledItem(new GUIContent("ApplyState"));
            menu.DropDown(rect);
        }

        private static void AddNativeItems(EditorWindow window, GenericMenu menu)
        {
            object state = UnityEditorBridge.Read(window, "state");
            object sequence = UnityEditorBridge.Read(state, "editSequence");
            menu.AddItem(new GUIContent("Preferences Page..."), false,
                () => SettingsService.OpenUserPreferences("Preferences/Timeline"));
            menu.AddSeparator("");

            PropertyInfo timeFormat = state.GetType().GetProperty("timeFormat", Flags);
            foreach (string name in new[] { "Frames", "Timecode", "Seconds" })
            {
                object value = Enum.Parse(timeFormat.PropertyType, name);
                var label = UnityEditorBridge.Read(WindowType, "MenuItem" + name) as GUIContent ?? new GUIContent("Time Format/" + name);
                menu.AddItem(label, Equals(timeFormat.GetValue(state), value), () =>
                {
                    timeFormat.SetValue(state, value);
                    window.Repaint();
                });
            }

            menu.AddSeparator("");
            WindowType.Assembly.GetType("UnityEditor.Timeline.TimeAreaContextMenu")
                .GetMethod("AddTimeAreaMenuItems", Flags).Invoke(null, new[] { (object)menu, state });
            menu.AddSeparator("");

            Type frameUtility = WindowType.Assembly.GetType("UnityEditor.Timeline.FrameRateDisplayUtility");
            MethodInfo standardMethod = frameUtility.GetMethod("GetStandardFromFrameRate", Flags);
            Type enumType = standardMethod.GetParameters()[1].ParameterType.GetElementType();
            var arguments = new[] { UnityEditorBridge.Read(sequence, "frameRate"), Enum.ToObject(enumType, 0) };
            bool standard = (bool)standardMethod.Invoke(null, arguments);
            object current = arguments[1];
            string[] labels = (string[])frameUtility.GetMethod("GetDefaultFrameRatesLabels", Flags)
                .Invoke(null, new[] { current });
            if (!standard)
            {
                string format = UnityEditorBridge.Read(WindowType, "k_CustomFpsLabel") as string ?? "{0} ({1} fps)";
                labels = (string[])labels.Clone();
                labels[labels.Length - 1] = string.Format(format, labels.Last(), UnityEditorBridge.Read(sequence, "frameRate"));
            }

            MethodInfo addFrameRate = WindowType.GetMethod("AddStandardFrameRateMenu", Flags);
            for (int i = 0; i < labels.Length; i++)
                addFrameRate.Invoke(window, new object[] { menu, Enum.ToObject(enumType, i), labels[i], Convert.ToInt32(current) == i });
        }

        private static void Dispose()
        {
            foreach (IMGUIContainer button in Buttons.Values)
                button.RemoveFromHierarchy();
            Buttons.Clear();
        }
    }
}
