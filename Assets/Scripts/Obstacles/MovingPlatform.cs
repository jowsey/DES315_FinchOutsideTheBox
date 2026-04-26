using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Splines;

#if UNITY_EDITOR
    using Sirenix.Utilities.Editor;
#endif

[InfoBox("When designing: SCALE the Arch object inside the Platform, and MOVE the top-level MovingPlatform object.")]
public class MovingPlatform : NetworkBehaviour
{
    private Rigidbody _rb;
    private SplineContainer _container;

    public AK.Wwise.Event PlatformSound = new();
    public AK.Wwise.RTPC RTPCPlatform;
    public float rtpcPlatformFloat = 0f;

    [SerializeField] private float _duration;
    [Tooltip("If enabled, changing the Duration will automatically call ScaleEditorAnimationCurve, scaling all keys in the Displacement Curve to fit the new Duration.")]
    [SerializeField] private bool _autoScaleEditorCurve;
    [SerializeField] private AnimationCurve _displacementCurve;
    [SerializeField] private bool _moveByDefault;
    [SerializeField] private float _startTime01;
    private bool _isMoving;
    private double _timeElapsed; //Tracks the passing Time.fixedDeltaTime but only when _isMoving is true

    private bool _useTargetSplineVal;
    private float _targetSplineVal;
    private float _splineValLastTick;

    private bool _useTargetTime;
    private float _targetTime;
    private float _timeLastTick;

#if UNITY_EDITOR
    [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly, ProgressBar(0, 1)] private float _currentSplineVal;

    //Used to detect changes in _duration for calling ScaleEditorAnimationCurve
    private float _oldDuration = -1.0f;
#else
        private float _currentSplineVal;
#endif
    
    private void Awake()
    {
        _rb = GetComponentInChildren<Rigidbody>(true);
        _container = GetComponentInChildren<SplineContainer>(true);
        _isMoving = _moveByDefault;
        _timeElapsed = _startTime01 * _duration;
        _currentSplineVal = _displacementCurve.Evaluate(_startTime01);
    }

    private void Start()
    {
        RTPCPlatform.SetGlobalValue(rtpcPlatformFloat);
        PlatformSound.Post(_rb.gameObject);
    }

    public void StartMoving()
    {
        _isMoving = true;
        _useTargetSplineVal = false;
        _useTargetTime = false;
    }

    public void StopMoving()
    {
        _isMoving = false;
        _useTargetSplineVal = false;
        _useTargetTime = false;
    }

    public void SetTargetSplineVal(float val)
    {
        _useTargetSplineVal = true;
        _useTargetTime = false;
        _targetSplineVal = val;
    }

    public void SetTargetTime(float time)
    {
        _useTargetTime = true;
        _useTargetSplineVal = false;
        _targetTime = time;
    }

    public void SetTargetTime01(float time01)
    {
        float time = time01 * _duration;
        _useTargetTime = true;
        _useTargetSplineVal = false;
        _targetTime = time;
    }

    private void FixedUpdate()
    {
        //Used Later in here
        rtpcPlatformFloat = Mathf.Clamp(rtpcPlatformFloat, 0, 10);

        // band-aid fix for network syncing. todo needs proper re-think
        if (!authority) return;

        if (_isMoving)
        {
            _splineValLastTick = _currentSplineVal;
            _timeLastTick = (float)_timeElapsed;
            _timeElapsed += Time.fixedDeltaTime;
        }

        if (_useTargetSplineVal)
        {
            _isMoving = (Mathf.Abs(_currentSplineVal - _targetSplineVal) > 0.001f);
            if (_isMoving && Mathf.Abs(_targetSplineVal - 1.0f) < 0.01f)
            {
                //_targetSplineVal is 1, need to handle special case where spline val wraps around from 1 to 0
                if (_splineValLastTick > _currentSplineVal)
                {
                    //spline val has wrapped around from 1 to 0 and so has hit the target spline val
                    _isMoving = false;
                }
            }

            //Just for cleanliness sake
            if (!_isMoving)
            {
                _currentSplineVal = _targetSplineVal;
            }
        }
        else if (_useTargetTime)
        {
            _isMoving = (float)_timeElapsed < _targetTime;

            //Just for cleanliness sake
            if (!_isMoving)
            {
                _timeElapsed = _targetTime;
            }
        }

        if (_isMoving)
        {
            //Range [0, _duration]
            float currentTime = _useTargetTime ? (float)_timeElapsed : (float)(_timeElapsed % _duration);

            //Map the current time to the splinal t value shaped by the _displacementCurve
            //Range [0, 1]
            _currentSplineVal = _displacementCurve.Evaluate(currentTime);
            //Evaluate spline
            Vector3 localPos = _container.Splines[0].EvaluatePosition(_currentSplineVal);
            Vector3 worldPos = _container.transform.TransformPoint(localPos);
            _rb.MovePosition(worldPos);

            rtpcPlatformFloat += 1;
        }
        else
        {
            rtpcPlatformFloat -= 0.5f;
        }

        if (_currentSplineVal <= 0 || Mathf.Approximately(_currentSplineVal, 0.5f) || _currentSplineVal >= 1)
        {
            rtpcPlatformFloat = 0;
        }

        //Sets RTPC value
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

        private void OnValidate()
        {
            if (_duration != _oldDuration)
            {
                if (_oldDuration > 0.0f)
                {
                    if (_autoScaleEditorCurve)
                    {
                        ScaleEditorAnimationCurve();
                    }
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