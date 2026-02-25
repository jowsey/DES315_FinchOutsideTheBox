using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;

public class LeverMovement : MonoBehaviour
{
    [SerializeField] private Transform[] _holder;
    [SerializeField] private Transform _pivot;
    [Tooltip("The rotation of the lever by default")]
    [SerializeField] private Quaternion _defaultTarget;
    [Tooltip("The rotation of the lever that it will approach when being pressed down")]
    [SerializeField] private Quaternion _activeTarget;
    [SerializeField] private float _speed;
    private bool _triggerColliding;
    private bool _triggerCollidingLastTick;

    [Tooltip("Invoked when the lever starts moving towards target rotation")]
    [SerializeField] private UnityEvent _onLeverActivate;
    [Tooltip("Invoked when the lever starts moving back to default rotation")]
    [SerializeField] private UnityEvent _onLeverDeactivate;
    [Tooltip("Invoked when the lever has reached target rotation")]
    [SerializeField] private UnityEvent _onLeverTargetRot;
    [Tooltip("Invoked when the lever has reached default rotation (not triggered on first tick)")]
    [SerializeField] private UnityEvent _onLeverDefaultRot;


    private void Start()
    {
        _triggerColliding = false;
        _triggerCollidingLastTick = false;
    }

    private void OnTriggerStay(Collider other)
    {
        _triggerColliding = true;
    }

    private void FixedUpdate()
    {
        if (_triggerColliding && (_pivot.localRotation != _activeTarget))
        {
            //Move lever towards active target
            _pivot.localRotation = Quaternion.RotateTowards(_pivot.localRotation, _activeTarget, _speed * Time.fixedDeltaTime);

            //Check if the lever has reached target rotation
            if (_pivot.localRotation == _activeTarget)
            {
                _onLeverTargetRot.Invoke();
            }
        }
        else if (!_triggerColliding && (_pivot.localRotation != _defaultTarget))
        {
            //Move lever towards default target
            _pivot.localRotation = Quaternion.RotateTowards(_pivot.localRotation, _defaultTarget, _speed * Time.fixedDeltaTime);

            //Check if the lever has reached default rotation
            if (_pivot.localRotation == _defaultTarget)
            {
                _onLeverDefaultRot.Invoke();
            }
        }

        //Check for changes in _triggerColliding state
        if (!_triggerCollidingLastTick && _triggerColliding)
        {
            //Trigger is now active
            _onLeverActivate.Invoke();
        }
        else if (_triggerCollidingLastTick && !_triggerColliding)
        {
            //Trigger is no longer active
            _onLeverDeactivate.Invoke();
        }

        //Keep the transform of the holder facing world-up
        //todo: this will obviously just be one mesh - im just kinda dumb and dont know how to use probuilder so it's two meshes for now
        foreach (Transform t in _holder)
        {
            t.rotation = Quaternion.identity;
        }

        //Reset
        _triggerCollidingLastTick = _triggerColliding;
        _triggerColliding = false;
    }
}
