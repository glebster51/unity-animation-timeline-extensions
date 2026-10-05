using UnityEditor;
using UnityEngine;

namespace ExtensionsForAnimationWondows
{
    internal static class ExtensionPreferences
    {
        private const string Prefix = "ExtensionsForAnimationWondows.";
        internal static bool AnimationStateMenu => EditorPrefs.GetBool(Prefix + "AnimationStateMenu", true);
        internal static bool TimelineStateMenu => EditorPrefs.GetBool(Prefix + "TimelineStateMenu", true);
        internal static bool SceneSelection => EditorPrefs.GetBool(Prefix + "SceneSelection", true);

        [SettingsProvider]
        private static SettingsProvider CreateProvider() => new SettingsProvider(
            "Preferences/Animation Window Extensions", SettingsScope.User)
        {
            keywords = new[] { "Animation", "Timeline", "CopyState", "ApplyState", "Selection" },
            guiHandler = _ =>
            {
                EditorGUILayout.LabelField("Animation & Timeline Window Extensions", EditorStyles.boldLabel);
                Toggle("Animation window state commands", "AnimationStateMenu", AnimationStateMenu);
                Toggle("Timeline window state commands", "TimelineStateMenu", TimelineStateMenu);
                Toggle("Select scene objects from Timeline animation keys", "SceneSelection", SceneSelection);
                EditorGUILayout.HelpBox("Changes apply automatically. Settings are per user and shared across projects.", MessageType.Info);
            }
        };

        private static void Toggle(string label, string name, bool current)
        {
            bool value = EditorGUILayout.ToggleLeft(label, current);
            if (value != current)
                EditorPrefs.SetBool(Prefix + name, value);
        }
    }
}
