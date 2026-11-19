using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.Core {
    public interface IInputHashed {
        int inputHash { get; set; }
    }

    public static class GeneratorInputs {
        private const string HashField = "inputHash";
        private const int MaxDepth = 3;
        private const ulong Prime = 1099511628211UL;

        public static void Store(Object owner) {
#if UNITY_EDITOR
            if (owner is IInputHashed hashed)
                hashed.inputHash = Hash(owner);
#endif
        }

        public static bool UpToDate(Object owner) =>
            owner is IInputHashed hashed && hashed.inputHash != 0 && hashed.inputHash == Hash(owner);

        public static int Hash(Object owner) {
#if UNITY_EDITOR
            ulong hash = 14695981039346656037UL;
            Add(ref hash, owner, new HashSet<Object> { owner }, 0);
            int folded = (int)(hash ^ (hash >> 32));
            return folded == 0 ? 1 : folded;
#else
            return 0;
#endif
        }

#if UNITY_EDITOR
        private static void Mix(ref ulong hash, ulong value) {
            for (int i = 0; i < 8; i++) {
                hash ^= (value >> (i * 8)) & 0xFF;
                hash *= Prime;
            }
        }

        private static void Mix(ref ulong hash, string value) {
            Mix(ref hash, (ulong)(value?.Length ?? -1));
            if (value != null)
                foreach (char c in value)
                    Mix(ref hash, c);
        }

        private static void Add(ref ulong hash, Object target, HashSet<Object> visited, int depth) {
            using SerializedObject serialized = new(target);
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren)) {
                enterChildren = true;
                switch (property.propertyType) {
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.ArraySize:
                        if (property.name == HashField) {
                            enterChildren = false;
                            continue;
                        }
                        Mix(ref hash, (ulong)property.longValue);
                        break;
                    case SerializedPropertyType.Boolean:
                        Mix(ref hash, property.boolValue ? 1UL : 2UL);
                        break;
                    case SerializedPropertyType.Float:
                        Mix(ref hash, (ulong)System.BitConverter.DoubleToInt64Bits(property.doubleValue));
                        break;
                    case SerializedPropertyType.Enum:
                        Mix(ref hash, (ulong)property.intValue);
                        break;
                    case SerializedPropertyType.String:
                        Mix(ref hash, property.stringValue);
                        break;
                    case SerializedPropertyType.ObjectReference:
                        AddReference(ref hash, property.objectReferenceValue, visited, depth);
                        enterChildren = false;
                        break;
                }
            }
        }

        private static void AddReference(ref ulong hash, Object reference, HashSet<Object> visited, int depth) {
            if (!reference) {
                Mix(ref hash, 3UL);
                return;
            }

            if (!EditorUtility.IsPersistent(reference) ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(reference, out string guid, out long localId)) {
                Mix(ref hash, 4UL);
                return;
            }

            Mix(ref hash, guid);
            Mix(ref hash, (ulong)localId);
            if (reference is ScriptableObject && depth < MaxDepth && visited.Add(reference))
                Add(ref hash, reference, visited, depth + 1);
        }
#endif
    }
}
