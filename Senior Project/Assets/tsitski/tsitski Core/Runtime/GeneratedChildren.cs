using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.Core {
    public interface IGenerated {
        MonoBehaviour generator { get; set; }
    }

    public static class GeneratedChildren {
        public static bool Resize<T>(MonoBehaviour owner, List<T> items, int wanted, string name, bool addCollider = true)
            where T : MonoBehaviour, IGenerated {
            bool changed = items.RemoveAll(item => !item) > 0;
            foreach (Transform child in owner.transform)
                if (child.TryGetComponent(out T stray) && stray.generator == owner && !items.Contains(stray)) {
                    items.Add(stray);
                    changed = true;
                }

            while (items.Count > wanted) {
                T extra = items[^1];
                items.RemoveAt(items.Count - 1);
                if (Application.isPlaying)
                    Object.Destroy(extra.gameObject);
                else
                    Object.DestroyImmediate(extra.gameObject);
                changed = true;
            }

            while (items.Count < wanted) {
                GameObject created = new(name) { layer = owner.gameObject.layer };
                created.transform.SetParent(owner.transform, false);
                TsitskiSettings.ApplyDefaultStaticFlags(created);

                TsitskiArray.suppressReset = true;
                T item;
                try {
                    item = created.AddComponent<T>();
                }
                finally {
                    TsitskiArray.suppressReset = false;
                }

                item.generator = owner;
                if (addCollider && !created.TryGetComponent(out MeshCollider _))
                    created.AddComponent<MeshCollider>();
                items.Add(item);
                changed = true;
            }

#if UNITY_EDITOR
            if (changed)
                EditorUtility.SetDirty(owner);
#endif
            return changed;
        }

        public static List<T> Resize<T>(MonoBehaviour owner, List<MonoBehaviour> store, int wanted, string name, bool addCollider = true)
            where T : MonoBehaviour, IGenerated {
            List<T> typed = store.OfType<T>().ToList();
            Resize(owner, typed, wanted, name, addCollider);
            store.RemoveAll(item => !item || item is T);
            store.AddRange(typed);
            return typed;
        }

        public static bool CollectRemoved<T>(List<T> items, List<string> keys, List<string> removed) where T : Object {
            bool changed = false;
            for (int i = Mathf.Min(items.Count, keys.Count) - 1; i >= 0; i--) {
                if (items[i])
                    continue;

                if (!removed.Contains(keys[i]))
                    removed.Add(keys[i]);
                items.RemoveAt(i);
                keys.RemoveAt(i);
                changed = true;
            }

            return changed;
        }

        public static void Place(MonoBehaviour owner, Component item, Vector3 position, Quaternion rotation) {
            Transform itemTransform = item.transform;
            if (itemTransform.parent != owner.transform)
                itemTransform.SetParent(owner.transform, false);
            itemTransform.localPosition = position;
            itemTransform.localRotation = rotation;
            itemTransform.localScale = Vector3.one;
        }

        public static void MarkDirty(Component item) {
#if UNITY_EDITOR
            EditorUtility.SetDirty(item);
            EditorUtility.SetDirty(item.transform);
#endif
        }
    }
}
