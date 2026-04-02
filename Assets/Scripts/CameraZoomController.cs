using Mirror;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraZoomController : MonoBehaviour
{
    [SerializeField] private InputActionReference _zoomAction;
    [SerializeField] private CinemachineOrbitalFollow _orbitalFollow;
    [SerializeField] private float _minThirdPersonRadius;
    [SerializeField] private float _maxThirdPersonRadius;
    [SerializeField] [PropertyRange("_minThirdPersonRadius", "_maxThirdPersonRadius")] private float _defaultZoom;
    [SerializeField] private float _zoomSpeed = 4f;
    [SerializeField] private float _smoothSpeed = 10f;
    private float _targetRadius;

    [SerializeField] private InputActionReference _changePerspectiveAction;
    [SerializeField] private CinemachineBrain _cinemachineBrain;
    [SerializeField] private Camera _camera;
    
    public bool FirstPerson { get; private set; }
    [SerializeField] private float _minFirstPersonFOV;
    [SerializeField] private float _maxFirstPersonFOV;
    [SerializeField][PropertyRange("_minFirstPersonFOV", "_maxFirstPersonFOV")] private float _defaultFOV;
    [SerializeField] private float _fovZoomSpeed = 4f;
    [SerializeField] private float _fovSmoothSpeed = 10f;
    private float _targetFOV;

    private void OnValidate()
    {
        _defaultZoom = Mathf.Clamp(_defaultZoom, _minThirdPersonRadius, _maxThirdPersonRadius);
    }

    private void Start()
    {
        FirstPerson = false;
        _targetRadius = _defaultZoom;
        _orbitalFollow.Radius = _targetRadius;
        _targetFOV = _defaultFOV;
        _camera.fieldOfView = _defaultFOV;
    }

    private void Update()
    {
        if (PlayerController.ControlsEnabled && _changePerspectiveAction.action.WasPressedThisFrame())
        {
            FirstPerson = !FirstPerson;
            _cinemachineBrain.enabled = !FirstPerson;

            foreach (Renderer r in NetworkClient.localPlayer.GetComponentsInChildren<Renderer>())
            {
                r.enabled = !FirstPerson;
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