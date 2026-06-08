using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class TreasuresBar : MonoBehaviour
    {
        [SerializeField] [RequiredIn(PrefabKind.NonPrefabInstance)] private Cart _linkedCart;
        [SerializeField] [Required] private TextMeshProUGUI _countText;
        [SerializeField] [Required] private Image _fillImage;

        private void OnValidate()
        {
            if (!_linkedCart)
            {
                _linkedCart = FindAnyObjectByType<Cart>();
            }
        }

        private void Update()
        {
            //_fillImage.fillAmount = _linkedCart.TreasuresRemainingRatio;
            //_countText.text = $"{_linkedCart.CarriedTreasures} / {_linkedCart.MaxTreasures}";
            _fillImage.fillAmount = 1.0f;
            _countText.text = $"{_linkedCart.NumCarriedTreasures}";
        }
    }
}