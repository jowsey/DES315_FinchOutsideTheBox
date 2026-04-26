using System.Collections;
using UnityEngine;

public class SpinningRig : MonoBehaviour
{
    [Tooltip("How far straight up the camera moves over the duration")]
    [SerializeField] private float _upwardDistance;

    [Tooltip("Degrees to spin around the Y axis per second")]
    [SerializeField] private float _spinSpeed;

    public void Play(float seconds)
    {
        StartCoroutine(Spin(seconds));
    }

    private IEnumerator Spin(float seconds)
    {
        float timer = 0f;
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + new Vector3(0f, _upwardDistance, 0f);

        while (timer < seconds)
        {
            timer += Time.deltaTime;
            float t = timer / seconds;
            transform.position = Vector3.Lerp(startPos, endPos, t);
            transform.Rotate(0f, _spinSpeed * Time.deltaTime, 0f, Space.World);
            yield return null;
        }
    }
}
