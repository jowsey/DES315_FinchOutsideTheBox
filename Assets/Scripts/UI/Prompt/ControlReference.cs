using PrimeTween;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI
{
    public class ControlReference : MonoBehaviour
    {
        [SerializeField] private InputActionReference _input;
        [SerializeField] private PlayerController.ControlBlockerFlags _affectedBlockerFlags;

        private const float _transitionDuration = 0.15f;

        private bool _isPressed;

        private void Update()
        {
            if (!_isPressed && (_input.action.IsPressed() && PlayerController.ControlEnabled(_affectedBlockerFlags)))
            {
                _isPressed = true;
                Tween.Scale(transform, Vector3.one * 0.9f, _transitionDuration, Ease.OutCubic);
            }
            else if (_isPressed && !_input.action.IsPressed())
            {
                _isPressed = false;
                Tween.Scale(transform, Vector3.one, _transitionDuration, Ease.OutCubic);
            }
        }
    }
}