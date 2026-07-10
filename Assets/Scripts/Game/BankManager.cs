using System.Collections.Generic;
using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;

public class BankManager : NetworkBehaviour
{
    public static BankManager Instance { get; private set; }

    [field: SyncVar] public int Balance;
    private Dictionary<Checkpoint, int> _balanceAtCheckpoint = new Dictionary<Checkpoint, int>();

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }

    public override void OnStartServer()
    {
        Cart.OnReachCheckpoint.AddListener(SaveCheckpointBalance);
        Checkpoint.RespawnEvent.AddListener(RestoreCheckpointBalance);
    }

    public override void OnStopServer()
    {
        Cart.OnReachCheckpoint.RemoveListener(SaveCheckpointBalance);
        Checkpoint.RespawnEvent.RemoveListener(RestoreCheckpointBalance);
    }

    [Server]
    private void SaveCheckpointBalance(Checkpoint checkpoint)
    {
        _balanceAtCheckpoint[checkpoint] = Balance;
        Debug.Log($"Bank Saved: {Balance} at {checkpoint.AreaName}");
    }

    [Server]
    private void RestoreCheckpointBalance(Checkpoint checkpoint)
    {
        if (_balanceAtCheckpoint.TryGetValue(checkpoint, out int savedBalance))
        {
            Balance = savedBalance;
            Debug.Log($"Bank Restored: Balance reverted to {Balance}");
        }
    }

    [Button] public void DebugBalance() => Debug.Log(Balance);
}