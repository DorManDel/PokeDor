using UnityEngine;
using System.Collections.Generic;

public class IndexManager : MonoBehaviour
{
    private List<DexIndex> picked = new List<DexIndex>();
    private int maxPicks = 6;

    public void OnPick(GameObject btnObj)
    {
        var dex = btnObj.GetComponent<DexIndex>();
        if (!dex) return;

        if (dex.IsSelected)   // deselect
        {
            picked.Remove(dex);
            dex.ClearIndex();
        }
        else if (picked.Count < maxPicks) // add new
        {
            picked.Add(dex);
        }

        // Reorder indexes so numbers are always 1..N
        for (int i = 0; i < picked.Count; i++)
            picked[i].SetIndex(i + 1);
    }

    public void ResetAll()
    {
        foreach (var dex in picked)
            dex.ClearIndex();
        picked.Clear();
    }

    public void Randomize()
    {
        ResetAll();

        var all = new List<DexIndex>(FindObjectsOfType<DexIndex>());
        for (int i = 0; i < maxPicks; i++)
        {
            var choice = all[Random.Range(0, all.Count)];
            if (!picked.Contains(choice))
                picked.Add(choice);
        }

        for (int i = 0; i < picked.Count; i++)
            picked[i].SetIndex(i + 1);
    }

    public List<string> GetPickedNames()
    {
        List<string> names = new List<string>();
        foreach (var dex in picked)
            names.Add(dex.name); // or dex.label.text
        return names;
    }
}
