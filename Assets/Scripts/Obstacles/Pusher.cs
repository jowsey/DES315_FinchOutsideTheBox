using Sirenix.OdinInspector;
using UnityEngine;
using Unity.VisualScripting;

#if UNITY_EDITOR
    using Sirenix.Utilities.Editor;
#endif

public class Pusher : Mirror.NetworkBehaviour
{
    [SerializeField] private float _duration;
    [SerializeField] private AnimationCurve _scaleCurve;
    [SerializeField] private bool _moveByDefault;
    [SerializeField] private float _startT;
    private bool _isMoving;
    private double _timeElapsed; //Tracks the passing Time.fixedDeltaTime but only when _isMoving is true
    private float _targetT;
    private bool _useTargetT;
    private float _tLastTick;
    private float _currentT;

#if UNITY_EDITOR
    //Used to detect changes in _duration for calling ScaleEditorAnimationCurve
    private float _oldDuration = -1.0f;

        //Used to detect changes in _scaleCurve for calling UpdateCurveMinMax
        private AnimationCurve _oldScaleCurve;

        //Used just for updating _currentScale's progress bar
        private float _minScale = -1.0f;
        private float _maxScale = -1.0f;

        [ShowInInspector, ReadOnly, ProgressBar("_minScale", "_maxScale")]
    #endif
        private float _currentScale; //todo figure out why this doesn't show up in the editor?


    private void Awake()
    {
        _isMoving = _moveByDefault;
        _timeElapsed = _startT * _duration;
        _targetT = (float)_timeElapsed;
        _useTargetT = false;
        _tLastTick = (float)_timeElapsed;
        _currentT = _startT;
    }

    private void Start()
    {
        _currentScale = _scaleCurve.Evaluate(_currentT);
        transform.localScale = new Vector3(_currentScale, transform.localScale.y, transform.localScale.z);
    }

    public void StartMoving()
    {
        _isMoving = true;
        _useTargetT = false;
    }

    public void StopMoving()
    {
        _isMoving = false;
        _useTargetT = false;
    }

    public void SetTargetT(float t)
    {
        _useTargetT = true;
        _targetT = t;
    }

    void FixedUpdate()
    {
        if (!isServer) { return; }

        if (_useTargetT)
        {
            _isMoving = (Mathf.Abs(_currentT - _targetT) > 0.01f);
            if (_isMoving && Mathf.Abs(_targetT - _duration) < 0.01f)
            {
                //_targetT is _duration, need to handle special case where t wraps around from _duration to 0
                if (_tLastTick > _currentT)
                {
                    //t has wrapped around from _duration to 0 and so has hit the target t
                    _isMoving = false;
                    _currentScale = _scaleCurve.Evaluate(_tLastTick);
                    transform.localScale = new Vector3(_currentScale, transform.localScale.y, transform.localScale.z);
                }
            }

            //Just for cleanliness sake
            if (!_isMoving)
            {
                _currentT = _targetT;
            }
        }

        if (_isMoving)
        {
            _tLastTick = _currentT;
            _timeElapsed += Time.fixedDeltaTime;
            _currentT = (float)(_timeElapsed % _duration); //range [0, _duration]
            _currentScale = _scaleCurve.Evaluate(_currentT);
            transform.localScale = new Vector3(_currentScale, transform.localScale.y, transform.localScale.z);
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
                    ScaleEditorAnimationCurve();
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
