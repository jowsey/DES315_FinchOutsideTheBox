using Mirror;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(CinemachineBrain))]
[RequireComponent(typeof(ObstructionDitherer))]
public class CameraZoomController : MonoBehaviour
{
    [SerializeField] private InputActionReference _changePerspectiveAction;
    [SerializeField] private InputActionReference _zoomAction;
    [SerializeField] private CinemachineOrbitalFollow _orbitalFollow;

    private Camera _camera;
    private CinemachineBrain _cinemachineBrain;
    private ObstructionDitherer _obstructionDitherer;

    // Third-person
    [SerializeField] private float _minThirdPersonRadius;
    [SerializeField] private float _maxThirdPersonRadius;
    [SerializeField] [PropertyRange("_minThirdPersonRadius", "_maxThirdPersonRadius")] private float _defaultZoom;
    [SerializeField] private float _zoomSpeed = 4f;
    [SerializeField] private float _smoothSpeed = 10f;
    private float _targetRadius;

    // First-person
    [SerializeField] private float _minFirstPersonFOV;
    [SerializeField] private float _maxFirstPersonFOV;
    [SerializeField] [PropertyRange("_minFirstPersonFOV", "_maxFirstPersonFOV")] private float _defaultFOV;
    [SerializeField] private float _fovZoomSpeed = 4f;
    [SerializeField] private float _fovSmoothSpeed = 10f;
    private float _targetFOV;

    public static bool FirstPerson { get; private set; }

    private Transform _targetTransform => _orbitalFollow.FollowTarget;

    private void OnValidate()
    {
        _defaultZoom = Mathf.Clamp(_defaultZoom, _minThirdPersonRadius, _maxThirdPersonRadius);
    }

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _cinemachineBrain = GetComponent<CinemachineBrain>();
        _obstructionDitherer = GetComponent<ObstructionDitherer>();
    }

    private void Start()
    {
        FirstPerson = false;
        _targetRadius = _defaultZoom;
        _orbitalFollow.Radius = _targetRadius;

        _targetFOV = _defaultFOV;
        _camera.fieldOfView = _targetFOV;
    }

    private void Update()
    {
        if (PlayerController.ControlsEnabled && _changePerspectiveAction.action.WasPressedThisFrame())
        {
            FirstPerson = !FirstPerson;
            _cinemachineBrain.enabled = !FirstPerson;

            foreach (Renderer r in _targetTransform.GetComponentsInChildren<Renderer>())
            {
                r.enabled = !FirstPerson;
            }

            if (FirstPerson)
            {
                _camera.fieldOfView = _targetFOV;
                _targetTransform.rotation = Quaternion.Euler(0, _camera.transform.eulerAngles.y, 0);
                // todo set _pitch, todo-todo: consolidate camera stuff under one roof (i.e. Here)

                _obstructionDitherer.RemoveAllActiveDithers();
            }
            else
            {
                _orbitalFollow.Radius = _targetRadius;
                _orbitalFollow.HorizontalAxis.Value = _camera.transform.eulerAngles.y;
                // _orbitalFollow.VerticalAxis.Value = // todo, doesn't match up if you pull from camera
                _orbitalFollow.VirtualCamera.PreviousStateIsValid = false;
            }
        }

        var zoom = _zoomAction.action.ReadValue<float>();
        if (PlayerController.ControlsEnabled && zoom != 0)
        {
            // mouse scroll isn't continuous so it shouldn't be deltaTime'd
            var isDeviceMouse = _zoomAction.action.activeControl?.device is Mouse;

            if (FirstPerson)
            {
                _targetFOV = Mathf.Clamp(
                    _targetFOV - zoom * _fovZoomSpeed * (isDeviceMouse ? 1 : Time.deltaTime),
                    _minFirstPersonFOV,
                    _maxFirstPersonFOV
                );
            }
            else
            {
                _targetRadius = Mathf.Clamp(
                    _targetRadius - zoom * _zoomSpeed * (isDeviceMouse ? 1 : Time.deltaTime),
                    _minThirdPersonRadius,
                    _maxThirdPersonRadius
                );
            }
        }

        if (FirstPerson)
        {
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, _targetFOV, _fovSmoothSpeed * Time.deltaTime);
        }
        else
        {
            _orbitalFollow.Radius = Mathf.Lerp(_orbitalFollow.Radius, _targetRadius, _smoothSpeed * Time.deltaTime);
        }
    }
}