using UnityEditor;
using UnityEngine;

namespace Overdraft.EditorTools
{
    /// <summary>
    /// Writes private [SerializeField] references from generator code.
    /// <para>
    /// Every field name here is a string, so a rename that skips [FormerlySerializedAs] shows up
    /// as a loud console error rather than as a silently null reference at play time. That trade
    /// is the point: a missed wire must never be discoverable only by playing the game.
    /// </para>
    /// </summary>
    public static class EditorWiring
    {
        public static void Set(Component target, string fieldName, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[Overdraft] {target.GetType().Name} has no serialised field '{fieldName}'.");
                return;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetString(Component target, string fieldName, string value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[Overdraft] {target.GetType().Name} has no serialised field '{fieldName}'.");
                return;
            }

            property.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(Component target, string fieldName, Object[] values)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(fieldName);

            if (property == null || !property.isArray)
            {
                Debug.LogError($"[Overdraft] {target.GetType().Name} has no serialised array '{fieldName}'.");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Creates a child GameObject with a known local transform.</summary>
        public static GameObject Child(GameObject parent, string name, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPosition;
            return go;
        }
    }
}
