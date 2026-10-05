using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ExtensionsForAnimationWondows
{
    // All access to Unity's non-public window state lives behind this bridge.
    internal static class UnityEditorBridge
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static |
                                           BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Dictionary<(Type, string), MemberInfo> Members =
            new Dictionary<(Type, string), MemberInfo>();
        private static readonly MethodInfo FloatValue = FindMethod(typeof(AnimationUtility), "GetFloatValue",
            p => p.Length == 3 && p[0].ParameterType == typeof(GameObject) &&
                 p[1].ParameterType == typeof(EditorCurveBinding) && p[2].ParameterType == typeof(float).MakeByRefType());

        internal static object Read(object instance, string name)
        {
            if (instance == null)
                return null;
            Type type = instance as Type ?? instance.GetType();
            var key = (type, name);
            if (!Members.TryGetValue(key, out MemberInfo member))
            {
                member = (MemberInfo)type.GetProperty(name, Flags) ?? type.GetField(name, Flags);
                Members[key] = member;
            }
            object target = instance is Type ? null : instance;
            return member is PropertyInfo property ? property.GetValue(target) : (member as FieldInfo)?.GetValue(target);
        }

        internal static GameObject GetActiveRoot(AnimationWindow window)
        {
            object state = Read(window, "state");
            // A Timeline-linked window owns its track binding; Selection may point to a child.
            return Read(state, "activeRootGameObject") as GameObject ?? Read(state, "activeGameObject") as GameObject;
        }

        internal static MethodInfo FindMethod(Type type, string name, Func<ParameterInfo[], bool> matches) =>
            type.GetMethods(Flags).FirstOrDefault(m => m.Name == name && matches(m.GetParameters()));

        internal static bool TryGetCurrentFloatValue(GameObject root, EditorCurveBinding binding, out float value)
        {
            value = default;
            if (!root || FloatValue == null)
                return false;
            var arguments = new object[] { root, binding, 0f };
            if (!(FloatValue.Invoke(null, arguments) is bool success) || !success)
                return false;
            value = (float)arguments[2];
            return true;
        }

        internal static string BindingKey(EditorCurveBinding binding) =>
            binding.path + "|" + binding.propertyName + "|" + binding.type?.AssemblyQualifiedName + "|" + binding.isPPtrCurve;
    }
}
