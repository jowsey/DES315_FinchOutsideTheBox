using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Util
{
    [InfoBox("This object has a small chance to turn and face the camera when not being looked at, before quickly turning back to normal when looked at again")]
    public class FollowWhenNotLookedAt : MonoBehaviour
    {
        [SerializeField] [Range(0, 1)] private float _runChance = 0.1f;

        [Tooltip("How long do we need to be invisible before we're eligible to run the animation")]
        [SerializeField] private float _invisibleTimeBeforePossible = 1f;

        [Tooltip("How long the turning animation will last")]
        [SerializeField] private float _turnDuration = 0.2f;

        [Tooltip("Won't fire if camera is within this distance to prevent being too obvious")]
        [SerializeField] private float _minCameraDistance = 10f;

        [SerializeField] private Collider _boundsCollider;
        private Camera _camera;

        private bool _wasVisible;
        private float _invisibleStartTime;

        private void Awake()
        {
            _camera = Camera.main;
        }

        private void LateUpdate()
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(_camera);
            var visible = GeometryUtility.TestPlanesAABB(planes, _boundsCollider.bounds);

            // Just became invisible
            if (_wasVisible && !visible)
            {
                _invisibleStartTime = Time.time;
            }
            // Just became visible
            else if (!_wasVisible && visible)
            {
                // If we've been invisible for long enough, we're far enough from the camera, and we hit the random chance ... 😼😼😼
                if (Time.time - _invisibleStartTime > _invisibleTimeBeforePossible
                    && Vector3.Distance(transform.position, _camera.transform.position) > _minCameraDistance
                    && Random.value < _runChance)
                {
                    var lookRotation = Quaternion.LookRotation(_camera.transform.position - transform.position);

                    Tween.EulerAngles(
                        transform,
                        new Vector3(transform.eulerAngles.x, lookRotation.eulerAngles.y, transform.eulerAngles.z),
                        transform.eulerAngles,
                        _turnDuration,
                        Ease.OutSine
                    );
                }
            }

            _wasVisible = visible;
        }
    }
}