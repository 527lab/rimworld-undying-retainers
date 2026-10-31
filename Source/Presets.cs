using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace UndyingRetainers
{
    public class RaisePreset : IExposable, IRenameable
    {
        public string name = "";
        public int gender; // 0 any, 1 male, 2 female
        public int ageMin;
        public int ageMax = 120;
        public int skillMode;
        public int allSkillLevel = 8;
        public Dictionary<string, int> perSkill = new Dictionary<string, int>();
        public List<string> disallowedWork = new List<string>();

        public string RenamableLabel
        {
            get => name;
            set => name = value;
        }

        public string BaseLabel => name;

        public string InspectLabel => name;

        public void ExposeData()
        {
            Scribe_Values.Look(ref name, "name", "");
            Scribe_Values.Look(ref gender, "gender", 0);
            Scribe_Values.Look(ref ageMin, "ageMin", 0);
            Scribe_Values.Look(ref ageMax, "ageMax", 120);
            Scribe_Values.Look(ref skillMode, "skillMode", 0);
            Scribe_Values.Look(ref allSkillLevel, "allSkillLevel", 8);
            Scribe_Collections.Look(ref perSkill, "perSkill", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref disallowedWork, "disallowedWork", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (name == null)
                {
                    name = "";
                }
                if (perSkill == null)
                {
                    perSkill = new Dictionary<string, int>();
                }
                if (disallowedWork == null)
                {
                    disallowedWork = new List<string>();
                }
            }
        }

        public RaisePreset Clone()
        {
            var p = new RaisePreset();
            p.CopyFrom(this);
            return p;
        }

        public void CopyFrom(RaisePreset o)
        {
            if (o == null)
            {
                return;
            }
            name = o.name;
            gender = o.gender;
            ageMin = o.ageMin;
            ageMax = o.ageMax;
            skillMode = o.skillMode;
            allSkillLevel = o.allSkillLevel;
            perSkill = o.perSkill == null ? new Dictionary<string, int>() : new Dictionary<string, int>(o.perSkill);
            disallowedWork = o.disallowedWork == null ? new List<string>() : new List<string>(o.disallowedWork);
        }

        /// <summary>Stable string of everything except the name; used to detect "modified".</summary>
        public string Signature()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(gender).Append('|').Append(ageMin).Append('-').Append(ageMax).Append('|').Append(skillMode).Append('|').Append(allSkillLevel).Append('|');
            if (perSkill != null)
            {
                foreach (KeyValuePair<string, int> kv in perSkill.Where(x => x.Value >= 0).OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    sb.Append(kv.Key).Append('=').Append(kv.Value).Append(',');
                }
            }
            sb.Append('|');
            if (disallowedWork != null)
            {
                foreach (string w in disallowedWork.Distinct().OrderBy(x => x, StringComparer.Ordinal))
                {
                    sb.Append(w).Append(',');
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>Names a preset. A new preset is only added to the list after OK; duplicates get " 2", " 3"...</summary>
    public class Dialog_RenamePreset : Dialog_Rename<RaisePreset>
    {
        private readonly bool isNew;

        public Dialog_RenamePreset(RaisePreset preset, bool isNew) : base(preset)
        {
            this.isNew = isNew;
        }

        protected override AcceptanceReport NameIsValid(string name)
        {
            return name != null && name.Trim().Length > 0;
        }

        protected override void OnRenamed(string name)
        {
            renaming.name = PresetUtility.UniqueName(name.Trim(), isNew ? null : renaming);
            if (isNew)
            {
                PresetUtility.Add(renaming);
                PresetUtility.Current = renaming;
            }
            PresetUtility.Save();
        }
    }

    public static class PresetUtility
    {
        /// <summary>The preset most recently applied/saved in this session (not persisted).</summary>
        public static RaisePreset Current;

        public static List<RaisePreset> All => UndyingRetainersMod.Settings.presets;

        public static RaisePreset CurrentValid
        {
            get
            {
                if (Current != null && !All.Contains(Current))
                {
                    Current = null;
                }
                return Current;
            }
        }

        public static void Save()
        {
            LoadedModManager.GetMod<UndyingRetainersMod>().WriteSettings();
        }

        public static string UniqueName(string wanted, RaisePreset exclude)
        {
            string baseName = wanted;
            string result = baseName;
            int n = 2;
            while (All.Any(p => p != exclude && p.name == result))
            {
                result = baseName + " " + n;
                n++;
            }
            return result;
        }

        public static string DefaultName()
        {
            return UniqueName("UR_Preset_DefaultName".Translate((All.Count + 1).ToString()), null);
        }

        public static RaisePreset CaptureCurrent(string name)
        {
            URSettings s = UndyingRetainersMod.Settings;
            SummonPage.GetFilters(out int g, out int amin, out int amax);
            var p = new RaisePreset
            {
                name = name,
                gender = g,
                ageMin = amin,
                ageMax = amax,
                skillMode = s.skillMode,
                allSkillLevel = s.allSkillLevel,
                perSkill = new Dictionary<string, int>(s.perSkill),
                disallowedWork = new List<string>(s.disallowedWork)
            };
            return p;
        }

        public static void Add(RaisePreset p)
        {
            if (p != null && !All.Contains(p))
            {
                All.Add(p);
            }
        }

        public static void Overwrite(RaisePreset target)
        {
            if (target == null)
            {
                return;
            }
            target.CopyFrom(CaptureCurrent(target.name));
            Current = target;
            Save();
        }

        public static void Delete(RaisePreset p)
        {
            if (p == null)
            {
                return;
            }
            All.Remove(p);
            if (Current == p)
            {
                Current = null;
            }
            Save();
        }

        public static void Apply(RaisePreset p)
        {
            if (p == null)
            {
                return;
            }
            URSettings s = UndyingRetainersMod.Settings;
            s.skillMode = p.skillMode;
            s.allSkillLevel = p.allSkillLevel;
            s.perSkill = p.perSkill == null ? new Dictionary<string, int>() : new Dictionary<string, int>(p.perSkill);
            s.disallowedWork = p.disallowedWork == null ? new List<string>() : new List<string>(p.disallowedWork);
            SummonPage.SetFilters(p.gender, p.ageMin, p.ageMax);
            Current = p;
            Save();
        }

        public static bool IsModified()
        {
            RaisePreset cur = CurrentValid;
            return cur != null && CaptureCurrent(cur.name).Signature() != cur.Signature();
        }

        /// <summary>One option per saved preset; picking one calls pick.</summary>
        public static List<FloatMenuOption> Options(Action<RaisePreset> pick)
        {
            var opts = new List<FloatMenuOption>();
            foreach (RaisePreset p in All)
            {
                RaisePreset local = p;
                opts.Add(new FloatMenuOption(p.name, () => pick(local)));
            }
            return opts;
        }

        /// <summary>Full menu for the summon window.</summary>
        public static List<FloatMenuOption> SummonMenuOptions()
        {
            var opts = Options(Apply);
            opts.Add(new FloatMenuOption("UR_Preset_SaveNew".Translate(), () =>
            {
                Find.WindowStack.Add(new Dialog_RenamePreset(CaptureCurrent(DefaultName()), true));
            }));
            RaisePreset cur = CurrentValid;
            if (cur != null)
            {
                opts.Add(new FloatMenuOption("UR_Preset_Overwrite".Translate(cur.name), () => Overwrite(cur)));
                opts.Add(new FloatMenuOption("UR_Preset_Rename".Translate(cur.name), () => Find.WindowStack.Add(new Dialog_RenamePreset(cur, false))));
                opts.Add(new FloatMenuOption("UR_Preset_Delete".Translate(cur.name), () =>
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("UR_Preset_DeleteConfirm".Translate(cur.name), () => Delete(cur), destructive: true));
                }));
            }
            return opts;
        }
    }
}
