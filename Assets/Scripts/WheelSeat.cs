using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

public class WheelSeat : MonoBehaviour
{
    [Tooltip("How much driving force the wheel applies")]
    [SerializeField] private float _moveForce = 100f;

    [Tooltip("Radius of the wheel")]
    [SerializeField] private float _radius = 0.5f;

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
    [ReadOnly] public PlayerController OwnedPlayer;

    private float _lastUnsitTime = -Mathf.Infinity;
    private ConfigurableJoint playerJoint;

    private void OnValidate()
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
    }

    private void FixedUpdate()
    {
        if (!OwnedPlayer) return;

        var wheelTop = transform.position + Vector3.up * (_radius * transform.lossyScale.y);
        _wheelRb.AddForceAtPosition(OwnedPlayer.WorldSpaceMoveDir * _moveForce, wheelTop);
        playerJoint.connectedBody = null;
        OwnedPlayer.transform.position = wheelTop;
        playerJoint.connectedBody = _cartRb;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _radius * transform.lossyScale.y);
    }

    public bool TrySitPlayer(PlayerController player)
    {
        if (OwnedPlayer || Time.time < _lastUnsitTime + _sitCooldown) return false;

        OwnedPlayer = player;
        player.transform.SetParent(_cartRb.transform);
        player.Rb.excludeLayers |= 1 << gameObject.layer;
        player.transform.position = transform.position + transform.up * (_radius * transform.lossyScale.y);

        playerJoint = player.gameObject.AddComponent<ConfigurableJoint>();
        playerJoint.connectedBody = _cartRb;
        playerJoint.xMotion = ConfigurableJointMotion.Locked;
        playerJoint.yMotion = ConfigurableJointMotion.Locked;
        playerJoint.zMotion = ConfigurableJointMotion.Locked;
        playerJoint.angularXMotion = ConfigurableJointMotion.Free;
        playerJoint.angularYMotion = ConfigurableJointMotion.Free;
        playerJoint.angularZMotion = ConfigurableJointMotion.Free;

        return true;
    }

    public void UnsitPlayer()
    {
        if (!OwnedPlayer) return;

        OwnedPlayer.transform.SetParent(null);
        OwnedPlayer.Rb.excludeLayers &= ~(1 << gameObject.layer);

        if (OwnedPlayer.TryGetComponent(out Joint joint))
        {
            Destroy(joint);
        }

        OwnedPlayer = null;

        _lastUnsitTime = Time.time;
    }
}