using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Splines;
#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
#endif
using ReadOnlyAttribute = Sirenix.OdinInspector.ReadOnlyAttribute;
using ShowInInspectorAttribute = Sirenix.OdinInspector.ShowInInspectorAttribute;

[InfoBox("When designing: SCALE the object inside the Platform, and MOVE the top-level MovingPlatform object.")]
public class MovingPlatform : NetworkBehaviour
{
    private Rigidbody _rb;
    private SplineContainer _splineContainer;

    public AK.Wwise.Event PlatformSound = new();
    public AK.Wwise.RTPC RTPCPlatform;
    public float rtpcPlatformFloat;

    [SerializeField] private float _duration;

    [Tooltip("If enabled, changing the Duration will automatically call ScaleEditorAnimationCurve, scaling all keys in the Displacement Curve to fit the new Duration.")]
    [SerializeField] private bool _autoScaleEditorCurve;

    [SerializeField] private AnimationCurve _displacementCurve;

    [SerializeField] private bool _moveByDefault;
    [SerializeField, Range(0f, 1f)] private float _startTime01;

    [ShowInInspector, ReadOnly, ProgressBar(0, 1)] private float _currentSplineT;

    [SyncVar] private double? _moveStartTime;
    [SyncVar] private double _pausedElapsedTime;

    [SyncVar] private bool _useTargetTime;
    [SyncVar] private float _targetTime;

#if UNITY_EDITOR
    //Used to detect changes in _duration for calling ScaleEditorAnimationCurve
    private float _oldDuration = -1.0f;
#endif

    private void Awake()
    {
        _rb = GetComponentInChildren<Rigidbody>(true);
        _splineContainer = GetComponentInChildren<SplineContainer>(true);

        _pausedElapsedTime = _startTime01 * _duration;
        _moveStartTime = _moveByDefault ? NetworkTime.time - _pausedElapsedTime : null;

        _currentSplineT = _displacementCurve.Evaluate(_startTime01);
    }

    private void Start()
    {
        RTPCPlatform.SetGlobalValue(rtpcPlatformFloat);
        PlatformSound.Post(_rb.gameObject);
    }

    public void StartMoving()
    {
        if (!isServer) return;

        if (!_moveStartTime.HasValue)
        {
            _moveStartTime = NetworkTime.time - _pausedElapsedTime;
        }

        _useTargetTime = false;
    }

    public void StopMoving()
    {
        if (!isServer) return;

        if (_moveStartTime.HasValue)
        {
            _pausedElapsedTime = NetworkTime.time - _moveStartTime.Value;
        }

        _moveStartTime = null;
        _useTargetTime = false;
    }

    public void SetTargetTime01(float time01)
    {
        if (!isServer) return;

        _useTargetTime = true;
        _targetTime = time01 * _duration;
    }

    public void ResetIfNotMoving()
    {
        if (!isServer) return;

        if (!_moveStartTime.HasValue)
        {
            _pausedElapsedTime = 0;
        }
    }

    private void FixedUpdate()
    {
        var timeElapsed = NetworkTime.time - _moveStartTime + _pausedElapsedTime;

        // Move if there is time elapsed
        if (timeElapsed.HasValue)
        {
            var currentTime = _useTargetTime ? timeElapsed.Value : timeElapsed.Value % _duration;

            _currentSplineT = _displacementCurve.Evaluate((float)currentTime);
            Vector3 localPos = _splineContainer.Splines[0].EvaluatePosition(_currentSplineT);
            Vector3 worldPos = _splineContainer.transform.TransformPoint(localPos);
            _rb.MovePosition(worldPos);

            rtpcPlatformFloat += 1;
        }
        else
        {
            rtpcPlatformFloat -= 0.5f;
        }

        if (_currentSplineT <= 0 || Mathf.Approximately(_currentSplineT, 0.5f) || _currentSplineT >= 1)
        {
            rtpcPlatformFloat = 0;
        }

        // Check if reached target time
        if (_useTargetTime && timeElapsed >= _targetTime)
        {
            _moveStartTime = null;
            _pausedElapsedTime = _targetTime;
        }

        //Sets RTPC value
        rtpcPlatformFloat = Mathf.Clamp(rtpcPlatformFloat, 0, 10);
        RTPCPlatform.SetGlobalValue(rtpcPlatformFloat);
    }

#if UNITY_EDITOR
    [OnInspectorGUI]
    private void RepaintConstantly()
    {
        if (Application.isPlaying)
        {
            GUIHelper.RequestRepaint();
        }
    }

    private new void OnValidate()
    {
        if (_duration != _oldDuration)
        {
            if (_oldDuration > 0.0f && _autoScaleEditorCurve)
            {
                ScaleEditorAnimationCurve();
            }

            _oldDuration = _duration;
        }
    }

    //Called whenever _duration is changed
    private void ScaleEditorAnimationCurve()
    {
        //Animation curve needs to be scaled from [0, 1] (default) to [0, duration]
        float timeScaleFactor = _duration / _oldDuration;
        Keyframe[] keys = _displacementCurve.keys;
        for (int i = 0; i < _displacementCurve.length; ++i)
        {
            keys[i].time *= timeScaleFactor;

            //tangent = ds/dt, stretching t (time) by a factor, k, means tangent = ds/kdt, which means tangent needs to be divided by k
            keys[i].inTangent /= timeScaleFactor;
            keys[i].outTangent /= timeScaleFactor;
        }

        _displacementCurve.keys = keys;
    }
#endif
}