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
        if (Physics.Raycast(ray, out RaycastHit hit, _maxDistance, LayerMask.GetMask("Interactable")))
        {
            _hitTransform = hit.transform;
        }
        else
        {
            _hitTransform = null;
        }
    }

    //private void OnDrawGizmos()
    //{
    //    if (_camera != null)
    //    {
    //        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
    //        Gizmos.DrawRay(ray.origin, ray.direction * _maxDistance);
    //    }
    //}
}