using System.Collections.Generic;

public static class TypeChart
{
    //Dicktionary
    static readonly Dictionary<(PokeType, PokeType), float> table = new()
    {
        //0 not effective, 0.5 not verry effective, 1.0 normal , 2.0 super effective
        {(PokeType.Fire,        PokeType.Grass),         2f},
        {(PokeType.Grass,       PokeType.Water),         2f},
        {(PokeType.Water,       PokeType.Fire),          2f},
        {(PokeType.Electric,    PokeType.Water),         2f},
        {(PokeType.Rock,        PokeType.Fire),          2f},

        {(PokeType.Fire,        PokeType.Water),        0.5f},
        {(PokeType.Grass,       PokeType.Fire),         0.5f},
        {(PokeType.Water,       PokeType.Grass),        0.5f},
        {(PokeType.Electric,    PokeType.Grass),        0.5f},
    };
    public static float Mult(PokeType atk, PokeType def) =>
        table.TryGetValue((atk, def), out var m) ? m : 1f;
}
