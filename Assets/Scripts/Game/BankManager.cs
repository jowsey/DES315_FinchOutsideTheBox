using Game;
using Mirror;
using Sirenix.OdinInspector;

public class BankManager : NetworkBehaviour
{
    public static BankManager Instance { get; private set; }

    [SyncVar, DisableInPlayMode] public int Balance;

    private void Awake()
    {
        if (!Instance) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnStartServer()
    {
        RespawnTarget.OnBuildRespawnSnapshot.AddListener(OnBuildRespawnSnapshot);
        RespawnTarget.OnRespawn.AddListener(OnRespawn);
    }

    public override void OnStopServer()
    {
        RespawnTarget.OnBuildRespawnSnapshot.RemoveListener(OnBuildRespawnSnapshot);
        RespawnTarget.OnRespawn.RemoveListener(OnRespawn);
    }

    [Server]
    private void OnBuildRespawnSnapshot(RespawnTarget.RespawnSnapshot snapshot)
    {
        snapshot.Balance = Balance;
    }

    [Server]
    private void OnRespawn(RespawnTarget target)
    {
        Balance = target.Snapshot.Balance;
    }
    
    [Button, DisableInEditorMode]
    private void DebugGiveMoney()
    {
        if (!isServer) return;
        Balance += 10;
    }
}