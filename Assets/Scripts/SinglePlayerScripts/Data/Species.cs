using System;
using System.Collections.Generic;

[Serializable]
public class Species
{
    public string name;
    public PokeType type;
    public int maxHP;
    public List<Move> moves = new();
}

public class PokeDor
{
    public Species baseData;
    public int hp;
    public PokeDor(Species s) { baseData = s; hp = s.maxHP; }
    public Move RandomMove()
    {
        var list = baseData.moves;
        return list[UnityEngine.Random.Range(0, list.Count)];
    }
}
