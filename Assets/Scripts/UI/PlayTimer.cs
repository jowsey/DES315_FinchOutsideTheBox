using Mirror;
using TMPro;
using UnityEngine;

namespace UI
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class PlayTimer : MonoBehaviour
    {
        private TextMeshProUGUI _timerText;

        private void Awake()
        {
            _timerText = GetComponent<TextMeshProUGUI>();
        }
        
        private void Update()
        {
            var networkTime = NetworkTime.time;
            var timeSpan = System.TimeSpan.FromSeconds(networkTime);
            _timerText.text = timeSpan.ToString(@"hh\:mm\:ss\.ff");
        }
    }
}
