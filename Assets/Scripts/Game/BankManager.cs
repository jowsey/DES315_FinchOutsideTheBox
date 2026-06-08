using Mirror;

public static class BankManager
{
    //The players' current joint balance
    public static int Balance { get; private set; }

    [Command] public static void CmdAddToBalance(int val) => RpcAddToBalance(val);
    [Command] public static void CmdSubtractFromBalance(int val) => RpcSubtractFromBalance(val);

    [ClientRpc] private static void RpcAddToBalance(int val) => Balance += val;
    [ClientRpc] private static void RpcSubtractFromBalance(int val) => Balance -= val;
}
