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

        private void Awake()
        {
            _camera = Camera.main;
            _renderer = GetComponentInChildren<MeshRenderer>();
        }

        public void Build(PlayerController player)
        {
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(BaseColor, PlayerController.LoadedSkins[player.PlayerSkinIndex].AccentColor);
            mpb.SetColor(EmissiveColor, PlayerController.LoadedSkins[player.PlayerSkinIndex].AccentColor * 1.75f);
            _renderer.SetPropertyBlock(mpb);
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