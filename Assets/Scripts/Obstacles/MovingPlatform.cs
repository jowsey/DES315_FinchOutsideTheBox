using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Splines;
using System.Linq;

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

    [SerializeField] private float _duration;
    [SerializeField] private AnimationCurve _displacementCurve;
    [SerializeField] private bool _moveByDefault;
    [SerializeField] private float _startT;
    private bool _isMoving;
    private double _timeElapsed; //Tracks the passing Time.fixedDeltaTime but only when _isMoving is true
    private float _targetT;
    private bool _useTargetT;
    private float _tLastTick;

#if UNITY_EDITOR
    [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly, ProgressBar(0, 1)] private float _currentT;

    //Used to detect changes in _duration for calling ScaleEditorAnimationCurve
    private float _oldDuration = -1.0f;
#else
        private float _currentT;
#endif
    
    private void Awake()
    {
        _rb = GetComponentInChildren<Rigidbody>(true);
        _container = GetComponentInChildren<SplineContainer>(true);
        _isMoving = _moveByDefault;
        _timeElapsed = _startT * _duration;
        _targetT = (float)_timeElapsed;
        _useTargetT = false;
        _tLastTick = (float)_timeElapsed;
        _currentT = _startT;



    }

    public void StartMoving()
    {
        //Play platform sound perma but its default rtpc is 0
        PlatformSound.Post(gameObject);
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

    private void FixedUpdate()
    {
        // band-aid fix for network syncing. todo needs proper re-think
        if (!authority) return;
        
        if (_useTargetT)
        {
            _isMoving = (Mathf.Abs(_currentT - _targetT) > 0.001f);
            if (_isMoving && Mathf.Abs(_targetT - 1.0f) < 0.01f)
            {
                //_targetT is 1, need to handle special case where t wraps around from 1 to 0
                if (_tLastTick > _currentT)
                {
                    //t has wrapped around from 1 to 0 and so has hit the target t
                    _isMoving = false;
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

            //Range [0, _duration]
            float absoluteT = (float)(_timeElapsed % _duration);

            //Map the absolute t value to the splinal t value shaped by the _displacementCurve
            //Range [0, 1]
            _currentT = _displacementCurve.Evaluate(absoluteT);
            //Evaluate spline
            Vector3 localPos = _container.Splines[0].EvaluatePosition(_currentT);
            Vector3 worldPos = _container.transform.TransformPoint(localPos);
            _rb.MovePosition(worldPos);
        }

        RpcSetRTPCGlobalValue(_rb.linearVelocity.magnitude);
    }


    [ClientRpc(includeOwner = true)]
    void RpcSetRTPCGlobalValue(float val)
    {
        if (transform.name == "LiftLever")
        {
            Debug.Log(val * 2);
        }
        RTPCPlatform.SetGlobalValue(val * 2);
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