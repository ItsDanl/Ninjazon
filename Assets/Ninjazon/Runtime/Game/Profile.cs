using System;
using System.IO;
using UnityEngine;

namespace Ninjazon.Game
{
    /// <summary>Everything that is saved between play sessions.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public int version = 2;
        public int credits;
        public int bestScore;
        public float bestDistance;
        public int totalRuns;
        public int stars;               // mission stars -> courier rank
        public int missionsCompleted;
        public int[] upgrades = new int[Upgrades.Count];
        public int trail;               // equipped cosmetic trail
        public int ownedTrails = 1;     // bit mask, trail 0 is free
        public bool tutorialDone;
        public float musicVolume = 0.7f;
        public float sfxVolume = 0.85f;
        public bool vibration = true;
        public bool screenShake = true;
        public bool controlHints = true;
        public MissionSlot[] missions = new MissionSlot[3];
        // lifetime stats
        public float lifetimeDistance;
        public int lifetimeSlices;
        public int lifetimeCredits;
    }

    [Serializable]
    public sealed class MissionSlot
    {
        public int template;
        public int target;
        public int reward;
        public int progress;
        public bool complete;
    }

    // ------------------------------------------------------------- upgrades --
    public static class Upgrades
    {
        public const int Dash = 0, Magnet = 1, Overclock = 2, Armor = 3, Fortune = 4;
        public const int Count = 5;

        public static readonly string[] Names = { "DASH CORE", "MAGNET COIL", "OVERCLOCK", "AEGIS PLATING", "LUCKY CHARM" };
        public static readonly string[] Icons = { "dash", "magnet", "bolt", "heart", "star" };
        public static readonly int[][] Costs =
        {
            new[] { 150, 350, 700, 1300, 2200 },
            new[] { 120, 300, 650, 1200, 2000 },
            new[] { 120, 300, 650, 1200, 2000 },
            new[] { 900, 2600 },
            new[] { 400, 950, 1800 },
        };

        public static int MaxLevel(int id) => Costs[id].Length;

        public static string Describe(int id, int level) => id switch
        {
            Dash => $"Dash recharges in {DashCooldown(level):0.0}s",
            Magnet => $"Magnet lasts {MagnetTime(level):0}s",
            Overclock => $"2x credits lasts {OverclockTime(level):0}s",
            Armor => $"Start runs with {MaxPips(level)} health",
            Fortune => $"Power-up rate {Mathf.RoundToInt(FortuneMultiplier(level) * 100)}%",
            _ => string.Empty
        };

        public static float DashCooldown(int level) => 3.0f - 0.3f * level;
        public static float MagnetTime(int level) => 6f + 2f * level;
        public static float OverclockTime(int level) => 6f + 2f * level;
        public static int MaxPips(int level) => 3 + level;
        public static float FortuneMultiplier(int level) => 1f + 0.25f * level;
    }

    // ------------------------------------------------------------- trails ---
    public static class Trails
    {
        public static readonly string[] Names = { "NEON TIDE", "SAKURA PULSE", "GOLD RUSH", "TOXIC MINT", "ULTRAVIOLET", "GHOST", "CRIMSON" };
        public static readonly Color[] Colors =
        {
            Palette.Cyan, Palette.Magenta, Palette.Amber, Palette.Mint, Palette.Violet, new Color(0.92f, 0.95f, 1f), Palette.Red
        };
        public static readonly int[] Costs = { 0, 300, 600, 900, 1300, 1800, 2500 };
        public static int Count => Names.Length;
    }

    // ------------------------------------------------------------- missions -
    public enum MissionKind
    {
        RunDistance, CollectCredits, Slices, LaneSwitches, DoubleJumps, ReachMultiplier,
        PowerUps, TotalDistance, CleanDistance, PlayRuns, CloseCalls
    }

    public static class Missions
    {
        private struct Template
        {
            public MissionKind kind; public string text; public int baseTarget; public int step; public bool perRun;
            public Template(MissionKind k, string t, int b, int s, bool p) { kind = k; text = t; baseTarget = b; step = s; perRun = p; }
        }

        private static readonly Template[] Pool =
        {
            new(MissionKind.RunDistance, "Run {0}m in one run", 500, 250, true),
            new(MissionKind.CollectCredits, "Grab {0} credits in one run", 60, 30, true),
            new(MissionKind.Slices, "Dash-slice {0} drones or barricades", 3, 2, true),
            new(MissionKind.LaneSwitches, "Switch lanes {0} times in one run", 25, 10, true),
            new(MissionKind.DoubleJumps, "Double jump {0} times in one run", 6, 3, true),
            new(MissionKind.ReachMultiplier, "Reach a x{0} multiplier", 3, 1, true),
            new(MissionKind.PowerUps, "Collect {0} power-ups in one run", 2, 1, true),
            new(MissionKind.TotalDistance, "Run {0}m in total", 2000, 1500, false),
            new(MissionKind.CleanDistance, "Run {0}m without getting hit", 300, 150, true),
            new(MissionKind.PlayRuns, "Complete {0} runs", 3, 2, false),
            new(MissionKind.CloseCalls, "Pull off {0} close dodges in one run", 4, 2, true),
        };

        public static int PoolSize => Pool.Length;
        public static MissionKind Kind(MissionSlot slot) => Pool[slot.template].kind;
        public static bool PerRun(MissionSlot slot) => Pool[slot.template].perRun;
        public static string Text(MissionSlot slot) => string.Format(Pool[slot.template].text, slot.target);

        public static MissionSlot Create(int template, int tier)
        {
            var t = Pool[template];
            var target = t.baseTarget + t.step * tier;
            if (t.kind == MissionKind.ReachMultiplier) target = Mathf.Min(5, target);
            return new MissionSlot { template = template, target = target, reward = 100 + 60 * tier, progress = 0 };
        }

        public static MissionSlot CreateRandom(int tier, MissionSlot[] existing)
        {
            for (var attempt = 0; attempt < 30; attempt++)
            {
                var template = UnityEngine.Random.Range(0, Pool.Length);
                var clash = false;
                if (existing != null)
                    foreach (var slot in existing)
                        if (slot != null && slot.template == template && !slot.complete) clash = true;
                if (!clash) return Create(template, tier);
            }
            return Create(0, tier);
        }
    }

    /// <summary>
    /// Loads and saves <see cref="SaveData"/> as JSON in Application.persistentDataPath.
    /// Also migrates the old v1 save so existing players keep their credits.
    /// </summary>
    public sealed class Profile
    {
        public SaveData Data { get; private set; }
        public event Action Changed;

        private readonly string path;

        public Profile()
        {
            path = Path.Combine(Application.persistentDataPath, "ninjazon-save-v2.json");
            Data = Load();
            Repair();
        }

        public int Rank => 1 + Data.stars / 3;
        public int StarsIntoRank => Data.stars % 3;
        public int Tier => Mathf.Min(8, Data.missionsCompleted / 3);
        public Color TrailColor => Trails.Colors[Mathf.Clamp(Data.trail, 0, Trails.Count - 1)];
        public int Level(int upgrade) => Data.upgrades[upgrade];
        public bool OwnsTrail(int i) => (Data.ownedTrails & (1 << i)) != 0;

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Data.credits < amount) return false;
            Data.credits -= amount;
            Save();
            return true;
        }

        public void AddCredits(int amount)
        {
            Data.credits += Mathf.Max(0, amount);
            Save();
        }

        public bool BuyUpgrade(int id)
        {
            var level = Data.upgrades[id];
            if (level >= Upgrades.MaxLevel(id)) return false;
            if (!TrySpend(Upgrades.Costs[id][level])) return false;
            Data.upgrades[id] = level + 1;
            Save();
            return true;
        }

        public bool BuyOrEquipTrail(int i)
        {
            if (!OwnsTrail(i))
            {
                if (!TrySpend(Trails.Costs[i])) return false;
                Data.ownedTrails |= 1 << i;
            }
            Data.trail = i;
            Save();
            return true;
        }

        /// <summary>Replace completed missions with new ones. Returns how many were replaced.</summary>
        public int RefreshMissions()
        {
            var replaced = 0;
            for (var i = 0; i < Data.missions.Length; i++)
            {
                if (Data.missions[i] != null && !Data.missions[i].complete) continue;
                Data.missions[i] = Missions.CreateRandom(Tier, Data.missions);
                replaced++;
            }
            if (replaced > 0) Save();
            return replaced;
        }

        public void Save()
        {
            try
            {
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(Data, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ninjazon] Save failed: " + e.Message);
            }
            Changed?.Invoke();
        }

        private SaveData Load()
        {
            try
            {
                if (File.Exists(path))
                {
                    var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                    if (data != null) return data;
                }
                var legacy = Path.Combine(Application.persistentDataPath, "ninjazon-save-v1.json");
                if (File.Exists(legacy))
                {
                    var old = JsonUtility.FromJson<Runtime.Persistence.SaveDataV1>(File.ReadAllText(legacy));
                    if (old != null)
                    {
                        return new SaveData
                        {
                            credits = old.lifetimeCredits,
                            bestScore = old.bestScore,
                            bestDistance = old.bestDistance,
                            musicVolume = old.musicVolume,
                            sfxVolume = old.sfxVolume,
                            vibration = old.vibration,
                            screenShake = old.screenShake,
                            controlHints = old.controlHints,
                        };
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ninjazon] Load failed, starting fresh: " + e.Message);
            }
            return new SaveData();
        }

        private void Repair()
        {
            if (Data.upgrades == null || Data.upgrades.Length != Upgrades.Count)
            {
                var fixedUpgrades = new int[Upgrades.Count];
                if (Data.upgrades != null)
                    for (var i = 0; i < Mathf.Min(Data.upgrades.Length, fixedUpgrades.Length); i++) fixedUpgrades[i] = Data.upgrades[i];
                Data.upgrades = fixedUpgrades;
            }
            for (var i = 0; i < Upgrades.Count; i++) Data.upgrades[i] = Mathf.Clamp(Data.upgrades[i], 0, Upgrades.MaxLevel(i));
            Data.ownedTrails |= 1;
            if (!OwnsTrail(Data.trail)) Data.trail = 0;
            if (Data.missions == null || Data.missions.Length != 3) Data.missions = new MissionSlot[3];
            for (var i = 0; i < 3; i++)
                if (Data.missions[i] == null || Data.missions[i].template < 0 || Data.missions[i].template >= Missions.PoolSize || Data.missions[i].target <= 0)
                    Data.missions[i] = Missions.CreateRandom(Tier, Data.missions);
            Save();
        }
    }
}
