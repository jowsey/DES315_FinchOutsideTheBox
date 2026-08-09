using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Items.Equipments
{
    public class PlaceablePreview : MonoBehaviour
    {
        [field: SerializeField] public Renderer[] ColouredRenderers { get; private set; }

        [Button("Assign all child renderers as coloured")]
        private void AssignAllColoured()
        {
            ColouredRenderers = GetComponentsInChildren<Renderer>();
        }
    }
}