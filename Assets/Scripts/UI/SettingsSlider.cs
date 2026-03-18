using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [ExecuteAlways]
    [InfoBox("Re-enable after assigning properties to initialize.")]
    public class SettingsSlider : MonoBehaviour
    {
        [SerializeField] [Required] private Slider _slider;
        [SerializeField] [Required] private TextMeshProUGUI _text;
        [SerializeField] private string _prefix;
        [SerializeField] private string _suffix;
        [SerializeField] private int _decimalPlaces = 2;

        private void OnSliderValueChanged(float value)
        {
            _text.text = $"{_prefix}{value.ToString($"F{_decimalPlaces}")}{_suffix}";
        }

        private void OnEnable()
        {
            _slider.onValueChanged.AddListener(OnSliderValueChanged);
            OnSliderValueChanged(_slider.value);
        }

        private void OnDisable()
        {
            _slider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }
    }
}