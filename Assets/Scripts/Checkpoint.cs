using UnityEngine;
using UnityEngine.Events;

public class Checkpoint : MonoBehaviour
{
    public static readonly UnityEvent<Checkpoint> RespawnEvent = new();
    
    [field: SerializeField] public Transform[] playerRespawnLocalTransforms { get; private set; }
    [field: SerializeField] public Transform cartRespawnLocalTransform { get; private set; }

    public string AreaName = "Unnamed Checkpoint";

    private void OnValidate()
    {
        name = $"Checkpoint [{AreaName}]";
    }
}
