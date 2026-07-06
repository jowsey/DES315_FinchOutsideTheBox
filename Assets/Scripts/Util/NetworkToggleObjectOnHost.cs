using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Util
{
    public class NetworkToggleObjectOnHost : NetworkBehaviour
    {
        [SerializeField] private InputActionReference _toggleAction;
        [SerializeField] private GameObject _object;
        
        private void Update()
        {
            if (!_object || !isServer) return;

            if (_toggleAction.action.WasPressedThisFrame())
            {
                RpcToggle();
            }
        }

        [ClientRpc]
        private void RpcToggle()
        {
            _object.SetActive(!_object.activeSelf);
        }
    }
}