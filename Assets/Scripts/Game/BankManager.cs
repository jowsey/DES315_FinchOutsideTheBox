using System;
using System.Collections.Generic;
using UnityEngine;

public static class BankManager
{
    //The players' current joint balance
    public static int Balance { get; private set; }

    public static void AddToBalance(int val) => Balance += val;
    public static void SubtractFromBalance(int val) => Balance -= val;
}
