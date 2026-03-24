using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
    using Sirenix.Utilities.Editor;
#endif

public class Pusher : Mirror.NetworkBehaviour
{
    [SerializeField] private float _duration;
    [SerializeField] private AnimationCurve _scaleCurve;
    [SerializeField] private bool _moveByDefault;
    [SerializeField] private bool _noLoop;
    private bool _isMoving;
    double _timeElapsed; //Tracks the passing Time.fixedDeltaTime but only when _isMoving is true

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
        _timeElapsed = 0;
    }

    public void StartMoving()
    {
        _isMoving = true;
    }

    public void StopMoving()
    {
        _isMoving = false;
    }

    void FixedUpdate()
    {
        if (!isServer) { return; }

        if (_isMoving)
        {
            _timeElapsed += Time.fixedDeltaTime;

            //ADDED IN LOOP OFF FUNCTIONALITY FOR FAKEOUT CRUSHER
            float t;
            if (!_noLoop)
            {
                t = (float)(_timeElapsed % _duration);
            }
            else
            {
                t = Mathf.Min((float)_timeElapsed, _duration);
                if (_timeElapsed >= _duration)
                {
                    _isMoving = false;
                }
            }

            _currentScale = _scaleCurve.Evaluate(t);
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
