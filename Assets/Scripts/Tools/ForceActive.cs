using UnityEngine;

public class ForceActive : MonoBehaviour
{
    [Tooltip("Objects in this list will be forced active every frame")]
    [SerializeField] private GameObject[] _objects;

    void Update()
    {
        foreach (GameObject o in _objects)
        {
            o.SetActive(true);
        }
    }
}
