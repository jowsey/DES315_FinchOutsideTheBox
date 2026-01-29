using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(ConfigurableJoint))]
public class Flask : NetworkBehaviour
{
    [SerializeField] private float _rimHeight = 1.0f;
    [SerializeField] private float _rimRadius = 0.5f;
    [SerializeField] private float _maxLiquid = 100f;
    [SerializeField] [Required] private Transform _liquidPlane;

    [Header("Spilling")]
    [Tooltip("Multiplier on how quickly liquid spills out when over the rim")]
    [SerializeField] private float _spillSpeed = 30f;

    [Header("Sloshing")]
    [Tooltip("Higher values reduce how much the liquid sloshes around")]
    [SerializeField] private float _sloshStiffness = 5f;

    [Tooltip("Higher values slow down the sloshing motion")]
    [SerializeField] private float _sloshDamping = 2f;

    [Tooltip("Maximum angle the liquid can slosh to")]
    [SerializeField] [SuffixLabel("degrees")] private float _maxSloshAngle = 5f;

    [Header("State")]
    [SerializeField] [PropertyRange(0, "_maxLiquid")] [SyncVar] private float _storedLiquid;

    [SerializeField] [Mirror.ReadOnly] private Vector2 _sloshAngle;
    [SerializeField] [Mirror.ReadOnly] private Vector2 _sloshVelocity;

    [SerializeField] [Required] private ParticleSystem _spillEffect;

    private ConfigurableJoint _joint;
    private Cart _cart;

    private void Awake()
    {
        _joint = GetComponent<ConfigurableJoint>();
        _cart = GetComponentInParent<Cart>();
    }

    protected override void OnValidate()
    {
        _storedLiquid = _maxLiquid;

        _liquidPlane.localPosition = new Vector3(0, _rimHeight, 0);
        _liquidPlane.localScale = new Vector3(_rimRadius * 2f, 0.01f, _rimRadius * 2f);
    }

    private Vector3 GetLowestRimPoint()
    {
        var localDown = transform.InverseTransformDirection(Vector3.down);
        var angleRad = Mathf.Atan2(localDown.x, localDown.z);
        var rimPointLocal = new Vector3(
            Mathf.Sin(angleRad) * _rimRadius,
            _rimHeight,
            Mathf.Cos(angleRad) * _rimRadius
        );
        return transform.TransformPoint(rimPointLocal);
    }

    private void LateUpdate()
    {
        // todo look into some kind of basic low-res fluid sim & shader graph for this

        // data
        var localDown = transform.InverseTransformDirection(Vector3.down);
        var liquidTargetAngle = new Vector2(
            Mathf.Atan2(-localDown.z, -localDown.y) * Mathf.Rad2Deg,
            Mathf.Atan2(localDown.x, -localDown.y) * Mathf.Rad2Deg
        );
        liquidTargetAngle = Vector2.ClampMagnitude(liquidTargetAngle, _maxSloshAngle);

        var sloshForce = (liquidTargetAngle - _sloshAngle) * _sloshStiffness - _sloshVelocity * _sloshDamping;
        _sloshVelocity += sloshForce * Time.deltaTime;
        _sloshAngle += _sloshVelocity * Time.deltaTime;

        var tiltMagnitudeRad = _sloshAngle.magnitude * Mathf.Deg2Rad;
        var tiltRise = _rimRadius * Mathf.Tan(tiltMagnitudeRad);
        var highestPointY = _liquidPlane.localPosition.y + tiltRise;

        var overflowedY = highestPointY - _rimHeight;
        if (isServer && _storedLiquid > 0 && overflowedY > 0)
        {
            var spillAmount = overflowedY / _rimRadius * _spillSpeed * Time.deltaTime;
            _storedLiquid = Mathf.Max(0, _storedLiquid - spillAmount);
        }

        // visual
        var fillPercent = Mathf.Clamp01(_storedLiquid / _maxLiquid);
        var liquidHeight = fillPercent * _rimHeight;
        _liquidPlane.localPosition = new Vector3(0, liquidHeight, 0);
        _liquidPlane.localRotation = Quaternion.Euler(_sloshAngle.x, 0, _sloshAngle.y);

        var tiltX = Mathf.Abs(_sloshAngle.x) * Mathf.Deg2Rad;
        var tiltZ = Mathf.Abs(_sloshAngle.y) * Mathf.Deg2Rad;
        var scaleX = 1f / Mathf.Max(Mathf.Cos(tiltZ), 0.0001f);
        var scaleZ = 1f / Mathf.Max(Mathf.Cos(tiltX), 0.0001f);
        _liquidPlane.localScale = new Vector3(
            _rimRadius * 2f * scaleX,
            _liquidPlane.localScale.y,
            _rimRadius * 2f * scaleZ
        );

        _spillEffect.transform.position = GetLowestRimPoint();
        var emission = _spillEffect.emission;
        emission.enabled = overflowedY > 0 && _storedLiquid > 0;
    }

    private void FixedUpdate()
    {
        // always try face world-upwards
        // _joint.targetRotation = Quaternion.Inverse(_cart.Rb.rotation); // for some reason the joint comes pre-rotated? god knows
        if (isServer)
        {
            _joint.targetRotation = _cart.Rb.rotation;
        }
    }

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        using (new UnityEditor.Handles.DrawingScope())
        {
            UnityEditor.Handles.color = Color.green;
            UnityEditor.Handles.DrawWireDisc(transform.position + transform.up * _rimHeight, transform.up, _rimRadius);
        }
#endif
    }
}