using UnityEngine;

public class CrosshairDetection : MonoBehaviour
{
    [SerializeField] private float _maxDistance;
    private Camera _camera;
    public static Transform _hitTransform { get; private set; } //The transform of the object currently being looked at

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    //LateUpdate so that it's after Cinemachine updates the camera
    private void LateUpdate()
    {
        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, _maxDistance, ~(1 << LayerMask.NameToLayer("Player"))))
        {
            if (hit.transform == _hitTransform) { return; }
            if (hit.transform.TryGetComponent<Interactable>(out Interactable interactable))
            {
                _hitTransform = interactable.InteractedTransform;
            }
            else
            {
                _hitTransform = null;
            }
        }
        else
        {
            _hitTransform = null;
        }
    }
}