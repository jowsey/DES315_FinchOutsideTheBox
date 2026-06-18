using Sirenix.OdinInspector;
using UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Rendering;

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

    [SerializeField] private PlayableDirector _director;
    
    //Used for tracking and restoring state for cutscenes, emotes, and shops (all guaranteed to be identical if needed at the same time)
    private bool _firstPersonBeforeAction;
    private float _targetRadiusBeforeAction;

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
        _director.stopped += OnRestorePreActionFirstPersonState;
    }

    private void ToggleFirstPerson(bool toggle)
    {
        FirstPerson = toggle;
        _cinemachineBrain.enabled = !FirstPerson;

        foreach (Renderer r in _targetTransform.GetComponentsInChildren<Renderer>())
        {
            // r.enabled = !FirstPerson;
            r.shadowCastingMode = toggle ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        if (FirstPerson)
        {
            _camera.fieldOfView = SettingsManager.ActiveSettings.FirstPersonFov;
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

    private void Update()
    {
        if (PlayerController.ControlEnabled(PlayerController.ControlBlockerFlags.ChangePerspective) && _changePerspectiveAction.action.WasPressedThisFrame())
        {
            ToggleFirstPerson(!FirstPerson);
        }

        var zoom = _zoomAction.action.ReadValue<float>();
        if (PlayerController.ControlEnabled(PlayerController.ControlBlockerFlags.CameraZoom) && zoom != 0)
        {
            // mouse scroll isn't continuous so it shouldn't be deltaTime'd
            var isDeviceMouse = _zoomAction.action.activeControl?.device is Mouse;

            if (!FirstPerson)
            {
                _targetRadius = Mathf.Clamp(
                    _targetRadius - zoom * _zoomSpeed * (isDeviceMouse ? 1 : Time.deltaTime),
                    _minThirdPersonRadius,
                    _maxThirdPersonRadius
                );
            }
        }

        if (!FirstPerson)
        {
            _orbitalFollow.Radius = Mathf.Lerp(_orbitalFollow.Radius, _targetRadius, _smoothSpeed * Time.deltaTime);
        }
    }

    private void OnDestroy()
    {
        FirstPerson = false;
        _director.stopped += OnRestorePreActionFirstPersonState;
    }


    //todo: i feel like there's a proper dsa for all this stuff below

    //Called by CutsceneStart, Emoter, and Shop
    public void OnForceThirdPersonActionStarted()
    {
        _firstPersonBeforeAction = FirstPerson;
        if (FirstPerson) { ToggleFirstPerson(!FirstPerson); }
    }

    //Called by Emoter and Shop, and callback from director
    public void OnRestorePreActionFirstPersonState(PlayableDirector _ = null)
    {
        if (FirstPerson != _firstPersonBeforeAction) { ToggleFirstPerson(!FirstPerson); }
    }

    //Called by Shop
    public void OnForceMinThirdPersonRadiusActionStarted()
    {
        _targetRadiusBeforeAction = _targetRadius;
        _targetRadius = _minThirdPersonRadius;
    }

    //Called by Shop
    public void OnRestorePreActionThirdPersonRadiusState(PlayableDirector _ = null)
    {
        _targetRadius = _targetRadiusBeforeAction;
    }
}