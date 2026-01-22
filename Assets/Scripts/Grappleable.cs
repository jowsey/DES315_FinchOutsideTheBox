using UnityEngine;

//todo: maybe unnecessary

public class Grappleable : MonoBehaviour
{
    void OnEnable()
    {
        EventManager.OnGrapple += OnGrapple;
    }

    void OnDisable()
    {
        EventManager.OnGrapple -= OnGrapple;
    }

    void OnGrapple(GameObject grappled)
    {
        Debug.Log(grappled.name);
    }
}
