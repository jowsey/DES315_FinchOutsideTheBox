using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Tools
{
    public class Cleaner : MonoBehaviour
    {
#if UNITY_EDITOR
        [ShowInInspector] private int _numEmpty;

        private bool ShouldMarkTransform(Transform t) => t.childCount == 0 &&
                                                         t.GetComponents<Component>().Length == 1 &&
                                                         !UnityEditor.PrefabUtility.IsPartOfPrefabInstance(t.gameObject);

        [Button("Count empty")]
        public void CountEmpty()
        {
            _numEmpty = GetComponentsInChildren<Transform>().Count(ShouldMarkTransform);
        }

        [Button("Cleanup empty in hierarchy")]
        public void CleanEmpty()
        {
            var allTransforms = GetComponentsInChildren<Transform>();

            List<GameObject> markedForDeletion = new();

            // If object has no children and no components, and is not part of a prefab instace, mark for deletion
            foreach (var child in allTransforms)
            {
                if (ShouldMarkTransform(child))
                {
                    markedForDeletion.Add(child.gameObject);
                }
            }

            foreach (var obj in markedForDeletion)
            {
                UnityEditor.Undo.DestroyObjectImmediate(obj);
            }
        }
#endif
    }
}