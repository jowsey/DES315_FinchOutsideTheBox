using Sirenix.OdinInspector;
using UnityEngine;

namespace Util
{
    [InfoBox("Shuffles the order of all children on enable")]
    public class ShuffleChildren : MonoBehaviour
    {
        private void OnEnable()
        {
            foreach (Transform child in transform)
            {
                child.SetSiblingIndex(Random.Range(0, transform.childCount));
            }
        }
    }
}