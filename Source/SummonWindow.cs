using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace UndyingRetainers
{
    public class SummonPage
    {
        private static Area area;
        private static int gender; // 0 any, 1 male, 2 female
        private static IntRange ageRange = new IntRange(0, 120);
        private static readonly HashSet<Corpse> selected = new HashSet<Corpse>();
        private static Pawn raiseMaster;
        private static int raiseHousehold = -1;

        private Vector2 listScroll;
        private Vector2 profileScroll;

        private static Map Map => Find.CurrentMap;

        public static void GetFilters(out int genderFilter, out int ageMin, out int ageMax)
        {
            genderFilter = gender;
            ageMin = ageRange.min;
            ageMax = ageRange.max;
        }

        public static void SetFilters(int genderFilter, int ageMin, int ageMax)
        {
            gender = Mathf.Clamp(genderFilter, 0, 2);
            int lo = Mathf.Clamp(ageMin, 0, 120);
            int hi = Mathf.Clamp(ageMax, 0, 120);
            if (hi < lo)
            {
                hi = lo;
            }
            ageRange = new IntRange(lo, hi);
        }

        private static List<Corpse> Candidates()
        {
            var result = new List<Corpse>();
            if (Map == null)
            {
                return result;
            }
            if (area != null && !Map.areaManager.AllAreas.Contains(area))
            {
                area = null;
            }
            foreach (Thing t in Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
            {
                if (!(t is Corpse c) || !RetainerUtility.CanRaise(c))
                {
                    continue;
                }
                Pawn p = c.InnerPawn;
                if (area != null && !area[c.Position])
                {
                    continue;
                }
                if (gender == 1 && p.gender != Gender.Male)
                {
                    continue;
                }
                if (gender == 2 && p.gender != Gender.Female)
                {
                    continue;
                }
                int age = p.ageTracker.AgeBiologicalYears;
                if (age < ageRange.min || age > ageRange.max)
                {
                    continue;
                }
                result.Add(c);
            }
            return result;
        }

        private static string GenderLabel(int g)
        {
            return ("UR_Gender_" + g).Translate();
        }

        public void Draw(Rect inRect)
        {
            if (Map == null)
            {
                Widgets.Label(inRect, "UR_NoMap".Translate());
                return;
            }
            List<Corpse> candidates = Candidates();
            selected.RemoveWhere(c => c == null || c.Destroyed || !candidates.Contains(c));

            Rect left = new Rect(inRect.x, inRect.y, inRect.width * 0.5f - 8f, inRect.height - 44f);
            Rect right = new Rect(inRect.x + inRect.width * 0.5f + 8f, inRect.y, inRect.width * 0.5f - 8f, inRect.height - 44f);
            DrawFilters(left, candidates);
            DrawProfile(right);

            Rect bottom = new Rect(inRect.x, inRect.yMax - 38f, inRect.width, 38f);
            if (Widgets.ButtonText(bottom, "UR_Summon_Raise".Translate(selected.Count.ToString())) && selected.Count > 0)
            {
                foreach (Corpse c in selected.ToList())
                {
                    if (c == null || c.Destroyed || c.InnerPawn == null)
                    {
                        continue;
                    }
                    Pawn p = c.InnerPawn;
                    if (RetainerUtility.Raise(c))
                    {
                        Hediff_RetainerContract k = HouseholdUtility.Contract(p);
                        if (k != null)
                        {
                            k.master = raiseMaster;
                            k.householdId = raiseHousehold;
                        }
                    }
                }
                selected.Clear();
                LoadedModManager.GetMod<UndyingRetainersMod>().WriteSettings();
            }
        }

        private void DrawFilters(Rect rect, List<Corpse> candidates)
        {
            var l = new Listing_Standard();
            l.Begin(rect);
            Text.Font = GameFont.Medium;
            l.Label("UR_Summon_Title".Translate());
            Text.Font = GameFont.Small;
            l.GapLine();

            string areaLabel = area == null ? (string)"UR_Summon_WholeMap".Translate() : area.Label;
            if (l.ButtonTextLabeledPct("UR_Summon_Area".Translate(), areaLabel, 0.3f))
            {
                var opts = new List<FloatMenuOption> { new FloatMenuOption("UR_Summon_WholeMap".Translate(), () => area = null) };
                foreach (Area a in Map.areaManager.AllAreas)
                {
                    Area local = a;
                    opts.Add(new FloatMenuOption(a.Label, () => area = local));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            if (l.ButtonTextLabeledPct("UR_Summon_Gender".Translate(), GenderLabel(gender), 0.3f))
            {
                gender = (gender + 1) % 3;
            }
            l.Label("UR_Summon_Age".Translate(ageRange.min.ToString(), ageRange.max.ToString()));
            Rect ageRect = l.GetRect(32f);
            Widgets.IntRange(ageRect, 4271, ref ageRange, 0, 120);
            l.Gap(4f);

            Rect btnRow = l.GetRect(28f);
            if (Widgets.ButtonText(btnRow.LeftHalf().ContractedBy(2f), "UR_Summon_All".Translate()))
            {
                foreach (Corpse c in candidates)
                {
                    selected.Add(c);
                }
            }
            if (Widgets.ButtonText(btnRow.RightHalf().ContractedBy(2f), "UR_Summon_None".Translate()))
            {
                selected.Clear();
            }
            l.Label("UR_Summon_Found".Translate(candidates.Count.ToString()));
            float used = l.CurHeight;
            l.End();

            Rect outRect = new Rect(rect.x, rect.y + used, rect.width, rect.height - used);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, candidates.Count * 28f);
            Widgets.BeginScrollView(outRect, ref listScroll, view);
            float y = 0f;
            foreach (Corpse c in candidates)
            {
                Pawn p = c.InnerPawn;
                Rect row = new Rect(0f, y, view.width, 26f);
                bool on = selected.Contains(c);
                bool was = on;
                string factionLabel = p.Faction == null ? "-" : p.Faction.Name;
                string label = p.LabelShortCap + "  (" + GenderLabel(p.gender == Gender.Female ? 2 : 1) + ", " + p.ageTracker.AgeBiologicalYears + ", " + factionLabel + ")";
                Widgets.CheckboxLabeled(row, label, ref on, paintable: true);
                if (on != was)
                {
                    if (on)
                    {
                        selected.Add(c);
                    }
                    else
                    {
                        selected.Remove(c);
                    }
                }
                y += 28f;
            }
            Widgets.EndScrollView();
        }

        private float profileHeight = 800f;

        private void DrawProfile(Rect rect)
        {
            URSettings s = UndyingRetainersMod.Settings;
            Rect view = new Rect(0f, 0f, rect.width - 16f, profileHeight);
            Widgets.BeginScrollView(rect, ref profileScroll, view);
            float w = view.width;
            float y = 0f;

            // Presets
            y = UIKit.Section(y, w, "UR_Preset_Section".Translate());
            RaisePreset curPreset = PresetUtility.CurrentValid;
            string presetName = curPreset == null ? (string)"UR_Preset_None".Translate() : curPreset.name;
            if (PresetUtility.IsModified())
            {
                presetName += (string)"UR_Preset_Modified".Translate();
            }
            if (Widgets.ButtonText(new Rect(0f, y, w, 28f), "UR_Preset_Label".Translate(presetName)))
            {
                Find.WindowStack.Add(new FloatMenu(PresetUtility.SummonMenuOptions()));
            }
            y += 40f;

            // Belongs to
            if (raiseMaster != null && (raiseMaster.Dead || raiseMaster.Discarded))
            {
                raiseMaster = null;
            }
            if (HouseholdUtility.Get(raiseHousehold) == null)
            {
                raiseHousehold = -1;
            }
            y = UIKit.Section(y, w, "UR_Section_Belong".Translate());
            float half = (w - 10f) / 2f;
            string mLabel = raiseMaster == null ? (string)"UR_None".Translate() : raiseMaster.LabelShort;
            if (UIKit.LabeledButton(new Rect(0f, y, half, 28f), "UR_Summon_Master".Translate(), mLabel))
            {
                Find.WindowStack.Add(new FloatMenu(HouseholdUtility.MasterOptions(null, m => raiseMaster = m)));
            }
            Household hh = HouseholdUtility.Get(raiseHousehold);
            if (UIKit.LabeledButton(new Rect(half + 10f, y, half, 28f), "UR_Summon_Household".Translate(), hh == null ? (string)"UR_None".Translate() : hh.name))
            {
                Find.WindowStack.Add(new FloatMenu(HouseholdUtility.HouseholdOptions(hh, id => raiseHousehold = id)));
            }
            y += 40f;

            // Skills
            y = UIKit.Section(y, w, "UR_Profile_SkillMode".Translate());
            string[] modes = { "UR_SkillMode_0".Translate(), "UR_SkillMode_1".Translate(), "UR_SkillMode_2".Translate() };
            s.skillMode = UIKit.Segmented(new Rect(0f, y, w, 30f), modes, s.skillMode);
            y += 38f;

            if (s.skillMode == 0)
            {
                GUI.color = UIKit.DimText;
                Widgets.Label(new Rect(0f, y, w, 24f), "UR_Profile_KeepHint".Translate());
                GUI.color = Color.white;
                y += 30f;
            }
            else if (s.skillMode == 1)
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(0f, y, 90f, 24f), "UR_Profile_AllSkillsShort".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                s.allSkillLevel = UIKit.SkillBar(new Rect(94f, y, w - 94f, 24f), s.allSkillLevel, allowKeep: false);
                y += 34f;
            }
            else
            {
                GUI.color = UIKit.DimText;
                Widgets.Label(new Rect(0f, y + 2f, w - 110f, 24f), "UR_Profile_PerSkillHint".Translate());
                GUI.color = Color.white;
                if (Widgets.ButtonText(new Rect(w - 100f, y, 100f, 24f), "UR_Profile_AllKeep".Translate()))
                {
                    s.perSkill.Clear();
                }
                y += 32f;
                List<SkillDef> skills = DefDatabase<SkillDef>.AllDefsListForReading;
                float colW = (w - 16f) / 2f;
                for (int i = 0; i < skills.Count; i++)
                {
                    SkillDef sk = skills[i];
                    Rect cell = new Rect((i % 2) * (colW + 16f), y + (i / 2) * 30f, colW, 24f);
                    int v = s.perSkill.TryGetValue(sk.defName, out int cur) ? cur : -1;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(cell.x, cell.y, 62f, cell.height), sk.skillLabel.CapitalizeFirst());
                    Text.Anchor = TextAnchor.UpperLeft;
                    v = UIKit.SkillBar(new Rect(cell.x + 64f, cell.y, cell.width - 64f - 28f, cell.height), v, allowKeep: true);
                    Rect keep = new Rect(cell.xMax - 24f, cell.y, 24f, 24f);
                    TooltipHandler.TipRegion(keep, "UR_Profile_Keep".Translate());
                    if (v >= 0 && Widgets.ButtonText(keep, "↺"))
                    {
                        v = -1;
                    }
                    s.perSkill[sk.defName] = v;
                }
                y += ((skills.Count + 1) / 2) * 30f + 6f;
            }

            // Work
            y = UIKit.Section(y, w, "UR_Profile_WorkShort".Translate());
            List<WorkTypeDef> works = DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(x => x.visible).ToList();
            if (Widgets.ButtonText(new Rect(w - 158f, y - 28f, 76f, 22f), "UR_Summon_All".Translate()))
            {
                s.disallowedWork.Clear();
            }
            if (Widgets.ButtonText(new Rect(w - 78f, y - 28f, 78f, 22f), "UR_Summon_None".Translate()))
            {
                s.disallowedWork = works.Select(x => x.defName).ToList();
            }
            GUI.color = UIKit.DimText;
            Widgets.Label(new Rect(0f, y, w, 22f), "UR_Profile_WorkHint".Translate());
            GUI.color = Color.white;
            y += 26f;
            const int cols = 3;
            const float gap = 6f;
            float tileW = (w - gap * (cols - 1)) / cols;
            for (int i = 0; i < works.Count; i++)
            {
                WorkTypeDef wt = works[i];
                Rect tile = new Rect((i % cols) * (tileW + gap), y + (i / cols) * 32f, tileW, 28f);
                bool allowed = !s.disallowedWork.Contains(wt.defName);
                string wl = wt.labelShort.NullOrEmpty() ? (string)wt.LabelCap : wt.labelShort.CapitalizeFirst();
                bool now = UIKit.ToggleTile(tile, wl, allowed);
                if (now != allowed)
                {
                    if (now)
                    {
                        s.disallowedWork.Remove(wt.defName);
                    }
                    else
                    {
                        s.disallowedWork.Add(wt.defName);
                    }
                }
            }
            y += ((works.Count + cols - 1) / cols) * 32f + 8f;

            Widgets.EndScrollView();
            profileHeight = y;
        }

        public void OnClose()
        {
            LoadedModManager.GetMod<UndyingRetainersMod>().WriteSettings();
        }
    }
}
