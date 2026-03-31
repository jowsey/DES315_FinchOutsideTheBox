using PrimeTween;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Tools
{
    public class TurretRotator : MonoBehaviour
    {
        [SerializeField] private Transform _target;

        [SerializeField] private float _fromLocal;
        [SerializeField] private float _toLocal;
        [SerializeField] private Vector3 _axis = Vector3.up;

        private void OnValidate()
        {
            if (!_target) _target = transform;
        }

        private void Update()
        {
            if (Keyboard.current.pKey.wasPressedThisFrame)
            {
                var origin = transform.localEulerAngles;
                Tween.LocalEulerAngles(transform, origin + _axis * _fromLocal, origin + _axis * _toLocal, 4f, Ease.OutBack);
            }
        }
    }
}