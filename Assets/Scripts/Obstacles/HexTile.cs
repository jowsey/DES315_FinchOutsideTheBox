using Mirror;
using PrimeTween;
using UnityEngine;

namespace Obstacles
{
    public class HexTile : NetworkBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private float _fallDelay = 2.5f;
        [SerializeField] private LayerMask _fallCollisionLayerMask;

        [SerializeField] private Renderer _renderer;
        [SerializeField] private Collider _collider;

        [SyncVar] private double _touchTime = -1;

        private bool _isFalling;

        protected override void OnValidate()
        {
            if (!_renderer) _renderer = GetComponentInChildren<Renderer>();
            if (!_collider) _collider = GetComponentInChildren<Collider>();
        }

        private void OnCollisionEnter(Collision other)
        {
            if (!isServer) return;
            if (_touchTime >= 0) return;
            if (((1 << other.gameObject.layer) & _fallCollisionLayerMask) == 0) return;

            _touchTime = NetworkTime.time;
        }

        private void FixedUpdate()
        {
            if (!_isFalling && _touchTime >= 0)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(BaseColor, Color.Lerp(_renderer.material.color, Color.red, (float)((NetworkTime.time - _touchTime) / _fallDelay)));

                _renderer.SetPropertyBlock(mpb);

                if (NetworkTime.time > _touchTime + _fallDelay)
                {
                    _isFalling = true;

                    Tween.Scale(transform, Vector3.zero, 1f, Ease.InBack)
                        .OnComplete(() => gameObject.SetActive(false), false);
                }
            }
        }
    }
}