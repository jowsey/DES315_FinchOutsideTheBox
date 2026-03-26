using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraZoomController : MonoBehaviour
{
    [SerializeField] private InputActionReference _zoomAction;
    [SerializeField] private CinemachineOrbitalFollow _orbitalFollow;

    [SerializeField] private float _minThirdPersonRadius = 3f;
    [SerializeField] private float _maxThirdPersonRadius = 12f;
    [SerializeField] [PropertyRange("_minThirdPersonRadius", "_maxThirdPersonRadius")] private float _defaultZoom;

    [SerializeField] private float _zoomSpeed = 4f;
    [SerializeField] private float _smoothSpeed = 10f;

    private float _targetRadius;

    private void OnValidate()
    {
        _defaultZoom = Mathf.Clamp(_defaultZoom, _minThirdPersonRadius, _maxThirdPersonRadius);
    }

    private void Start()
    {
        _targetRadius = _defaultZoom;
        _orbitalFollow.Radius = _targetRadius;
    }

    private void Update()
    {
        var zoom = _zoomAction.action.ReadValue<float>();

        if (PlayerController.ControlsEnabled && zoom != 0)
        {
            // mouse scroll isn't continuous so it shouldn't be deltaTime'd
            var isDeviceMouse = _zoomAction.action.activeControl?.device is Mouse;

            _targetRadius = Mathf.Clamp(
                _targetRadius - zoom * _zoomSpeed * (isDeviceMouse ? 1 : Time.deltaTime),
                _minThirdPersonRadius,
                _maxThirdPersonRadius
            );
        }

        _orbitalFollow.Radius = Mathf.Lerp(_orbitalFollow.Radius, _targetRadius, _smoothSpeed * Time.deltaTime);
    }
}