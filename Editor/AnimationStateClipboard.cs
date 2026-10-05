using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ExtensionsForAnimationWondows
{
    internal static class AnimationStateClipboard
    {
        private static GameObject _root;
        private static AnimationClip _clip;
        private static List<StateSnapshot.CapturedProperty> _properties;

        public static bool HasState => _root && _clip && _properties != null && _properties.Count > 0;

        public static bool Copy(AnimationWindow window)
        {
            if (!CanCopy(window))
            {
                Debug.LogWarning("[Animation Window Extensions] CopyState skipped: Animation window is not previewing or has no clip.");
                return false;
            }

            GameObject root = UnityEditorBridge.GetActiveRoot(window);
            AnimationClip clip = window.animationClip;
            if (!root || !root.scene.IsValid() || EditorUtility.IsPersistent(root))
            {
                Debug.LogWarning("[Animation Window Extensions] CopyState skipped: active Animation root is missing.");
                return false;
            }

            var bindings = new List<EditorCurveBinding>();
            try
            {
                MethodInfo getAllBindings = UnityEditorBridge.FindMethod(
                    typeof(AnimationMode),
                    "GetAllBindings",
                    parameters => parameters.Length == 2 &&
                                  parameters[0].ParameterType == typeof(GameObject) &&
                                  parameters[1].ParameterType == typeof(AnimationClip));
                EditorCurveBinding[] animationBindings = getAllBindings?.Invoke(null, new object[] { root, clip }) as EditorCurveBinding[];
                if (animationBindings != null)
                    bindings.AddRange(animationBindings);
            }
            catch
            {
                // Fall back to the clip bindings below when the active animation mode
                // does not expose the Animator binding set.
            }

            bindings.AddRange(AnimationUtility.GetCurveBindings(clip));
            bindings.AddRange(AnimationUtility.GetObjectReferenceCurveBindings(clip));

            var captured = new List<StateSnapshot.CapturedProperty>();
            foreach (EditorCurveBinding binding in bindings
                .GroupBy(UnityEditorBridge.BindingKey)
                .Select(group => group.First()))
            {
                if (StateSnapshot.TryCaptureProperty(root, binding, out StateSnapshot.CapturedProperty property))
                    captured.Add(property);
            }

            if (captured.Count == 0)
            {
                Debug.LogWarning(
                    $"[Animation Window Extensions] CopyState found {bindings.Count} bindings but captured 0 serialized properties. " +
                    $"Root='{root.name}', Clip='{clip.name}', Time={window.time:0.######}.");
                return false;
            }

            _root = root;
            _clip = clip;
            _properties = captured;
            Debug.Log(
                $"[Animation Window Extensions] CopyState copied {captured.Count} properties from {bindings.Count} bindings. " +
                $"Root='{root.name}', Clip='{clip.name}', Time={window.time:0.######}.");
            return true;
        }

        public static bool Apply(AnimationWindow window)
        {
            if (!CanApply(window))
                return false;

            return StateSnapshot.ApplyProperties(_properties, "Apply Animation State");
        }
        internal static bool CanCopy(AnimationWindow window) =>
            !EditorApplication.isPlayingOrWillChangePlaymode && window && window.previewing && window.animationClip;

        internal static bool CanApply(AnimationWindow window) =>
            !EditorApplication.isPlayingOrWillChangePlaymode && window && !window.previewing &&
            !AnimationMode.InAnimationMode() && HasState;
    }
}
