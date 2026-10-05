using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ExtensionsForAnimationWondows
{
    // Timeline deliberately disables AnimationWindow's built-in scene selection.
    // Observe the native selection after it is processed, using the bound track root.
    [InitializeOnLoad]
    internal static class TimelineAnimationSceneSelection
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Dictionary<AnimationWindow, Observer> Windows = new Dictionary<AnimationWindow, Observer>();
        private static double nextScan;

        static TimelineAnimationSceneSelection()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
        }

        private static void Update()
        {
            if (!ExtensionPreferences.SceneSelection)
            {
                Dispose();
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            if (EditorApplication.timeSinceStartup >= nextScan)
            {
                nextScan = EditorApplication.timeSinceStartup + 0.5d;
                foreach (AnimationWindow closed in Windows.Keys.Where(w => !w).ToArray())
                {
                    Windows[closed].Dispose();
                    Windows.Remove(closed);
                }

                foreach (AnimationWindow window in Resources.FindObjectsOfTypeAll<AnimationWindow>())
                {
                    object state = UnityEditorBridge.Read(window, "state");
                    if (state == null)
                        continue;
                    if (Windows.TryGetValue(window, out Observer installed) && ReferenceEquals(installed.State, state))
                        continue;
                    installed?.Dispose();
                    Windows[window] = new Observer(window, state);
                }
            }

            foreach (Observer observer in Windows.Values)
                observer.Update();
        }

        private static void Dispose()
        {
            foreach (Observer observer in Windows.Values)
                observer.Dispose();
            Windows.Clear();
        }

        private sealed class Observer : IDisposable
        {
            internal readonly object State;
            private readonly AnimationWindow window;
            private readonly FieldInfo rowSelectionField;
            private readonly Action<int[]> rowSelectionHandler;
            private readonly HashSet<int> previousKeys = new HashSet<int>();
            private readonly List<GameObject> targets = new List<GameObject>();
            private AnimationClip previousClip;
            private GameObject previousRoot;
            private bool initialized;
            private bool rowsPending;
            private bool keysPending;
            private bool warned;

            internal Observer(AnimationWindow window, object state)
            {
                this.window = window;
                State = state;
                rowSelectionField = state.GetType().GetField("onHierarchySelectionChange", Flags);
                rowSelectionHandler = _ => rowsPending = EditorWindow.focusedWindow == window;
                if (rowSelectionField != null)
                {
                    var existing = rowSelectionField.GetValue(state) as Action<int[]>;
                    rowSelectionField.SetValue(state, existing + rowSelectionHandler);
                }
                window.rootVisualElement.RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            }

            private void OnMouseUp(MouseUpEvent evt)
            {
                if (evt.button != 0)
                    return;

                // Also handle clicking a key that was already selected. Do not react
                // to the ruler, toolbar or transport buttons, which do not select keys.
                for (VisualElement element = evt.target as VisualElement; element != null; element = element.parent)
                {
                    string typeName = element.GetType().Name;
                    if (typeName.Contains("Ruler") || typeName.Contains("Toolbar") || typeName.Contains("Button"))
                        return;
                    if (typeName.Contains("DopeSheet") || typeName.Contains("Dopeline") || typeName.Contains("CurveEditor"))
                    {
                        keysPending = true;
                        return;
                    }
                }
            }

            internal void Update()
            {
                if (!window)
                    return;
                try
                {
                    UpdateSelection();
                }
                catch (Exception exception)
                {
                    initialized = false;
                    rowsPending = keysPending = false;
                    if (!warned)
                    {
                        warned = true;
                        Debug.LogWarning("Timeline Animation scene selection: " + exception.GetBaseException().Message);
                    }
                }
            }

            private void UpdateSelection()
            {
                object controller = UnityEditorBridge.Read(State, "controller");
                bool linked = UnityEditorBridge.Read(State, "linkedWithSequencer") is bool isLinked && isLinked;
                if (EditorWindow.focusedWindow != window || !linked ||
                    controller?.GetType().FullName != "UnityEditor.Timeline.TimelineWindowTimeControl" ||
                    (UnityEditorBridge.Read(State, "filterBySelection") is bool filter && filter))
                {
                    initialized = false;
                    rowsPending = keysPending = false;
                    return;
                }

                var root = UnityEditorBridge.Read(State, "activeRootGameObject") as GameObject;
                AnimationClip clip = window.animationClip;
                if (!root || !root.scene.IsValid() || EditorUtility.IsPersistent(root) || !clip)
                    return;

                var hashes = UnityEditorBridge.Read(State, "selectedKeyHashes") as HashSet<int>;
                bool contextChanged = !initialized || previousClip != clip || previousRoot != root;
                bool keySelectionChanged = !contextChanged && hashes != null && !previousKeys.SetEquals(hashes);
                previousKeys.Clear();
                if (hashes != null)
                    previousKeys.UnionWith(hashes);
                previousClip = clip;
                previousRoot = root;
                initialized = true;

                bool useKeys = hashes != null && hashes.Count > 0 &&
                               (keySelectionChanged || (keysPending && !rowsPending));
                bool shouldSync = useKeys || rowsPending;
                rowsPending = keysPending = false;
                if (!shouldSync)
                    return;

                targets.Clear();
                if (useKeys && UnityEditorBridge.Read(State, "selectedKeys") is IEnumerable keys)
                {
                    foreach (object key in keys)
                        AddCurveTarget(root, UnityEditorBridge.Read(key, "curve"));
                }
                else if (UnityEditorBridge.Read(State, "selectedHierarchyNodes") is IEnumerable nodes)
                {
                    foreach (object node in nodes)
                    {
                        // The summary row has all curves but no single scene target.
                        if (node.GetType().Name.Contains("MasterNode"))
                            continue;
                        if (UnityEditorBridge.Read(node, "curves") is IEnumerable curves)
                            foreach (object curve in curves)
                                AddCurveTarget(root, curve);
                    }
                }

                if (targets.Count == 0)
                    return;
                Object[] selection = Selection.objects;
                if (selection.Length == targets.Count && targets.All(t => selection.Contains(t)))
                    return;

                // Selection only: never sample, restart preview, change time or focus.
                // linkedWithSequencer keeps AnimationWindow bound to its Timeline clip.
                Selection.objects = targets.Cast<Object>().ToArray();
                SceneView.RepaintAll();
            }

            private void AddCurveTarget(GameObject root, object curve)
            {
                if (!(UnityEditorBridge.Read(curve, "binding") is EditorCurveBinding binding))
                    return;
                Object animated = AnimationUtility.GetAnimatedObject(root, binding);
                GameObject target = animated as GameObject ?? (animated as Component)?.gameObject;
                if (target && !targets.Contains(target))
                    targets.Add(target);
            }

            public void Dispose()
            {
                if (rowSelectionField != null)
                {
                    var existing = rowSelectionField.GetValue(State) as Action<int[]>;
                    rowSelectionField.SetValue(State, existing - rowSelectionHandler);
                }
                if (window && window.rootVisualElement != null)
                    window.rootVisualElement.UnregisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            }
        }
    }
}
