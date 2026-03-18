using PrimeTween;
using UnityEngine;

namespace Obstacles
{
    public class HexTile : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private float _fallDelay = 2.5f;
        [SerializeField] private LayerMask _fallCollisionLayerMask;
        [SerializeField] private Renderer _renderer;

        private float _touchTime = -1;

        private bool _isFalling;

        private void OnValidate()
        {
            if (!_renderer) _renderer = GetComponentInChildren<Renderer>();
        }

        private void OnCollisionEnter(Collision other)
        {
            if (((1 << other.gameObject.layer) & _fallCollisionLayerMask) == 0) return;
            if (_touchTime >= 0) return;

            _touchTime = Time.time;
        }

        private void FixedUpdate()
        {
            if (!_isFalling && _touchTime >= 0)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor(BaseColor, Color.Lerp(_renderer.material.color, Color.red, (Time.time - _touchTime) / _fallDelay));

                _renderer.SetPropertyBlock(mpb);

                if (Time.time > _touchTime + _fallDelay)
                {
                    _isFalling = true;

                    Tween.Scale(transform, Vector3.zero, 1f, Ease.InBack)
                        .OnComplete(() => Destroy(gameObject), false);
                }
            }
        }
    }
}