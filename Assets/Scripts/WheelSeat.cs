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

    private PlayerController _seatedPlayer;
    
    [SerializeField] [Required] private SphereCollider _sphereCollider;
    
    private float _lastUnsitTime = -Mathf.Infinity;

    public Vector3 SeatedPosition => transform.position + Vector3.up * (_sphereCollider.radius * transform.lossyScale.y);

    [Command(requiresAuthority = false)]
    public void CmdTrySitPlayer(NetworkIdentity playerIdentity)
    {
        if (_seatedPlayer || Time.time < _lastUnsitTime + _sitCooldown) return;
        _seatedPlayerIdentity = playerIdentity; //synced to all clients
    }

    [Command(requiresAuthority = false)]
    public void CmdUnsitPlayer()
    {
        if (!_seatedPlayer) return;
        _seatedPlayerIdentity = null; //synced to all clients
    }

    private void OnSeatedPlayerChanged(NetworkIdentity oldValue, NetworkIdentity newValue)
    {
        PlayerController oldPlayer = _seatedPlayer;
        _seatedPlayer = newValue ? newValue.GetComponent<PlayerController>() : null;
        
        if (_seatedPlayer != null)
        {
            //Player is getting on
            _seatedPlayer.Rb.isKinematic = true;
            _seatedPlayer.Rb.excludeLayers |= 1 << gameObject.layer;
            _seatedPlayer.Seat = this;
        }
        else if (oldPlayer != null)
        {
            //Player is getting off
            oldPlayer.Rb.isKinematic = false;
            oldPlayer.Rb.angularVelocity = Vector3.zero;
            oldPlayer.Rb.excludeLayers &= ~(1 << gameObject.layer);
            oldPlayer.Seat = null;

            _lastUnsitTime = Time.time;
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
        if (!_seatedPlayer) return;
        
        if (_cart.UseNewTorqueSystem)
        {
            var torqueAxis = Vector3.Cross(Vector3.up, _seatedPlayer.WorldSpaceMoveDir);
            _wheelRb.AddTorque(torqueAxis * MoveForce);
        }
        else
        {
            var wheelTop = transform.position + Vector3.up * (_sphereCollider.radius * transform.lossyScale.y);
            _wheelRb.AddForceAtPosition(_seatedPlayer.WorldSpaceMoveDir * MoveForce, wheelTop);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _sphereCollider.radius * transform.lossyScale.y);
    }
}