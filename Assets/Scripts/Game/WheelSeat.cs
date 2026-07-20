using Game.Items;
using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;

public class WheelSeat : NetworkBehaviour
{
    [Header("Ball Properties")]
    [Tooltip("How much driving force the wheel applies")]
    [SyncVar] public float MoveForce = 250f;

    [Tooltip("Cooldown time after player leaves before a player can sit again")]
    [SerializeField] private float _sitCooldown = 2.0f;

    [Header("Components")]
    [Tooltip("The parent cart's rigidbody")]
    [SerializeField] [RequiredIn(PrefabKind.InstanceInScene)] [DisableIn(PrefabKind.Regular)] private Rigidbody _cartRb;

    [Tooltip("The rigidbody of the sphere that will rotate")]
    [SerializeField] [Required] private Rigidbody _wheelRb;

    [Tooltip("The joint connecting the wheel to the cart")]
    [SerializeField] [Required] private ConfigurableJoint _wheelJoint;

    [SerializeField] private Cart _cart;
    
    [Header("State")]
    [Tooltip("The player currently sitting in this seat")]
    [SyncVar(hook = nameof(OnSeatedPlayerChanged))]
    [SerializeField] [Sirenix.OdinInspector.ReadOnly] private NetworkIdentity _seatedPlayerIdentity;

    public PlayerController SeatedPlayer { get; private set; }
    
    [SerializeField] [Required] private SphereCollider _sphereCollider;
    
    private float _lastUnsitTime = -Mathf.Infinity;

    public Vector3 SeatedPosition => transform.position + Vector3.up * (_sphereCollider.radius * transform.lossyScale.y);

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isServer)
        {
            _wheelJoint.connectedBody = null; // don't use joints on client
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdTrySitPlayer(NetworkConnectionToClient sender = null)
    {
        if (SeatedPlayer || Time.time < _lastUnsitTime + _sitCooldown) return;
        
        var player = sender!.identity.GetComponent<PlayerController>();
        if (player.HeldObject?.State == Item.ItemState.Held) return; // dont allow sitting while holding an item
        
        _seatedPlayerIdentity = player.netIdentity; //synced to all clients
    }

    [Command(requiresAuthority = false)]
    public void CmdUnsitPlayer()
    {
        if (!SeatedPlayer) return;
        _seatedPlayerIdentity = null; //synced to all clients
    }

    private void OnSeatedPlayerChanged(NetworkIdentity oldValue, NetworkIdentity newValue)
    {
        PlayerController oldPlayer = SeatedPlayer;
        SeatedPlayer = newValue ? newValue.GetComponent<PlayerController>() : null;

        if (oldPlayer)
        {
            //Player is getting off
            oldPlayer.Rb.isKinematic = false;
            oldPlayer.Rb.angularVelocity = Vector3.zero;
            oldPlayer.Rb.excludeLayers &= ~(1 << gameObject.layer);
            oldPlayer.Seat = null;

            _lastUnsitTime = Time.time;

            if (oldPlayer.isLocalPlayer)
            {
                Highlight.SetHighlightable("Item", true);
            }
        }

        if (SeatedPlayer)
        {
            //Player is getting on
            SeatedPlayer.Rb.isKinematic = true;
            SeatedPlayer.Rb.excludeLayers |= 1 << gameObject.layer;
            SeatedPlayer.Seat = this;

            if (SeatedPlayer.isLocalPlayer)
            {
                Highlight.SetHighlightable("Item", false);
            }
        }
    }

    protected override void OnValidate()
    {
        if (!_wheelRb)
        {
            _wheelRb = GetComponentInChildren<Rigidbody>();
        }

        if (!_wheelJoint)
        {
            _wheelJoint = GetComponentInChildren<ConfigurableJoint>();
        }

        if (!_cartRb)
        {
            _cartRb = GetComponentInParent<Rigidbody>();
        }

        if (!_cart)
        {
            _cart = GetComponentInParent<Cart>();
        }

        if (_wheelJoint && _cartRb && !_wheelJoint.connectedBody)
        {
            _wheelJoint.connectedBody = _cartRb;
        }
        
        if (!_sphereCollider)
        {
            _sphereCollider = GetComponentInChildren<SphereCollider>();
        }
    }

    private void FixedUpdate()
    {
        if (!isServer) return;
        if (!SeatedPlayer) return;
        
        var torqueAxis = Vector3.Cross(Vector3.up, SeatedPlayer.WorldSpaceMoveDir);
        _wheelRb.AddTorque(torqueAxis * (MoveForce * SeatedPlayer.AnalogueMoveScale));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _sphereCollider.radius * transform.lossyScale.y);
    }

    public void ApplyDrive(Vector3 worldMoveDir, float scale)
    {
        Vector3 torqueAxis = Vector3.Cross(Vector3.up, worldMoveDir.normalized);
        _wheelRb.WakeUp();
        _wheelRb.AddTorque(torqueAxis * (MoveForce * scale));
    }
}