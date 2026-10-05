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
    internal static class TimelineStateClipboard
    {
        private static List<StateSnapshot.CapturedProperty> properties;
        private static PlayableDirector sourceDirector;
        private static TimelineAsset sourceTimeline;

        internal static bool CanCopy(EditorWindow window)
        {
            object state = UnityEditorBridge.Read(window, "state");
            return !EditorApplication.isPlayingOrWillChangePlaymode &&
                   UnityEditorBridge.Read(state, "previewMode") is bool preview && preview &&
                   TimelineEditor.inspectedDirector && TimelineEditor.inspectedAsset;
        }

        internal static bool CanApply(EditorWindow window)
        {
            object state = UnityEditorBridge.Read(window, "state");
            return !EditorApplication.isPlayingOrWillChangePlaymode &&
                   !(UnityEditorBridge.Read(state, "previewMode") is bool preview && preview) &&
                   !AnimationMode.InAnimationMode() &&
                   sourceDirector && sourceTimeline &&
                   sourceDirector == TimelineEditor.inspectedDirector &&
                   sourceTimeline == TimelineEditor.inspectedAsset &&
                   properties != null && properties.Any(p => p.target);
        }

        internal static bool Copy(EditorWindow window)
        {
            if (!CanCopy(window))
                return false;

            PlayableDirector director = TimelineEditor.inspectedDirector;
            TimelineAsset timeline = TimelineEditor.inspectedAsset;
            var captured = new List<StateSnapshot.CapturedProperty>();
            var seen = new HashSet<(Object, string)>();
            var roots = new HashSet<GameObject>();
            MethodInfo allBindings = UnityEditorBridge.FindMethod(typeof(AnimationMode), "GetAllBindings",
                parameters => parameters.Length == 2 && parameters[0].ParameterType == typeof(GameObject) &&
                              parameters[1].ParameterType == typeof(AnimationClip));

            foreach (AnimationTrack track in Tracks(timeline.GetRootTracks()).OfType<AnimationTrack>())
            {
                if (track.mutedInHierarchy)
                    continue;
                TrackAsset bindingTrack = track;
                while (bindingTrack.isSubTrack && bindingTrack.parent is TrackAsset parent)
                    bindingTrack = parent;
                Object binding = director.GetGenericBinding(bindingTrack);
                GameObject root = binding as GameObject ?? (binding as Component)?.gameObject;
                if (!root || !root.scene.IsValid() || EditorUtility.IsPersistent(root))
                    continue;

                var clips = track.GetClips().Select(c => (c.asset as AnimationPlayableAsset)?.clip)
                    .Where(c => c).ToList();
                if (track.infiniteClip)
                    clips.Add(track.infiniteClip);
                if (clips.Count == 0)
                    continue;

                roots.Add(root);
                foreach (AnimationClip clip in clips.Distinct())
                {
                    var bindings = new List<EditorCurveBinding>();
                    if (allBindings != null)
                    {
                        try
                        {
                            if (allBindings.Invoke(null, new object[] { root, clip }) is EditorCurveBinding[] previewBindings)
                                bindings.AddRange(previewBindings);
                        }
                        catch
                        {
                            // Clip bindings remain available if the preview API differs.
                        }
                    }
                    bindings.AddRange(AnimationUtility.GetCurveBindings(clip));
                    bindings.AddRange(AnimationUtility.GetObjectReferenceCurveBindings(clip));
                    foreach (EditorCurveBinding curveBinding in bindings.GroupBy(UnityEditorBridge.BindingKey)
                        .Select(group => group.First()))
                        Capture(root, curveBinding, captured, seen);
                }

                // The animation track can add a root offset even without root curves.
                CaptureTransform(root.transform, captured, seen);
                Animator animator = root.GetComponent<Animator>();
                if (animator && animator.isHuman && animator.avatar && animator.avatar.isValid)
                {
                    for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
                    {
                        Transform bone = animator.GetBoneTransform((HumanBodyBones)i);
                        if (bone)
                            CaptureTransform(bone, captured, seen);
                    }
                }
            }

            if (captured.Count == 0)
            {
                Debug.LogWarning("[Timeline State] CopyState: No bound scene animation properties to copy.");
                return false;
            }

            // Capture the evaluated scene values once, including overlapping clips,
            // masks and track offsets. Do not sample clips separately or move time.
            properties = captured;
            sourceDirector = director;
            sourceTimeline = timeline;
            Debug.Log($"[Timeline State] CopyState: {captured.Count} properties, {roots.Count} animation roots.");
            return true;
        }

        private static IEnumerable<TrackAsset> Tracks(IEnumerable<TrackAsset> tracks)
        {
            foreach (TrackAsset track in tracks)
            {
                yield return track;
                foreach (TrackAsset child in Tracks(track.GetChildTracks()))
                    yield return child;
            }
        }

        private static void Capture(GameObject root, EditorCurveBinding binding,
            List<StateSnapshot.CapturedProperty> captured, HashSet<(Object, string)> seen)
        {
            if (StateSnapshot.TryCaptureProperty(root, binding, out var property) &&
                seen.Add((property.target, property.propertyPath)))
                captured.Add(property);
        }

        private static void CaptureTransform(Transform transform,
            List<StateSnapshot.CapturedProperty> captured, HashSet<(Object, string)> seen)
        {
            foreach (string name in new[] { "m_LocalPosition", "m_LocalRotation", "m_LocalScale" })
            {
                int components = name == "m_LocalRotation" ? 4 : 3;
                for (int i = 0; i < components; i++)
                    Capture(transform.gameObject, EditorCurveBinding.FloatCurve("", typeof(Transform),
                        name + "." + "xyzw"[i]), captured, seen);
            }
        }

        internal static bool Apply(EditorWindow window)
        {
            if (!CanApply(window))
                return false;
            bool applied = StateSnapshot.ApplyProperties(properties, "Apply Timeline State");
            if (applied)
                SceneView.RepaintAll();
            return applied;
        }
    }

}
