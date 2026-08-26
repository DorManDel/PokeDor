using System.Collections.Generic;

public static class PokeDex
{
    public static List<Species> CreatePokeDors()
    {
        return new List<Species>
        {
            New("EmberKit",       PokeType.Fire,     48, ("Flame",PokeType.Fire,18),    ("Scratch",PokeType.Normal,12)),
            New("AquaBud",        PokeType.Water,    52, ("Splash",PokeType.Water,18),  ("Tackle",PokeType.Normal,12)),
            New("LeafCub",        PokeType.Grass,    50, ("Vine",PokeType.Grass,18),    ("Pound",PokeType.Normal,12)),
            New("VoltPup",        PokeType.Electric, 46, ("Zap",PokeType.Electric,20),  ("Nip",PokeType.Normal,10)),
            New("Pebblin",        PokeType.Rock,     56, ("Stone",PokeType.Rock,20),    ("Bash",PokeType.Normal,10)),
            New("Mewt",           PokeType.Normal,   54, ("Swipe",PokeType.Normal,16),  ("Headbutt",PokeType.Normal,14)),
            New("Snoamlax",       PokeType.Normal,   58, ("Snooze",    PokeType.Normal, 16), ("BellyBump", PokeType.Normal, 20)),
            New("Bulbazachman",  PokeType.Grass,     52, ("Sprout",    PokeType.Grass,  18), ("ThornJab",  PokeType.Grass,  12)),
            New("Charmandor",     PokeType.Fire,     50, ("EmberSpin", PokeType.Fire,   18), ("CinderClaw",PokeType.Fire,   12)),
            New("Sneavel",        PokeType.Water,    48, ("FrostCut",  PokeType.Water,    18), ("QuickSlash",PokeType.Normal, 12)),
            New("Natam",          PokeType.Normal,   46, ("Headbonk",  PokeType.Normal, 14), ("Feint",     PokeType.Normal, 16)),
            New("Eemeer",         PokeType.Electric, 47, ("Sparklet",  PokeType.Electric,20), ("StaticNip",PokeType.Normal, 10)),
            New("Tentalex",       PokeType.Water,    53, ("AquaWhip",  PokeType.Water,  18), ("InkBash",   PokeType.Normal, 12)),
            New("Electronen",     PokeType.Electric, 51, ("IonZap",    PokeType.Electric,20), ("PulseTap", PokeType.Normal, 10)),
            New("Chuchana",       PokeType.Electric, 51, ("Slap",      PokeType.Rock,20), ("YesDaddy", PokeType.Normal, 45))
        };
    }


    static Species New(string n, PokeType t, int hp, params (string, PokeType, int)[] ms)
    {
        var s = new Species { name = n, type = t, maxHP = hp };
        foreach (var (mn, mt, pow) in ms)
            s.moves.Add(new Move { name = mn, type = mt, power = pow });
        return s;
    }
}
