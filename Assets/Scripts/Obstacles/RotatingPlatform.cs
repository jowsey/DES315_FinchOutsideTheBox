using Sirenix.OdinInspector;
using UnityEngine;

public class RotatingPlatform : MonoBehaviour
{
    private Rigidbody _rb;

    [SerializeField] private float _duration = 2f;
    [SerializeField] private AnimationCurve _rotationCurve = AnimationCurve.Linear(0f, 0f, 2f, 1f);
    [SerializeField] private bool _rotateByDefault = false;

    [Header("New Rotation (Relative)")]
    [SerializeField] private Vector3 _rotationOffsetEuler = new Vector3(0f, 90f, 0f);

    private bool _isRotating;
    private float _timeElapsed;
    private float _currentT;

    private Quaternion _startRotation;
    private Quaternion _targetRotation;

    private void Awake()
    {
        _rb = GetComponentInChildren<Rigidbody>();

        _startRotation = _rb.rotation;
        _targetRotation = _startRotation * Quaternion.Euler(_rotationOffsetEuler);

        if (_rotateByDefault)
        {
            StartRotating();
        }
    }

    public void StartRotating()
    {
        if (_isRotating == false) _timeElapsed = 0f;

        _isRotating = true;
    }

    public void ResetRotation()
    {
        _isRotating = false;
        _timeElapsed = 0f;
        _currentT = 0f;
        _rb.MoveRotation(_startRotation);
    }







    private void FixedUpdate()
    {
        if (!_isRotating)
            return;

        _timeElapsed += Time.fixedDeltaTime;

        float normalizedTime = Mathf.Clamp01(_timeElapsed / _duration);
        float curveValue = Mathf.Clamp01(_rotationCurve.Evaluate(normalizedTime));
        _currentT = curveValue;

        Quaternion nextRotation = Quaternion.Slerp(_startRotation, _targetRotation, _currentT);
        _rb.MoveRotation(nextRotation);

        if (_timeElapsed >= _duration)
        {
            _rb.MoveRotation(_targetRotation);
            _isRotating = false;
        }
    }
}