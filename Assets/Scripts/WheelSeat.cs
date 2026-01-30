using Sirenix.OdinInspector;
using UnityEngine;

public class WheelSeat : Mirror.NetworkBehaviour
{
    [Header("Ball Properties")]
    [Tooltip("How much driving force the wheel applies")]
    [SerializeField] private float _moveForce = 100f;

    [Tooltip("Cooldown time after player leaves before a player can sit again")]
    [SerializeField] private float _sitCooldown = 2.0f;

    [Header("Components")]
    [Tooltip("The parent cart's rigidbody")]
    [SerializeField] [RequiredIn(PrefabKind.InstanceInScene)] [DisableIn(PrefabKind.Regular)] private Rigidbody _cartRb;

    [Tooltip("The rigidbody of the sphere that will rotate")]
    [SerializeField] [Required] private Rigidbody _wheelRb;

    [Tooltip("The joint connecting the wheel to the cart")]
    [SerializeField] [Required] private ConfigurableJoint _wheelJoint;

    [Header("State")]
    [Tooltip("The player currently sitting in this seat")]
    [Mirror.SyncVar(hook = nameof(OnSeatedPlayerChanged))]
    [SerializeField] [ReadOnly] private Mirror.NetworkIdentity _seatedPlayerIdentity;

    private PlayerController _seatedPlayer;
    
    private float _radius;
    private float _lastUnsitTime = -Mathf.Infinity;
    

    [Mirror.Command(requiresAuthority = false)]
    public void CmdTrySitPlayer(Mirror.NetworkIdentity playerIdentity)
    {
        if (_seatedPlayer || Time.time < _lastUnsitTime + _sitCooldown) return;
        _seatedPlayerIdentity = playerIdentity; //synced to all clients
    }

    [Mirror.Command(requiresAuthority = false)]
    public void CmdUnsitPlayer()
    {
        if (!_seatedPlayer) return;
        _seatedPlayerIdentity = null; //synced to all clients
    }

    private void OnSeatedPlayerChanged(Mirror.NetworkIdentity oldValue, Mirror.NetworkIdentity newValue)
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

        if (_wheelJoint && _cartRb && !_wheelJoint.connectedBody)
        {
            _wheelJoint.connectedBody = _cartRb;
        }

        _radius = GetComponentInChildren<SphereCollider>().radius;
    }

    private void FixedUpdate()
    {
        if (!_seatedPlayer) return;

        var wheelTop = transform.position + Vector3.up * (_radius * transform.lossyScale.y);

        //Only apply force on server
        if (isServer)
        {
            _wheelRb.AddForceAtPosition(_seatedPlayer.WorldSpaceMoveDir * _moveForce, wheelTop);
        }

        _seatedPlayer.Rb.MovePosition(wheelTop);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _radius * transform.lossyScale.y);
    }
}