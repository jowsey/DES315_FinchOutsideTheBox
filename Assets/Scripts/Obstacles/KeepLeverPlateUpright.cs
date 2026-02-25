using UnityEngine;

public class KeepLeverPlateUpright : MonoBehaviour
{
    private Quaternion _fixedRot;

    void Start()
    {
        _fixedRot = transform.rotation;
    }

    void LateUpdate()
    {
        transform.rotation = _fixedRot;
    }
}
