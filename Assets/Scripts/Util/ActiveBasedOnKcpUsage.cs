using System;
using System.Linq;
using System.Net.NetworkInformation;
using kcp2k;
using Sirenix.OdinInspector;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Util
{
    [InfoBox("Sets the attached button to interactive based on whether the KCP port is in use")]
    public class ActiveBasedOnKcpUsage : MonoBehaviour
    {
        public enum BehaviourType
        {
            Host,
            Join
        }

        [SerializeField] private BehaviourType _behaviourType;
        [SerializeField] private Button _button;
        private float _checkInterval = 1.0f;

        private GloballyLockedButton _globalLock;

        private KcpTransport _kcpTransport;

        private void OnValidate()
        {
            if (!_button) _button = GetComponent<Button>();
        }

        private void Awake()
        {
            _globalLock = _button.GetComponent<GloballyLockedButton>();
        }

        private void Start()
        {
            _kcpTransport = FindAnyObjectByType<KcpTransport>(FindObjectsInactive.Include);
            InvokeRepeating(nameof(Check), 0f, _checkInterval);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(Check));
        }

        private static bool IsUdpPortInUse(ushort port)
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var udpListeners = ipGlobalProperties.GetActiveUdpListeners();

            return udpListeners.Any(listener => listener.Port == port);
        }

        private void Check()
        {
            if (GloballyLockedButton.Locked && _globalLock) return;

            try
            {
                _button.interactable = _behaviourType switch
                {
                    BehaviourType.Host => !IsUdpPortInUse(_kcpTransport.Port),
                    BehaviourType.Join => Menu.DiscoveredServers.Count > 0,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
            catch (NotImplementedException)
            {
                _button.interactable = true;
            }
        }
    }
}