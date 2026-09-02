using UnityEngine;
using UnityEngine.UI;

namespace UI.Menus
{
#if DEV_KEYS || UNITY_EDITOR
    public class DebugSettings : MonoBehaviour
    {
        [field: SerializeField] public Toggle EnableDebugKeysToggle { get; private set; }
        [field: SerializeField] public Slider StartingCheckpointSlider { get; private set; }
    }
#endif
}