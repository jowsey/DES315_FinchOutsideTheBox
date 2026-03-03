using Mirror;
using TMPro;
using UnityEngine;

namespace UI
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class PlayTimer : NetworkBehaviour
    {
        private TextMeshProUGUI _timerText;
        [SyncVar] private float _sessionStartTime;

        private void Awake()
        {
            _timerText = GetComponent<TextMeshProUGUI>();
        }
        
        private void Update()
        {
            var networkTime = Time.time - _sessionStartTime;
            var timeSpan = System.TimeSpan.FromSeconds(networkTime);
            _timerText.text = timeSpan.ToString(@"hh\:mm\:ss\.ff");
        }
    }
}
