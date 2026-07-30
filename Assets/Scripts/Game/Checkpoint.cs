using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public class Checkpoint : MonoBehaviour
{
    public static readonly UnityEvent<Checkpoint> OnPreRespawn = new();
    public static readonly UnityEvent<Checkpoint> OnRespawn = new();

    [field: SerializeField] public Transform[] playerRespawnLocalTransforms { get; private set; }
    [field: SerializeField] public Transform cartRespawnLocalTransform { get; private set; }

    public string AreaName = "Unnamed Checkpoint";

    [field: SerializeField] [RequiredIn(PrefabKind.PrefabInstanceAndNonPrefabInstance)] public RuntimeAnimatorController AnimatorController { get; private set; }

    private void OnValidate()
    {
        name = $"Checkpoint [{AreaName}]";
    }
}