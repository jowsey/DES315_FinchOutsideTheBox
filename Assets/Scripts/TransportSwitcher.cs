using System;
using EpicTransport;
using kcp2k;
using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(NetworkManager))]
public class TransportSwitcher : MonoBehaviour
{
    private enum TransportType
    {
        Eos,
        Kcp
    }

    private TransportType _currentTransport;

    [SerializeField] private EosTransport _eosTransport;

    [SerializeField] private NetworkManager _networkManager;
    [SerializeField] private KcpTransport _kcpTransport;
    [SerializeField] private NetworkManagerHUD _networkManagerHUD;

    private void OnValidate()
    {
        if (!_eosTransport) _eosTransport = FindAnyObjectByType<EosTransport>(FindObjectsInactive.Include);
        if (!_networkManager) _networkManager = GetComponent<NetworkManager>();
        if (!_kcpTransport) _kcpTransport = GetComponent<KcpTransport>();
        if (!_networkManagerHUD) _networkManagerHUD = GetComponent<NetworkManagerHUD>();

        _currentTransport = _networkManager.transport == _eosTransport ? TransportType.Eos : TransportType.Kcp;
    }

    [PropertySpace]
    [DisableInPlayMode]
    [Button("@\"Switch to \" + (_currentTransport == TransportType.Eos ? \"KCP (local play)\" : \"EOS (online play)\")", ButtonSizes.Large)]
    private void Switch()
    {
        if (_currentTransport == TransportType.Eos)
        {
            // Switch to KCP
            _currentTransport = TransportType.Kcp;

            _eosTransport.gameObject.SetActive(false);
            _networkManagerHUD.enabled = true;
            _kcpTransport.enabled = true;
            _networkManager.transport = _kcpTransport;
        }
        else
        {
            // Switch to EOS
            _currentTransport = TransportType.Eos;

            _eosTransport.gameObject.SetActive(true);
            _networkManagerHUD.enabled = false;
            _kcpTransport.enabled = false;
            _networkManager.transport = _eosTransport;
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(_eosTransport);
        UnityEditor.EditorUtility.SetDirty(_kcpTransport);
        UnityEditor.EditorUtility.SetDirty(_networkManager);
#endif
    }
}