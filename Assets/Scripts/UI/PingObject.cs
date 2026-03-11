using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

namespace UI
{
    public class PingObject : MonoBehaviour
    {
        private Camera _camera;

        [SerializeField] private int _cycles = 4;
        [SerializeField] [SuffixLabel("seconds")] private float _cycleLength = 1f;
        [SerializeField] [SuffixLabel("meters")] private float _animateDistance = 0.5f;
        [SerializeField] private float _smoothingSpeed = 2f;

        private void Awake()
        {
            _camera = Camera.main;
        }

        private void Start()
        {
            Sequence.Create()
                .Group(Tween.LocalPosition(
                    transform,
                    transform.localPosition,
                    transform.localPosition - transform.forward * _animateDistance,
                    _cycleLength,
                    Ease.InOutSine,
                    _cycles,
                    CycleMode.Yoyo
                ))
                .Chain(Tween.Scale(transform, 0f, 0.5f, Ease.InOutCubic))
                .OnComplete(() => Destroy(gameObject));
        }

        private void LateUpdate()
        {
            var cameraDir = transform.position - _camera.transform.position;
            var idealRotation = Quaternion.LookRotation(transform.forward, cameraDir);

            transform.rotation = Quaternion.Slerp(transform.rotation, idealRotation, Time.deltaTime * _smoothingSpeed);
        }
    }
}