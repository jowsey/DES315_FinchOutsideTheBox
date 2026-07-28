using PrimeTween;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI
{
    public class ControlReference : MonoBehaviour
    {
        [SerializeField] private InputActionReference _input;

        private const float _transitionDuration = 0.15f;

        private void Update()
        {
            if (_input.action.WasPressedThisFrame())
            {
                Tween.Scale(transform, Vector3.one * 0.9f, _transitionDuration, Ease.OutCubic);
            }
            else if (_input.action.WasReleasedThisFrame())
            {
                Tween.Scale(transform, Vector3.one, _transitionDuration, Ease.OutCubic);
            }
        }
    }
}