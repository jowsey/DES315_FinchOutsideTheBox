using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class PositionalPing : NetworkBehaviour
{
    [SerializeField] private InputActionReference _pingAction;
    [SerializeField] private GameObject _pingPrefab;

    private Camera _camera;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void Update()
    {
        if (_pingAction.action.WasPressedThisFrame())
        {
            var ray = _camera.ViewportPointToRay(new Vector2(0.5f, 0.5f));
            if (Physics.Raycast(ray, out var hit, 100, ~0, QueryTriggerInteraction.Ignore))
            {
                const float visualOffset = 0.25f;
                CmdPingPosition(hit.point + hit.normal * visualOffset, -hit.normal);
            }
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdPingPosition(Vector3 position, Vector3 normal)
    {
        RpcPingPosition(position, normal);
    }

    [ClientRpc]
    private void RpcPingPosition(Vector3 position, Vector3 normal)
    {
        Instantiate(_pingPrefab, position, Quaternion.LookRotation(normal));
    }
}