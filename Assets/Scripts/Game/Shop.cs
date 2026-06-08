using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;

public class Shop : MonoBehaviour
{
    private CinemachineCamera _cinemachineCamera;
    private CinemachineOrbitalFollow _orbitalFollow;
    private CameraZoomController _zoomController;
    [Tooltip("The transform that the camera will be moved to when the shop is entered")]
    [SerializeField] private Transform _cameraLockLocation;

    void Awake()
    {
        foreach (CinemachineCamera cam in FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam.CompareTag("FreeLookCam"))
            {
                _cinemachineCamera = cam;
                _orbitalFollow = _cinemachineCamera.GetComponent<CinemachineOrbitalFollow>();
                break;
            }
        }
        _zoomController = Camera.main.GetComponent<CameraZoomController>();
    }

    [Button]
    public void EnterShop()
    {
        //Move camera
        _zoomController.OnForceThirdPersonActionStarted();
        _zoomController.OnForceMinThirdPersonRadiusActionStarted();
        _cinemachineCamera.Follow = _cameraLockLocation;
        _cinemachineCamera.LookAt = _cameraLockLocation;
        _orbitalFollow.HorizontalAxis.Value = _cameraLockLocation.eulerAngles.y;

        //Add control blockers
        PlayerController.ControlBlockerFlags flags = PlayerController.ControlBlockerFlags.All;
        flags &= ~PlayerController.ControlBlockerFlags.Pause;
        flags &= ~PlayerController.ControlBlockerFlags.ToggleTextChat;
        //todo: do we let players respawn if they're in the shop? i feel like it would introduce a loooot of edge cases like if they're in the middle of stuff
        PlayerController.AddControlBlockerFlags(this, flags);
    }

    [Button]
    public void LeaveShop()
    {
        //Move camera
        _cinemachineCamera.Follow = PlayerController.LocalPlayer.transform;
        _cinemachineCamera.LookAt = PlayerController.LocalPlayer.transform;
        _orbitalFollow.HorizontalAxis.Value = PlayerController.LocalPlayer.transform.eulerAngles.y;
        _zoomController.OnRestorePreActionFirstPersonState();
        _zoomController.OnRestorePreActionThirdPersonRadiusState();

        //Remove control blockers
        PlayerController.RemoveAllControlBlockerFlags(this);
    }

    void Update()
    {
        
    }
}
