using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
#endif

public class Pusher : Mirror.NetworkBehaviour
{
    [SerializeField] private float _duration;
    [Tooltip("If enabled, changing the Duration will automatically call ScaleEditorAnimationCurve, scaling all keys in the Scale Curve to fit the new Duration.")]
    [SerializeField] private bool _autoScaleEditorCurve;
    [SerializeField] private AnimationCurve _scaleCurve;
    [SerializeField] private bool _moveByDefault;
    [SerializeField] private float _startTime01;
    private bool _isMoving;
    [ShowInInspector] [ReadOnly] private double _timeElapsed; //Tracks the passing Time.fixedDeltaTime but only when _isMoving is true
    
    private bool _useTargetScale;
    private float _targetScale;
    private float _scaleLastTick;

    private bool _useTargetTime;
    private float _targetTime;
    private float _timeLastTick;

    [ShowInInspector] [ReadOnly] private float _currentScale;

    //Wwise Stuff
    public AK.Wwise.Event PlatformSound = new();
    public AK.Wwise.RTPC RTPCPlatform;
    public float rtpcPlatformFloat = 0f;

#if UNITY_EDITOR
    //Used to detect changes in _duration for calling ScaleEditorAnimationCurve
    private float _oldDuration = -1.0f;

        //Used to detect changes in _scaleCurve for calling UpdateCurveMinMax
        private AnimationCurve _oldScaleCurve;

        //Used just for updating _currentScale's progress bar
        private float _minScale = -1.0f;
        private float _maxScale = -1.0f;
    #endif


    private void Awake()
    {
        _isMoving = _moveByDefault;
        _timeElapsed = _startTime01 * _duration;
        _currentScale = _scaleCurve.Evaluate(_startTime01);
    }

    private void Start()
    {
        _currentScale = _scaleCurve.Evaluate((float)_timeElapsed % _duration);
        transform.localScale = new Vector3(_currentScale, transform.localScale.y, transform.localScale.z);
        
        RTPCPlatform.SetGlobalValue(rtpcPlatformFloat);
        PlatformSound.Post(gameObject);
    }

    public void StartMoving()
    {
        _isMoving = true;
        _useTargetScale = false;
        _useTargetTime = false;
    }

    public void StopMoving()
    {
        _isMoving = false;
        _useTargetScale = false;
        _useTargetTime = false;
    }

    public void SetTargetScale(float val)
    {
        _useTargetScale = true;
        _useTargetTime = false;
        _targetScale = val;
    }

    public void SetTargetTime(float time)
    {
        _useTargetTime = true;
        _useTargetScale = false;
        _targetTime = time;
    }

    public void SetTargetTime01(float time01)
    {
        float time = time01 * _duration;
        _useTargetTime = true;
        _useTargetScale = false;
        _targetTime = time;
    }

    public void ResetIfNotMoving()
    {
        if (!_isMoving)
        {
            _timeElapsed = 0.0f;
        }
    }

    void FixedUpdate()
    {
        if (!isServer) { return; }

        if (_isMoving)
        {
            _scaleLastTick = _currentScale;
            _timeLastTick = (float)_timeElapsed;
            _timeElapsed += Time.fixedDeltaTime;
        }

        if (_useTargetScale)
        {
            _isMoving = (Mathf.Abs(_currentScale - _targetScale) > 0.01f);

            //Just for cleanliness sake
            if (!_isMoving)
            {
                _currentScale = _targetScale;
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

            //Map the current time to the scale shaped by the _scaleCurve
            _currentScale = _scaleCurve.Evaluate(currentTime);
            transform.localScale = new Vector3(_currentScale, transform.localScale.y, transform.localScale.z);

            RTPCPlatform.SetGlobalValue(_currentScale);
            rtpcPlatformFloat = _currentScale;
        }
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

        protected override void OnValidate()
        {
            base.OnValidate(); //NetworkBehaviour has its own OnValidate() apparently

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

            if (_scaleCurve != _oldScaleCurve)
            {
                UpdateCurveMinMax();
                _oldScaleCurve = _scaleCurve;
            }
        }

        //Called whenever _duration is changed
        private void ScaleEditorAnimationCurve()
        {
            //Animation curve needs to be scaled from [0, 1] (default) to [0, duration]
            float timeScaleFactor = _duration / _oldDuration;
            Keyframe[] keys = _scaleCurve.keys;
            for (int i = 0; i < _scaleCurve.length; ++i)
            {
                keys[i].time *= timeScaleFactor;

                //tangent = ds/dt, stretching t (time) by a factor, k, means tangent = ds/kdt, which means tangent needs to be divided by k
                keys[i].inTangent /= timeScaleFactor;
                keys[i].outTangent /= timeScaleFactor;
            }
            _scaleCurve.keys = keys;
        }

        //Called whenever _scaleCurve is changed
        private void UpdateCurveMinMax()
        {
            if (_scaleCurve == null || _scaleCurve.length == 0) { return; }
            _minScale = float.MaxValue;
            _maxScale = float.MinValue;

            //Can't just loop through all keyframes because tangents push intermediate values outside of discrete keyframe range
            //Take samples instead
            int samples = 50;
            for (int i = 0; i < samples; ++i)
            {
                float val = _scaleCurve.Evaluate(_duration / samples * i);
                if (val < _minScale) { _minScale = val; }
                if (val > _maxScale) { _maxScale = val; }
            }
        }
#endif
}
