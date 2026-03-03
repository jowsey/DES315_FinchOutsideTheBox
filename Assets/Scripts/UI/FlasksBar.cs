using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class FlasksBar : MonoBehaviour
    {
        [SerializeField] private FlaskCarrier _linkedFlaskCarrier;
        [SerializeField] private TextMeshProUGUI _countText;
        [SerializeField] private Image _fillImage;

        private void OnValidate()
        {
            if (!_linkedFlaskCarrier)
            {
                _linkedFlaskCarrier = FindAnyObjectByType<FlaskCarrier>();
            }
        }
        
        private void Update()
        {
            if (!_linkedFlaskCarrier) return;
            
            _fillImage.fillAmount = _linkedFlaskCarrier.FlasksRemainingRatio;
            _countText.text = $"{_linkedFlaskCarrier.CarriedFlasks} / {_linkedFlaskCarrier.MaxFlasks}";
        }
    }
}