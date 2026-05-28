using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class CartDistanceWarning : MonoBehaviour
    {
        [SerializeField] [Required] private Image _icon;
        [SerializeField] private float _warningDistance = 50f;
        [SerializeField] private float _iconAnimationScale = 1.05f;

        [SerializeField] [RequiredIn(PrefabKind.PrefabInstanceAndNonPrefabInstance)] private Transform _cart;

        private bool _active;

        private void Awake()
        {
            transform.localScale = Vector3.zero;
        }

        private void Start()
        {
            Tween.Scale(_icon.transform, Vector3.one * _iconAnimationScale, 1f, Ease.InOutQuad, -1, CycleMode.Rewind);
        }

        private void LateUpdate()
        {
            if (!PlayerController.LocalPlayer) return;

            var distance = Vector3.Distance(PlayerController.LocalPlayer.transform.position, _cart.position);
            if (!_active && distance > _warningDistance)
            {
                Toggle(true);
            }
            else if (_active && distance < _warningDistance)
            {
                Toggle(false);
            }
        }

        private void Toggle(bool toggle)
        {
            _active = toggle;

            if (toggle)
            {
                Tween.Scale(transform, Vector3.zero, Vector3.one, 0.5f, Ease.OutBack);
            }
            else
            {
                Tween.Scale(transform, Vector3.one, Vector3.zero, 0.5f, Ease.InBack);
            }
        }
    }
}