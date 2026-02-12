using System;
using UnityEngine;
using UnityEngine.Events;

public class Checkpoint : MonoBehaviour
{
    [HideInInspector] public int index;
    [HideInInspector] public static UnityEvent<Checkpoint> respawnEvent;
    [field: SerializeField] public Transform[] playerRespawnLocalTransforms { get; private set; }
    [field: SerializeField] public Transform cartRespawnLocalTransform { get; private set; }


    private void Awake()
    {
        if (respawnEvent == null)
        {
            respawnEvent = new UnityEvent<Checkpoint>();
        }
    }
}
