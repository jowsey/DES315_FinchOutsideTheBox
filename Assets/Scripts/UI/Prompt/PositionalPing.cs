using Mirror;
using UI;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class PositionalPing : NetworkBehaviour
{
    [SerializeField] private InputActionReference _pingAction;
    [SerializeField] private PingObject _pingPrefab;

    private Camera _camera;

    public AK.Wwise.Event catMeow;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void Update()
    {
        if (PlayerController.ControlEnabled(PlayerController.ControlBlockerFlags.Ping) && _pingAction.action.WasPressedThisFrame())
        {
            var ray = _camera.ViewportPointToRay(new Vector2(0.5f, 0.5f));
            if (Physics.Raycast(ray, out var hit, 100, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore))
            {
                const float visualOffset = 0.25f;
                CmdPingPosition(hit.point + hit.normal * visualOffset, -hit.normal);
            }
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdPingPosition(Vector3 position, Vector3 direction, NetworkConnectionToClient sender = null)
    {
        RpcPingPosition(position, direction, sender?.identity);
    }

    [ClientRpc]
    private void RpcPingPosition(Vector3 position, Vector3 direction, NetworkIdentity sender)
    {
        var ping = Instantiate(_pingPrefab, position, Quaternion.LookRotation(direction));

        Transform attachTarget = null;
        
        // Attach to pointed-at object
        if (Physics.Raycast(position, direction, out var hit, 1f, ~0, QueryTriggerInteraction.Ignore))
        {
            attachTarget = hit.transform;
        }
        
        ping.Build(sender.GetComponent<PlayerController>(), attachTarget);

        catMeow.Post(gameObject);
    }
}