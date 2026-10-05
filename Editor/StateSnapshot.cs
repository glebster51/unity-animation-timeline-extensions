using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ExtensionsForAnimationWondows
{
    internal static class StateSnapshot
    {
        internal sealed class CapturedProperty
        {
            public UnityEngine.Object target;
            public string propertyPath;
            public SerializedPropertyType propertyType;
            public float floatValue;
            public int intValue;
            public bool boolValue;
            public string stringValue;
            public Color colorValue;
            public Vector2 vector2Value;
            public Vector3 vector3Value;
            public Vector4 vector4Value;
            public Rect rectValue;
            public Bounds boundsValue;
            public Quaternion quaternionValue;
            public UnityEngine.Object objectReferenceValue;
        }

        internal static bool TryCaptureProperty(GameObject root, EditorCurveBinding binding, out CapturedProperty captured)
        {
            captured = null;
            UnityEngine.Object target = null;
            try
            {
                MethodInfo getAnimatedObject = UnityEditorBridge.FindMethod(
                    typeof(AnimationUtility),
                    "GetAnimatedObject",
                    parameters => parameters.Length == 2 &&
                                  parameters[0].ParameterType == typeof(GameObject) &&
                                  parameters[1].ParameterType == typeof(EditorCurveBinding));
                target = getAnimatedObject?.Invoke(
                    null,
                    new object[] { root, binding }) as UnityEngine.Object;
            }
            catch
            {
                return false;
            }

            if (!target)
                return false;

            var serializedObject = new SerializedObject(target);
            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty property = serializedObject.FindProperty(binding.propertyName);
            if (property == null)
            {
                if (binding.isPPtrCurve || !UnityEditorBridge.TryGetCurrentFloatValue(root, binding, out float animatedValue))
                    return false;

                captured = new CapturedProperty
                {
                    target = target,
                    propertyPath = binding.propertyName,
                    propertyType = SerializedPropertyType.Float,
                    floatValue = animatedValue
                };
                return true;
            }

            captured = new CapturedProperty
            {
                target = target,
                propertyPath = binding.propertyName,
                propertyType = property.propertyType
            };

            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    captured.floatValue = property.floatValue;
                    return true;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Enum:
                    captured.intValue = property.intValue;
                    return true;
                case SerializedPropertyType.Boolean:
                    captured.boolValue = property.boolValue;
                    return true;
                case SerializedPropertyType.String:
                    captured.stringValue = property.stringValue;
                    return true;
                case SerializedPropertyType.Color:
                    captured.colorValue = property.colorValue;
                    return true;
                case SerializedPropertyType.Vector2:
                    captured.vector2Value = property.vector2Value;
                    return true;
                case SerializedPropertyType.Vector3:
                    captured.vector3Value = property.vector3Value;
                    return true;
                case SerializedPropertyType.Vector4:
                    captured.vector4Value = property.vector4Value;
                    return true;
                case SerializedPropertyType.Rect:
                    captured.rectValue = property.rectValue;
                    return true;
                case SerializedPropertyType.Bounds:
                    captured.boundsValue = property.boundsValue;
                    return true;
                case SerializedPropertyType.Quaternion:
                    captured.quaternionValue = property.quaternionValue;
                    return true;
                case SerializedPropertyType.ObjectReference:
                    captured.objectReferenceValue = property.objectReferenceValue;
                    return true;
                case SerializedPropertyType.ExposedReference:
                    captured.objectReferenceValue = property.exposedReferenceValue;
                    return true;
                default:
                    return false;
            }
        }

        internal static bool ApplyProperties(IReadOnlyList<CapturedProperty> properties, string undoName)
        {
            var targets = properties
                .Where(property => property != null && property.target)
                .Select(property => property.target)
                .Distinct()
                .ToArray();
            if (targets.Length == 0)
                return false;

            Undo.RecordObjects(targets, undoName);
            int applied = 0;
            foreach (CapturedProperty captured in properties)
            {
                if (!captured.target)
                    continue;

                var serializedObject = new SerializedObject(captured.target);
                serializedObject.UpdateIfRequiredOrScript();
                SerializedProperty property = serializedObject.FindProperty(captured.propertyPath);
                if (property == null)
                {
                    if (captured.propertyType == SerializedPropertyType.Float &&
                        TryApplyTransformFloat(captured.target as Transform, captured.propertyPath, captured.floatValue))
                        applied++;
                    continue;
                }

                switch (captured.propertyType)
                {
                    case SerializedPropertyType.Float:
                        property.floatValue = captured.floatValue;
                        break;
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.LayerMask:
                    case SerializedPropertyType.Enum:
                        property.intValue = captured.intValue;
                        break;
                    case SerializedPropertyType.Boolean:
                        property.boolValue = captured.boolValue;
                        break;
                    case SerializedPropertyType.String:
                        property.stringValue = captured.stringValue;
                        break;
                    case SerializedPropertyType.Color:
                        property.colorValue = captured.colorValue;
                        break;
                    case SerializedPropertyType.Vector2:
                        property.vector2Value = captured.vector2Value;
                        break;
                    case SerializedPropertyType.Vector3:
                        property.vector3Value = captured.vector3Value;
                        break;
                    case SerializedPropertyType.Vector4:
                        property.vector4Value = captured.vector4Value;
                        break;
                    case SerializedPropertyType.Rect:
                        property.rectValue = captured.rectValue;
                        break;
                    case SerializedPropertyType.Bounds:
                        property.boundsValue = captured.boundsValue;
                        break;
                    case SerializedPropertyType.Quaternion:
                        property.quaternionValue = captured.quaternionValue;
                        break;
                    case SerializedPropertyType.ObjectReference:
                        property.objectReferenceValue = captured.objectReferenceValue;
                        break;
                    case SerializedPropertyType.ExposedReference:
                        property.exposedReferenceValue = captured.objectReferenceValue;
                        break;
                    default:
                        continue;
                }

                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                applied++;
            }

            foreach (UnityEngine.Object target in targets)
            {
                EditorUtility.SetDirty(target);
                if (PrefabUtility.IsPartOfPrefabInstance(target))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }

            foreach (GameObject gameObject in targets.Select(target => target as GameObject ?? (target as Component)?.gameObject)
                .Where(gameObject => gameObject && gameObject.scene.IsValid()).Distinct())
                EditorSceneManager.MarkSceneDirty(gameObject.scene);

            Debug.Log($"[{undoName}] ApplyState applied {applied} properties.");
            return applied > 0;
        }

        private static bool TryApplyTransformFloat(Transform transform, string propertyPath, float value)
        {
            if (!transform || string.IsNullOrEmpty(propertyPath))
                return false;

            int component = "xyzw".IndexOf(propertyPath[propertyPath.Length - 1]);
            if (component < 0 || component > 3)
                return false;

            if (propertyPath.IndexOf("localPosition", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Vector3 position = transform.localPosition;
                if (component == 0) position.x = value;
                else if (component == 1) position.y = value;
                else if (component == 2) position.z = value;
                else return false;
                transform.localPosition = position;
                return true;
            }

            if (propertyPath.IndexOf("localEulerAngles", StringComparison.OrdinalIgnoreCase) >= 0 ||
                propertyPath.IndexOf("LocalEulerAnglesHint", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Vector3 angles = transform.localEulerAngles;
                if (component == 0) angles.x = value;
                else if (component == 1) angles.y = value;
                else if (component == 2) angles.z = value;
                else return false;
                transform.localEulerAngles = angles;
                return true;
            }

            if (propertyPath.IndexOf("localScale", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Vector3 scale = transform.localScale;
                if (component == 0) scale.x = value;
                else if (component == 1) scale.y = value;
                else if (component == 2) scale.z = value;
                else return false;
                transform.localScale = scale;
                return true;
            }

            if (propertyPath.IndexOf("localRotation", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Quaternion rotation = transform.localRotation;
                if (component == 0) rotation.x = value;
                else if (component == 1) rotation.y = value;
                else if (component == 2) rotation.z = value;
                else if (component == 3) rotation.w = value;
                transform.localRotation = rotation;
                return true;
            }

            return false;
        }
    }
}
