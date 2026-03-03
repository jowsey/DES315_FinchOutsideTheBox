using UnityEngine;
using UnityEngine.Events;

public class Checkpoint : MonoBehaviour
{
    public static UnityEvent<Checkpoint> respawnEvent = new();
    
    [HideInInspector] public int index;
    [field: SerializeField] public Transform[] playerRespawnLocalTransforms { get; private set; }
    [field: SerializeField] public Transform cartRespawnLocalTransform { get; private set; }

    public string AreaName;
}
