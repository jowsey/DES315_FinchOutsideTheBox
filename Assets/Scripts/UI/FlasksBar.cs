using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class FlasksBar : MonoBehaviour
    {
        [SerializeField] [Required] private Cart _linkedCart;
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
            _fillImage.fillAmount = _linkedCart.FlasksRemainingRatio;
            _countText.text = $"{_linkedCart.CarriedFlasks} / {_linkedCart.MaxFlasks}";
        }
    }
}