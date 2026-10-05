using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExtensionsForAnimationWondows
{
    [InitializeOnLoad]
    internal static class AnimationWindowStateMenu
    {
        private static readonly Dictionary<AnimationWindow, EventCallback<ContextualMenuPopulateEvent>> Windows =
            new Dictionary<AnimationWindow, EventCallback<ContextualMenuPopulateEvent>>();
        private static double nextScan;

        static AnimationWindowStateMenu()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
        }

        private static void Update()
        {
            if (!ExtensionPreferences.AnimationStateMenu)
            {
                Dispose();
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.timeSinceStartup < nextScan)
                return;
            nextScan = EditorApplication.timeSinceStartup + 0.5;
            foreach (AnimationWindow closed in Windows.Keys.Where(w => !w).ToArray())
                Windows.Remove(closed);
            foreach (AnimationWindow window in Resources.FindObjectsOfTypeAll<AnimationWindow>())
            {
                if (Windows.ContainsKey(window))
                    continue;
                EventCallback<ContextualMenuPopulateEvent> handler = evt => Populate(window, evt);
                window.rootVisualElement.RegisterCallback(handler, TrickleDown.TrickleDown);
                Windows.Add(window, handler);
            }
        }

        private static void Populate(AnimationWindow window, ContextualMenuPopulateEvent evt)
        {
            if (!ExtensionPreferences.AnimationStateMenu)
                return;
            for (VisualElement element = evt.target as VisualElement; element != null; element = element.parent)
            {
                if (!(element.GetType().FullName ?? "").Contains("AnimationWindow.Widgets.OptionsButton"))
                    continue;
                evt.menu.AppendAction("CopyState", _ => AnimationStateClipboard.Copy(window),
                    _ => AnimationStateClipboard.CanCopy(window) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction("ApplyState", _ => AnimationStateClipboard.Apply(window),
                    _ => AnimationStateClipboard.CanApply(window) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                return;
            }
        }

        private static void Dispose()
        {
            foreach (var entry in Windows)
                if (entry.Key)
                    entry.Key.rootVisualElement.UnregisterCallback(entry.Value, TrickleDown.TrickleDown);
            Windows.Clear();
        }
    }
}
