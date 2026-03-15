using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Tools
{
    public class Cleaner : MonoBehaviour
    {
        [ShowInInspector] private int _numEmpty;

        [Button("Count empty")]
        public void CountEmpty()
        {
            _numEmpty = GetComponentsInChildren<Transform>()
                .Count(child => child.childCount == 0 && child.GetComponents<Component>().Length == 1);
        }

        [Button("Cleanup empty in hierarchy")]
        public void CleanEmpty()
        {
#if UNITY_EDITOR
            var allTransforms = GetComponentsInChildren<Transform>();
            
            List<GameObject> markedForDeletion = new();

            // If object has no children and no components, mark for deletion
            foreach (var child in allTransforms)
            {
                if (child.childCount == 0 && child.GetComponents<Component>().Length == 1)
                {
                    markedForDeletion.Add(child.gameObject);
                }
            }

            foreach (var obj in markedForDeletion)
            {
                UnityEditor.Undo.DestroyObjectImmediate(obj);
            }
#endif
        }
    }
}