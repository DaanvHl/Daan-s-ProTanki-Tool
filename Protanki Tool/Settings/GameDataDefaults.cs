using ProtankiTool.Models;
using ProtankiTool.Types;

namespace ProtankiTool.Settings
{
    internal static class GameDataDefaults
    {
        public static GameData Create()
        {
            return new GameData
            {
                Paints = CreatePaints(),
                Turrets = CreateTurrets()
            };
        }

        private static Paint P(string name, params (string w, double p)[] protections)
        {
            Paint paint = new() { Name = name };
            foreach ((string w, double p) in protections)
                paint.Protections[w] = p;
            return paint;
        }

        private static List<Paint> CreatePaints() =>
        [
            // ── Garage paints ────────────────────────────────────────────
            P("Red"),
            P("Blue"),
            P("Black"),
            P("White"),
            P("Orange"),
            P("Flora",       ("Smoky",    10)),
            P("Marine",      ("Firebird", 10)),
            P("Swamp",       ("Twins",    10)),
            P("Forester",    ("Railgun",  12)),
            P("Magma",       ("Hammer",   12)),
            P("Safari",      ("Isida",    15)),
            P("Invader",     ("Vulcan",   15)),
            P("Metallic",    ("Thunder",  18)),
            P("Lava",        ("Freeze",   18)),
            P("Dragon",      ("Ricochet", 22)),
            P("Lead",        ("Shaft",    25)),
            P("Mary",        ("Freeze",   15), ("Twins",    8), ("Smoky",    8)),
            P("Storm",       ("Firebird", 17), ("Freeze",   7), ("Hammer",   6)),
            P("Carbon",      ("Twins",     6), ("Smoky",   18), ("Shaft",    6)),
            P("Roger",       ("Firebird",  8), ("Twins",   16), ("Hammer",  10)),
            P("Fracture",    ("Vulcan",   10), ("Smoky",    8), ("Railgun", 16)),
            P("Vortex",      ("Vulcan",   13), ("Ricochet",13), ("Thunder",  8)),
            P("Chainmail",   ("Hammer",   22), ("Isida",   11), ("Railgun",  5)),
            P("Corrosion",   ("Railgun",  10), ("Thunder", 15), ("Shaft",   15)),
            P("Tundra",      ("Smoky",    11), ("Railgun", 27)),
            P("Alien",       ("Isida",    25), ("Freeze",   9), ("Thunder",  9)),
            P("Swash",       ("Isida",    10), ("Firebird",18), ("Twins",   11), ("Smoky", 5)),
            P("Pixel",       ("Hammer",   30), ("Smoky",   16)),
            P("Guerrilla",   ("Hammer",   12), ("Vulcan",  26), ("Shaft",    8)),
            P("Cedar",       ("Isida",     8), ("Thunder", 30), ("Smoky",    8)),
            P("In Love",     ("Isida",    13), ("Freeze",  24), ("Ricochet",10)),
            P("Desert",      ("Twins",     8), ("Ricochet",26), ("Smoky",    8), ("Hammer", 10)),
            P("Dirty",       ("Railgun",  24), ("Shaft",   32)),
            P("Jaguar",      ("Firebird", 17), ("Freeze",  30), ("Thunder", 10)),
            P("Savanna",     ("Ricochet", 15), ("Smoky",   30), ("Railgun", 16)),
            P("Loam",        ("Firebird", 30), ("Freeze",  11), ("Ricochet",11), ("Thunder", 11)),
            P("Sakura",      ("Hammer",   25), ("Vulcan",  12), ("Isida",   25)),
            P("Urban",       ("Isida",    15), ("Firebird",10), ("Twins",   32), ("Ricochet", 10)),
            P("Atom",        ("Hammer",   33), ("Firebird",18), ("Freeze",  11), ("Twins",   11)),
            P("Digital",     ("Smoky",    24), ("Railgun", 35), ("Shaft",   14)),
            P("Hohloma",     ("Freeze",   22), ("Ricochet",30), ("Smoky",   22)),
            P("Rhino",       ("Hammer",   30), ("Vulcan",  30), ("Firebird",18)),
            P("Electra",     ("Isida",    38), ("Twins",   20), ("Smoky",   20)),
            P("Cherry",      ("Vulcan",   22), ("Twins",   34), ("Thunder", 28)),
            P("Blacksmith",  ("Vulcan",   40), ("Railgun", 18), ("Shaft",   26)),
            P("Rustle",      ("Isida",    15), ("Freeze",  30), ("Thunder", 40)),
            P("Python",      ("Freeze",   44), ("Shaft",   40)),
            P("Sandstone",   ("Twins",    12), ("Thunder", 22), ("Railgun", 34), ("Shaft",  22)),
            P("Spark",       ("Firebird", 30), ("Twins",   18), ("Ricochet",42)),
            P("Winter",      ("Firebird", 12), ("Thunder", 26), ("Railgun", 12), ("Shaft",  45)),
            P("Needle",      ("Ricochet", 22), ("Smoky",   50), ("Shaft",   28)),
            P("Zeus",        ("Firebird", 50), ("Freeze",  25), ("Smoky",   25)),
            P("Hive",        ("Vulcan",   42), ("Freeze",  26), ("Ricochet",32)),
            P("Rock",        ("Isida",    28), ("Freeze",  12), ("Twins",   50), ("Hammer", 10)),
            P("Mars",        ("Hammer",   50), ("Firebird",30), ("Isida",   20)),
            P("Prodigi",     ("Firebird",  7), ("Thunder", 23), ("Railgun", 50), ("Shaft",  20)),
            P("Graffiti",    ("Vulcan",   50), ("Twins",   25), ("Shaft",   25)),
            P("Irbis",       ("Isida",    50), ("Freeze",  27), ("Ricochet",23)),
            P("Mirage",      ("Firebird", 35), ("Smoky",   22), ("Thunder", 20), ("Vulcan", 23)),
            P("Emerald",     ("Twins",    35), ("Thunder", 50), ("Railgun", 15)),
            P("Inferno",     ("Firebird", 36), ("Freeze",  50), ("Twins",   14)),
            P("Nano",        ("Hammer",   35), ("Ricochet",32), ("Smoky",   22), ("Firebird",11)),
            P("Raccoon",     ("Hammer",   33), ("Vulcan",  27), ("Isida",   19), ("Railgun", 21)),
            P("Clay",        ("Ricochet", 50), ("Thunder", 20), ("Railgun", 30)),
            P("Taiga",       ("Isida",    35), ("Firebird",24), ("Twins",   17), ("Ricochet",24)),
            P("Tiger",       ("Hammer",   47), ("Smoky",   35), ("Railgun", 18)),
            P("Jade",        ("Thunder",  11), ("Smoky",   24), ("Shaft",   50), ("Hammer",  15)),
            P("Picasso",     ("Freeze",   20), ("Thunder", 20), ("Railgun", 27), ("Shaft",   33)),
            P("Lumberjack",  ("Vulcan",   10), ("Isida",   30), ("Firebird",30), ("Freeze",  30)),
            P("Africa",      ("Hammer",   20), ("Vulcan",  30), ("Twins",   25), ("Thunder", 25)),

            // ── Special paints ───────────────────────────────────────────
            P("Premium Paint", ("Spectrum", 15)),
            P("Moonwalker",    ("Ricochet", 22)),
            P("Eternity"),
            P("Frost",         ("Spectrum", 15)),
            P("Soul Flight"),
            P("Year of the Dragon"),
            P("Arachnid"),
            P("Smiley",        ("Spectrum",  1)),
            P("Tank-Noir",     ("Ricochet", 30), ("Firebird", 30)),
            P("Aurora"),
        ];

        public static List<Turret> CreateTurrets() =>
        [
            new Turret { Name = "Railgun",  DamageType = DamageType.Railgun,  Range = TurretRange.Long,   CanDefend = false },
            new Turret { Name = "Smoky",    DamageType = DamageType.Smoky,    Range = TurretRange.Long,   CanDefend = true  },
            new Turret { Name = "Firebird", DamageType = DamageType.Firebird, Range = TurretRange.Short,  CanDefend = false },
            new Turret { Name = "Vulcan",   DamageType = DamageType.Vulcan,   Range = TurretRange.Long,   CanDefend = true  },
            new Turret { Name = "Twins",    DamageType = DamageType.Twins,    Range = TurretRange.Medium, CanDefend = false },
            new Turret { Name = "Isida",    DamageType = DamageType.Isida,    Range = TurretRange.Short,  CanDefend = false },
            new Turret { Name = "Thunder",  DamageType = DamageType.Thunder,  Range = TurretRange.Long,   CanDefend = false },
            new Turret { Name = "Hammer",   DamageType = DamageType.Hammer,   Range = TurretRange.Medium, CanDefend = false },
            new Turret { Name = "Freeze",   DamageType = DamageType.Freeze,   Range = TurretRange.Short,  CanDefend = false },
            new Turret { Name = "Ricochet", DamageType = DamageType.Ricochet, Range = TurretRange.Medium, CanDefend = true  },
            new Turret { Name = "Shaft",    DamageType = DamageType.Shaft,    Range = TurretRange.Long,   CanDefend = true  },
        ];
    }
}
