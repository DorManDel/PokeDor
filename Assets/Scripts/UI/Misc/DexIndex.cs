using UnityEngine;
using TMPro;

public class DexIndex : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI txtIndex;   // attach Txt_Index child
    private int assignedIndex = 0;

    public bool IsSelected => assignedIndex > 0;

    public void SetIndex(int idx)
    {
        assignedIndex = idx;
        txtIndex.text = idx > 0 ? idx.ToString() : "";
    }

    public void ClearIndex()
    {
        assignedIndex = 0;
        txtIndex.text = "";
    }
}
