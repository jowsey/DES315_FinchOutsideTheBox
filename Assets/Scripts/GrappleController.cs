using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class GrappleController : MonoBehaviour
{
    [Header("Components")]
    [Tooltip("Camera to use for raycasting")]
    [SerializeField] [RequiredIn(PrefabKind.NonPrefabInstance)] private Camera _cam;

    private Rigidbody _rb;

    [Tooltip("Layer of objects that can be grappled to")]
    [SerializeField] private LayerMask grappleableLayer;

    [Tooltip("Origin point of grapple hook")]
    [SerializeField] [Required] private Transform _grappleOrigin;

    [Tooltip("Grappling hook prefab")]
    [SerializeField] [Required] private GameObject _grapplingHookPrefab;

    private GameObject _grapplingHook;

    [Header("Input")]
    [Tooltip("Boolean input action used to grapple")]
    [SerializeField] [Required] private InputActionReference _grappleAction;


    [Header("Settings")]
    [Tooltip("Maximum range at which the player can grapple on to objects")]
    [SerializeField] [Min(1)] [SuffixLabel("m")] private float _range = 100f;

    [Tooltip("Speed of grappling hook")]
    [SerializeField] [Min(1)] [SuffixLabel("m/s")] private float _extendSpeed = 500f;

    [Tooltip("Speed of retracting the grappling hook")]
    [SerializeField] [Min(0)] [SuffixLabel("m/s")] private float _retractSpeed = 50f;

    [Tooltip("Distance at which grappling will end")]
    [SerializeField] [Min(0.1f)] [SuffixLabel("m")] private float _grappleEndDistance = 5f;


    private Transform _highlightedObject;
    private Transform _grappledObject;

    private Vector3 _hitTargetPos;

    private float _extendedDistance;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void CancelGrapple()
    {
        _extendedDistance = 0f;
        _grappledObject = null;
        _hitTargetPos = Vector3.zero;
        if (_grapplingHook)
        {
            Destroy(_grapplingHook);
        }
    }

    private void LateUpdate()
    {
        // If currently grappling, update grapple visuals
        if (_grappledObject)
        {
            if (_grappleAction.action.WasReleasedThisFrame())
            {
                CancelGrapple();
                return;
            }

            // todo temp for pitch, should be fancier but for now we hardcode scaling
            var dir = (_hitTargetPos - _grappleOrigin.position).normalized;

            _grapplingHook.transform.forward = dir;
            _grapplingHook.transform.localScale = new Vector3(1, 1, _extendedDistance);
            return;
        }

        // Aim + find grappleable objects
        if (Physics.Raycast(_cam.transform.position, _cam.transform.forward, out RaycastHit hit, _range, grappleableLayer))
        {
            hit.transform.GetComponent<Renderer>().material.color = Color.red; //todo: temp, replace with a cool shader
            _highlightedObject = hit.transform;

            GlobalEvents.OnGrappleHover.Invoke();
        }
        else
        {
            if (_highlightedObject)
            {
                _highlightedObject.GetComponent<Renderer>().material.color = Color.green; //todo: temp, replace with a cool shader
                _highlightedObject = null;

                GlobalEvents.OnGrappleHoverEnd.Invoke();
            }
        }

        // Start grappling
        if (_highlightedObject && _grappleAction.action.WasPressedThisFrame())
        {
            _grappledObject = _highlightedObject;
            _hitTargetPos = hit.point;

            _grapplingHook = Instantiate(_grapplingHookPrefab, _grappleOrigin);
        }
    }

    private void FixedUpdate()
    {
        if (_hitTargetPos != Vector3.zero)
        {
            var dir = (_hitTargetPos - transform.position);
            var distance = dir.magnitude;
            dir.Normalize();

            var visualDistance = Vector3.Distance(_grappleOrigin.position, _hitTargetPos);
            _extendedDistance += _extendSpeed * Time.fixedDeltaTime;
            _extendedDistance = Mathf.Min(_extendedDistance, visualDistance);

            if (distance < _grappleEndDistance)
            {
                CancelGrapple();
                return;
            }

            var grappleForce = dir * _retractSpeed;
            _rb.AddForce(grappleForce, ForceMode.Acceleration);
        }
    }
}