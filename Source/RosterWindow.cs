using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace UndyingRetainers
{
    public class RosterPage
    {
        // household filter: -2 = all, -1 = none, >=0 id
        private static int householdFilter = -2;
        // master filter: null + allMasters = all; null + !allMasters = no master
        private static bool allMasters = true;
        private static Pawn masterFilter;
        private static readonly HashSet<Pawn> selected = new HashSet<Pawn>();

        private Vector2 scroll;

        private const float RowH = 28f;

        private static List<Pawn> AllRetainers()
        {
            return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction
                .Where(p => p.IsRetainer())
                .OrderBy(p => HouseholdUtility.Contract(p)?.Household?.name ?? "￿")
                .ThenBy(p => p.LabelShort)
                .ToList();
        }

        private static bool PassesFilter(Pawn p)
        {
            Hediff_RetainerContract k = HouseholdUtility.Contract(p);
            int hid = k?.householdId ?? -1;
            if (HouseholdUtility.Get(hid) == null)
            {
                hid = -1;
            }
            if (householdFilter != -2 && hid != householdFilter)
            {
                return false;
            }
            if (!allMasters && k?.master != masterFilter)
            {
                return false;
            }
            return true;
        }

        private static string Status(Pawn p)
        {
            if (p.health.hediffSet.GetFirstHediffOfDef(URDefOf.UR_Reforming) is Hediff_Reforming r)
            {
                int left = r.endTick - Find.TickManager.TicksGame;
                return "UR_Roster_Reforming".Translate(Mathf.Max(0, left).ToStringTicksToPeriod());
            }
            if (p.Downed)
            {
                return "UR_Roster_Downed".Translate();
            }
            if (p.Drafted)
            {
                return "UR_Roster_Drafted".Translate();
            }
            if (p.GetCaravan() != null)
            {
                return "UR_Roster_Caravan".Translate();
            }
            if (p.Spawned)
            {
                return p.Map == Find.CurrentMap ? (string)"UR_Roster_Here".Translate() : p.Map.Parent.LabelCap;
            }
            return "UR_Roster_Away".Translate();
        }

        public void Draw(Rect inRect)
        {
            List<Pawn> all = AllRetainers();
            List<Pawn> shown = all.Where(PassesFilter).ToList();
            selected.RemoveWhere(p => p == null || p.Dead || p.Destroyed || !shown.Contains(p));
            if (masterFilter != null && (masterFilter.Dead || masterFilter.Discarded))
            {
                masterFilter = null;
                allMasters = true;
            }
            if (householdFilter >= 0 && HouseholdUtility.Get(householdFilter) == null)
            {
                householdFilter = -2;
            }

            float y = inRect.y;
            int reforming = all.Count(p => p.health.hediffSet.HasHediff(URDefOf.UR_Reforming));
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 34f), "UR_Roster_Title".Translate(all.Count.ToString(), reforming.ToString()));
            Text.Font = GameFont.Small;
            y += 38f;

            // Filters
            float fw = 260f;
            string hLabel = householdFilter == -2 ? (string)"UR_Roster_All".Translate() : householdFilter == -1 ? (string)"UR_None".Translate() : HouseholdUtility.Get(householdFilter).name;
            if (UIKit.LabeledButton(new Rect(inRect.x, y, fw, 28f), "UR_Summon_Household".Translate(), hLabel))
            {
                var opts = new List<FloatMenuOption>
                {
                    new FloatMenuOption("UR_Roster_All".Translate(), () => householdFilter = -2),
                    new FloatMenuOption("UR_None".Translate(), () => householdFilter = -1)
                };
                if (HouseholdUtility.Comp != null)
                {
                    foreach (Household h in HouseholdUtility.Comp.households)
                    {
                        int id = h.id;
                        opts.Add(new FloatMenuOption(h.name, () => householdFilter = id));
                    }
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            string mLabel = allMasters ? (string)"UR_Roster_All".Translate() : masterFilter == null ? (string)"UR_None".Translate() : masterFilter.LabelShort;
            if (UIKit.LabeledButton(new Rect(inRect.x + fw + 12f, y, fw, 28f), "UR_Summon_Master".Translate(), mLabel))
            {
                var opts = new List<FloatMenuOption> { new FloatMenuOption("UR_Roster_All".Translate(), () => { allMasters = true; masterFilter = null; }) };
                opts.AddRange(HouseholdUtility.MasterOptions(null, m => { allMasters = false; masterFilter = m; }));
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            GUI.color = UIKit.DimText;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(inRect.x + 2 * fw + 24f, y, inRect.xMax - (inRect.x + 2 * fw + 24f), 28f), "UR_Roster_Shown".Translate(shown.Count.ToString(), selected.Count.ToString()));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            y += 36f;

            // Batch actions
            float bx = inRect.x;
            bool any = selected.Count > 0;
            if (ActionButton(ref bx, y, 90f, "UR_Summon_All".Translate(), true))
            {
                foreach (Pawn p in shown)
                {
                    selected.Add(p);
                }
            }
            if (ActionButton(ref bx, y, 90f, "UR_Summon_None".Translate(), true))
            {
                selected.Clear();
            }
            bx += 12f;
            if (ActionButton(ref bx, y, 130f, "UR_Roster_SetMaster".Translate(), any))
            {
                Find.WindowStack.Add(new FloatMenu(HouseholdUtility.MasterOptions(null, m =>
                {
                    foreach (Pawn p in selected)
                    {
                        Hediff_RetainerContract k = HouseholdUtility.Contract(p);
                        if (k != null)
                        {
                            k.master = m == p ? null : m;
                        }
                    }
                })));
            }
            if (ActionButton(ref bx, y, 130f, "UR_Roster_SetHousehold".Translate(), any))
            {
                Find.WindowStack.Add(new FloatMenu(HouseholdUtility.HouseholdOptions(null, id =>
                {
                    foreach (Pawn p in selected)
                    {
                        Hediff_RetainerContract k = HouseholdUtility.Contract(p);
                        if (k != null)
                        {
                            k.householdId = id;
                        }
                    }
                })));
            }
            if (ActionButton(ref bx, y, 150f, "UR_Roster_ApplyWork".Translate(), any))
            {
                foreach (Pawn p in selected)
                {
                    RetainerUtility.ApplyWorkProfile(p);
                }
                Messages.Message("UR_Roster_WorkApplied".Translate(selected.Count.ToString()), MessageTypeDefOf.NeutralEvent, historical: false);
            }
            TooltipHandler.TipRegion(new Rect(bx - 156f, y, 150f, 28f), "UR_Roster_ApplyWorkDesc".Translate());
            if (ActionButton(ref bx, y, 160f, "UR_Roster_ApplyPresetWork".Translate(), any))
            {
                if (PresetUtility.All.Count == 0)
                {
                    Messages.Message("UR_Preset_NoneSaved".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                }
                else
                {
                    Find.WindowStack.Add(new FloatMenu(PresetUtility.Options(preset =>
                    {
                        int count = 0;
                        foreach (Pawn p in selected)
                        {
                            if (p == null || p.Dead || p.Destroyed)
                            {
                                continue;
                            }
                            RetainerUtility.ApplyWorkProfile(p, preset.disallowedWork);
                            count++;
                        }
                        Messages.Message("UR_Roster_WorkApplied".Translate(count.ToString()), MessageTypeDefOf.NeutralEvent, historical: false);
                    })));
                }
            }
            TooltipHandler.TipRegion(new Rect(bx - 166f, y, 160f, 28f), "UR_Roster_ApplyPresetWorkDesc".Translate());
            if (ActionButton(ref bx, y, 90f, "UR_Roster_Draft".Translate(), any))
            {
                SetDrafted(true);
            }
            if (ActionButton(ref bx, y, 110f, "UR_Roster_Undraft".Translate(), any))
            {
                SetDrafted(false);
            }
            y += 38f;

            // Header
            float[] cols = { 30f, 200f, 150f, 140f, 60f, 160f };
            Rect header = new Rect(inRect.x, y, inRect.width, 24f);
            GUI.color = UIKit.SectionColor;
            float cx = header.x + cols[0];
            string[] heads = { "UR_Roster_Col_Name", "UR_Summon_Household", "UR_Summon_Master", "UR_Roster_Col_Bond", "UR_Roster_Col_Status", "UR_Roster_Col_Job" };
            for (int i = 0; i < heads.Length; i++)
            {
                float cw = i + 1 < cols.Length ? cols[i + 1] : header.xMax - cx;
                Widgets.Label(new Rect(cx, y, cw, 24f), heads[i].Translate());
                cx += cw;
            }
            Widgets.DrawLineHorizontal(inRect.x, y + 24f, inRect.width, new Color(0.75f, 0.85f, 1f, 0.35f));
            GUI.color = Color.white;
            y += 28f;

            // Rows
            Rect outRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, shown.Count * RowH);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            for (int i = 0; i < shown.Count; i++)
            {
                Pawn p = shown[i];
                Rect row = new Rect(0f, i * RowH, view.width, RowH);
                if (i % 2 == 1)
                {
                    Widgets.DrawLightHighlight(row);
                }
                if (selected.Contains(p))
                {
                    Widgets.DrawHighlightSelected(row);
                }
                Widgets.DrawHighlightIfMouseover(row);

                bool on = selected.Contains(p);
                bool was = on;
                Widgets.Checkbox(new Vector2(row.x + 3f, row.y + 2f), ref on, paintable: true);
                if (on != was)
                {
                    if (on)
                    {
                        selected.Add(p);
                    }
                    else
                    {
                        selected.Remove(p);
                    }
                }

                Hediff_RetainerContract k = HouseholdUtility.Contract(p);
                float x = row.x + cols[0];
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect nameRect = new Rect(x, row.y, cols[1] - 6f, RowH);
                Widgets.Label(nameRect, p.LabelShortCap.Truncate(nameRect.width));
                TooltipHandler.TipRegion(nameRect, "UR_Roster_JumpTip".Translate());
                if (Widgets.ButtonInvisible(nameRect))
                {
                    CameraJumper.TryJumpAndSelect(p);
                }
                x += cols[1];
                string hn = k?.Household?.name ?? "-";
                Widgets.Label(new Rect(x, row.y, cols[2] - 6f, RowH), hn.Truncate(cols[2] - 6f));
                x += cols[2];
                string mn = k?.master?.LabelShort ?? "-";
                Widgets.Label(new Rect(x, row.y, cols[3] - 6f, RowH), mn.Truncate(cols[3] - 6f));
                x += cols[3];
                Widgets.Label(new Rect(x, row.y, cols[4] - 6f, RowH), BondUtility.LevelLabel(p));
                x += cols[4];
                Widgets.Label(new Rect(x, row.y, cols[5] - 6f, RowH), Status(p).Truncate(cols[5] - 6f));
                x += cols[5];
                string job = p.Spawned && p.jobs?.curDriver != null ? p.jobs.curDriver.GetReport() : "-";
                GUI.color = UIKit.DimText;
                Widgets.Label(new Rect(x, row.y, row.xMax - x, RowH), job.CapitalizeFirst().Truncate(row.xMax - x));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            Widgets.EndScrollView();
        }

        private static bool ActionButton(ref float x, float y, float width, string label, bool active)
        {
            Rect r = new Rect(x, y, width, 30f);
            x += width + 6f;
            return Widgets.ButtonText(r, label, active: active) && active;
        }

        private static void SetDrafted(bool drafted)
        {
            foreach (Pawn p in selected)
            {
                if (p.Spawned && p.drafter != null && !p.Downed)
                {
                    p.drafter.Drafted = drafted;
                }
            }
        }
    }
}
