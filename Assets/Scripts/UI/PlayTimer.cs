using System;
using Mirror;
using TMPro;
using UnityEngine;

namespace UI
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class PlayTimer : NetworkBehaviour
    {
        private TextMeshProUGUI _timerText;
        [SyncVar] private long _sessionStartTime;

        private void Awake()
        {
            _timerText = GetComponent<TextMeshProUGUI>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _sessionStartTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        private void Update()
        {
            var timeElapsed = DateTimeOffset.Now.ToUnixTimeMilliseconds() - _sessionStartTime;
            var timeSpan = TimeSpan.FromMilliseconds(timeElapsed);
            _timerText.text = timeSpan.ToString(@"hh\:mm\:ss\.ff");
        }
    }
}