using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using UnityEngine;
using UnityEngine.Splines;

public class MovingPlatform : MonoBehaviour
{
    private Rigidbody _rb;
    private SplineContainer _container;

    [SerializeField] private float _duration;
    [SerializeField] private AnimationCurve _displacementCurve;

    #if UNITY_EDITOR
        [ShowInInspector, ReadOnly, ProgressBar(0, 1)] private float _currentT;

        //Used to detect changes in _duration for calling ScaleEditorAnimationCurve
        private float _oldDuration = -1.0f;
    #else
        private float _currenT;
    #endif


    private void Awake()
    {
        _rb = GetComponentInChildren<Rigidbody>();
        _container = GetComponentInChildren<SplineContainer>();
    }

    private void FixedUpdate()
    {
        //Range [0, _duration]
        float absoluteT = (float)(Mirror.NetworkTime.time % _duration);

        //Map the absolute t value to the splinal t value shaped by the _displacementCurve
        //Range [0, 1]
        _currentT = _displacementCurve.Evaluate(absoluteT);

        //Evaluate spline
        Vector3 localPos = _container.Splines[0].EvaluatePosition(_currentT);
        Vector3 worldPos = _container.transform.TransformPoint(localPos);
        _rb.MovePosition(worldPos);
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