using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LogEnableEasterEgg : MonoBehaviour
{
    void OnEnable() { Debug.Log("[EggPanel] ENABLED"); }
    void OnDisable() { Debug.Log("[EggPanel] DISABLED"); }
}
