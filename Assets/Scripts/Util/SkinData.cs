using Sirenix.OdinInspector;
using UnityEngine;

namespace Util
{
    [CreateAssetMenu(fileName = "SkinData", menuName = "Skins/Skin Data")]
    public class SkinData : ScriptableObject
    {
        [PreviewField] public Material Material;
        [PreviewField] public Sprite Icon;
        [PreviewField] public Sprite VCIcon;
        public Color AccentColor = Color.white;
    }
}