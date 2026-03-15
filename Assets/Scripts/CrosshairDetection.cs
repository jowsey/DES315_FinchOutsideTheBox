using UnityEngine;

public class CrosshairDetection : MonoBehaviour
{
    private Camera _camera;
    [SerializeField] private float _maxDistance;
    
    //The transform of the object currently being looked at
    public static Transform TargetedTransform { get; private set; } 

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    //LateUpdate so that it's after Cinemachine updates the camera
    private void LateUpdate()
    {
        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, _maxDistance, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == TargetedTransform) { return; }
            if (hit.transform.TryGetComponent<Interactable>(out Interactable interactable))
            {
                TargetedTransform = interactable.InteractedTransform;
            }
            else
            {
                TargetedTransform = null;
            }
        }
        else
        {
            TargetedTransform = null;
        }
    }
}