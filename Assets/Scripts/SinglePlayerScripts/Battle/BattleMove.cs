// Assets/Scripts/SinglePlayerScripts/Battle/BattleMove.cs
// summary: forwards button click with an index to the BattleLogic, decoupled like Slot.OnClickSlot(TicTacToe style)

using System;
using UnityEngine;

public class BattleMove : MonoBehaviour
{
    /// <summary>
    /// Events system 
    /// </summary>
    public static Action<int> OnClickMove;
    public int index;
    public void Click() => OnClickMove?.Invoke(index);  // like NullCheck_ if not = DO!
}
