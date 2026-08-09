using PrimeTween;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI
{
    public class ControlReference : MonoBehaviour
    {
        [SerializeField] private InputActionReference _input;
        [SerializeField] private PlayerController.ControlBlockerFlags _affectedBlockerFlags;
        [SerializeField] private InputIconManager _inputIconManager;

        [SerializeField] private CanvasGroup _canvasGroup;

        private const float _transitionDuration = 0.15f;

        private bool _isPressed;

        public bool Visible { get; private set; }

        private void Start()
        {
            _inputIconManager.SetAction(_input);
        }

        public void ToggleVisible(bool visible, bool instant = false)
        {
            Visible = visible;

            if (instant)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                return;
            }

            Tween.CompleteAll(_canvasGroup);
            Tween.Alpha(_canvasGroup, visible ? 1f : 0f, _transitionDuration, Ease.OutCubic);
        }

        private void Update()
        {
            if (!_isPressed && (_input.action.IsPressed() && PlayerController.ControlEnabled(_affectedBlockerFlags)))
            {
                _isPressed = true;
                Tween.Scale(transform, Vector3.one * 0.95f, _transitionDuration, Ease.OutCubic);
            }
            else if (_isPressed && !_input.action.IsPressed())
            {
                _isPressed = false;
                Tween.Scale(transform, Vector3.one, _transitionDuration, Ease.OutCubic);
            }
        }
    }
}