// activates openworld scene
using UnityEngine;

public class BattleTrigger : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        App.I.BeginBattle();
    }
}
