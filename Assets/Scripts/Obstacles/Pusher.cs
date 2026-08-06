using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
#endif
using ReadOnlyAttribute = Sirenix.OdinInspector.ReadOnlyAttribute;
using ShowInInspectorAttribute = Sirenix.OdinInspector.ShowInInspectorAttribute;

public class Pusher : NetworkBehaviour
{
    //Wwise Stuff
    public AK.Wwise.Event PlatformSound = new();
    public AK.Wwise.RTPC RTPCPlatform;
    public float rtpcPlatformFloat;

    [SerializeField] private float _duration;

    [Tooltip("If enabled, changing the Duration will automatically call ScaleEditorAnimationCurve, scaling all keys in the Scale Curve to fit the new Duration.")]
    [SerializeField] private bool _autoScaleEditorCurve;

    [SerializeField] private AnimationCurve _scaleCurve;

    [SerializeField] private bool _moveByDefault;
    [SerializeField] private float _startTime01;

    [ShowInInspector] [ReadOnly] private float _currentScale;

    [SyncVar] private double? _moveStartTime;
    [SyncVar] private double _pausedElapsedTime;

    [SyncVar] private bool _useTargetTime;
    [SyncVar] private float _targetTime;

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
        _pausedElapsedTime = _startTime01 * _duration;
        _moveStartTime = _moveByDefault ? NetworkTime.time - _pausedElapsedTime : null;

        _currentScale = _scaleCurve.Evaluate(_startTime01);
    }

    private void Start()
    {
        RTPCPlatform.SetGlobalValue(rtpcPlatformFloat);
        PlatformSound.Post(gameObject);
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

    void FixedUpdate()
    {
        var timeElapsed = NetworkTime.time - _moveStartTime + _pausedElapsedTime;

        if (timeElapsed.HasValue)
        {
            var currentTime = _useTargetTime ? timeElapsed.Value : timeElapsed.Value % _duration;

            _currentScale = _scaleCurve.Evaluate((float)currentTime);
            transform.localScale = new Vector3(_currentScale, transform.localScale.y, transform.localScale.z);

            RTPCPlatform.SetGlobalValue(_currentScale);
            rtpcPlatformFloat = _currentScale;
        }

        // Check if reached target time
        if (_useTargetTime && timeElapsed >= _targetTime)
        {
            _moveStartTime = null;
            _pausedElapsedTime = _targetTime;
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
        if (_scaleCurve == null || _scaleCurve.length == 0)
        {
            return;
        }

        _minScale = float.MaxValue;
        _maxScale = float.MinValue;

        //Can't just loop through all keyframes because tangents push intermediate values outside of discrete keyframe range
        //Take samples instead
        int samples = 50;
        for (int i = 0; i < samples; ++i)
        {
            float val = _scaleCurve.Evaluate(_duration / samples * i);
            if (val < _minScale)
            {
                _minScale = val;
            }

            if (val > _maxScale)
            {
                _maxScale = val;
            }
        }
    }
#endif
}