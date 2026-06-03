using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

namespace UI
{
    public class PingObject : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissiveColor = Shader.PropertyToID("_EmissiveColor");

        private Camera _camera;

        [SerializeField] private int _cycles = 4;
        [SerializeField] [SuffixLabel("seconds")] private float _cycleLength = 1f;
        [SerializeField] [SuffixLabel("meters")] private float _animateDistance = 0.5f;
        [SerializeField] private float _smoothingSpeed = 2f;

        private MeshRenderer _renderer;

        private Transform _attachTarget;
        private Vector3 _attachLocalPosition;
        private Quaternion _attachLocalRotation;

        private Vector3 _animationOffset;

        private void Awake()
        {
            _camera = Camera.main;
            _renderer = GetComponentInChildren<MeshRenderer>();
        }

        public void Build(PlayerController player, Transform attachTarget)
        {
            var accentColor = PlayerController.LoadedSkins[player.PlayerSkinIndex].AccentColor;

            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(BaseColor, accentColor);
            mpb.SetColor(EmissiveColor, accentColor * 1.75f);
            _renderer.SetPropertyBlock(mpb);

            attachTarget ??= transform;
            _attachTarget = attachTarget;
            _attachLocalPosition = attachTarget.InverseTransformPoint(transform.position);
            _attachLocalRotation = Quaternion.Inverse(attachTarget.rotation) * transform.rotation;
        }

        private void Start()
        {
            Sequence.Create()
                .Group(Tween.Custom(
                    this,
                    Vector3.zero,
                    new Vector3(0, 0, -_animateDistance),
                    _cycleLength,
                    (obj, val) => obj._animationOffset = val,
                    Ease.InOutSine,
                    _cycles,
                    CycleMode.Yoyo
                ))
                .Chain(Tween.Scale(transform, 0f, 0.5f, Ease.InOutCubic))
                .OnComplete(() => Destroy(gameObject));
        }

        private void LateUpdate()
        {
            var localPosition = _attachTarget.TransformPoint(_attachLocalPosition);
            var localRotation = _attachTarget.rotation * _attachLocalRotation;
            transform.position = localPosition + (localRotation * _animationOffset);

            var cameraDir = transform.position - _camera.transform.position;
            var targetForward = localRotation * Vector3.forward;
            var idealRotation = Quaternion.LookRotation(targetForward, cameraDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, idealRotation, Time.deltaTime * _smoothingSpeed);
        }
    }
}