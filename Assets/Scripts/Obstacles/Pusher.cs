using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;

public class Pusher : NetworkBehaviour
{
    [SuffixLabel("m/s")]
    [SerializeField] private float _pushSpeed;

    [SuffixLabel("m")]
    [SerializeField] private float _minPushDistance;

    [SuffixLabel("m")]
    [SerializeField] private float _maxPushDistance;

    private bool _extending;

    private void Start()
    {
        _extending = true;
    }

    void Update()
    {
        if (_extending)
        {
            transform.localScale += Vector3.right * _pushSpeed * Time.deltaTime;
            if (transform.localScale.x >= _maxPushDistance)
            {
                _extending = false;
            }
        }
        else
        {
            transform.localScale -= Vector3.right * _pushSpeed * Time.deltaTime;
            if (transform.localScale.x <= _minPushDistance)
            {
                _extending = true;
            }
        }
        transform.localScale = new Vector3(Mathf.Clamp(transform.localScale.x, _minPushDistance, _maxPushDistance), transform.localScale.y, transform.localScale.z);
    }
}
