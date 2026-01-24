using Sirenix.OdinInspector;
using UnityEngine;

public class GravitySurface : MonoBehaviour
{
    public enum SurfaceType
    {
        ConstantLocal,
        MatchSurfaceNormal
    }

    [Header("Gravity Settings")]
    public SurfaceType Type = SurfaceType.ConstantLocal;

    [EnableIf("Type", SurfaceType.ConstantLocal)] public Vector3 ConstantDirection = Vector3.down;

    private void OnDrawGizmosSelected()
    {
        if (Type == SurfaceType.ConstantLocal)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + transform.TransformDirection(ConstantDirection));
        }
    }
}