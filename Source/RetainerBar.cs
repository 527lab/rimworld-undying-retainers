using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace UndyingRetainers
{
    /// <summary>
    /// v1.2: decides which retainers appear in the colonist bar (top portrait row).
    /// Mode 0 = show all, 1 = only when combat related (default), 2 = hide all.
    /// </summary>
    public static class RetainerBar
    {
        private static int lastSignature;
        private static bool warned;

        public static bool ShouldShow(Pawn p)
        {
            if (p == null || p.Dead || UndyingRetainersMod.Settings == null)
            {
                return true;
            }
            int mode = UndyingRetainersMod.Settings.retainerBarMode;
            if (mode == 0 || !p.IsRetainer())
            {
                return true;
            }
            if (mode == 2)
            {
                return false;
            }
            return IsCombatRelated(p);
        }

        private static bool IsCombatRelated(Pawn p)
        {
            if (p.Drafted || p.Downed || p.InMentalState)
            {
                return true;
            }
            if (p.health?.hediffSet != null && p.health.hediffSet.HasHediff(URDefOf.UR_Reforming))
            {
                return true;
            }
            return p.health != null && p.health.HasHediffsNeedingTend(false);
        }

        /// <summary>
        /// Replaces the PlayerPawnsDisplayOrderUtility.Sort calls inside ColonistBar.CheckRecacheEntries:
        /// sort as vanilla does, then drop hidden retainers before the bar builds its entries.
        /// </summary>
        public static void SortAndFilter(List<Pawn> pawns)
        {
            PlayerPawnsDisplayOrderUtility.Sort(pawns);
            if (pawns != null)
            {
                pawns.RemoveAll(p => !ShouldShow(p));
            }
        }

        /// <summary>
        /// Re-check which retainers are hidden; if that set changed (or force), tell the colonist bar to rebuild.
        /// Cheap enough to call every ~30 ticks.
        /// </summary>
        public static void Refresh(bool force)
        {
            if (Find.ColonistBar == null)
            {
                return;
            }
            int sig = 17;
            foreach (Pawn p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction)
            {
                if (p.IsRetainer() && !ShouldShow(p))
                {
                    sig = sig * 31 + p.thingIDNumber;
                }
            }
            if (!force && sig == lastSignature)
            {
                return;
            }
            lastSignature = sig;
            Find.ColonistBar.MarkColonistsDirty();
            Verify();
        }

        // If a hidden retainer still shows up in the bar, the patch did not take effect: say so once in the log.
        private static void Verify()
        {
            if (warned)
            {
                return;
            }
            foreach (ColonistBar.Entry e in Find.ColonistBar.Entries)
            {
                if (e.pawn != null && !ShouldShow(e.pawn))
                {
                    warned = true;
                    Log.Warning("[UndyingRetainers] Colonist bar filter is not effective: a hidden retainer is still listed. "
                        + "Another mod may rebuild the colonist bar; please report this log line.");
                    return;
                }
            }
        }
    }

    // Swap the Sort calls in ColonistBar.CheckRecacheEntries for RetainerBar.SortAndFilter.
    // A transpiler is used because Sort is a one-line method the JIT may inline, which would skip a postfix.
    [HarmonyPatch]
    public static class Patch_ColonistBar_CheckRecacheEntries
    {
        private static readonly MethodInfo SortMethod =
            AccessTools.Method(typeof(PlayerPawnsDisplayOrderUtility), nameof(PlayerPawnsDisplayOrderUtility.Sort), new[] { typeof(List<Pawn>) });

        private static readonly MethodInfo Replacement =
            AccessTools.Method(typeof(RetainerBar), nameof(RetainerBar.SortAndFilter));

        public static bool Prepare()
        {
            bool ok = TargetMethod() != null && SortMethod != null && Replacement != null;
            if (!ok)
            {
                Log.Warning("[UndyingRetainers] ColonistBar.CheckRecacheEntries not found; retainer colonist bar filter disabled.");
            }
            return ok;
        }

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ColonistBar), "CheckRecacheEntries");
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            int replaced = 0;
            foreach (CodeInstruction ci in list)
            {
                if (ci.Calls(SortMethod))
                {
                    ci.operand = Replacement;
                    replaced++;
                }
            }
            if (replaced == 0)
            {
                Log.Warning("[UndyingRetainers] No Sort call found in ColonistBar.CheckRecacheEntries; retainer colonist bar filter disabled.");
            }
            else
            {
                Log.Message("[UndyingRetainers] Colonist bar filter active (" + replaced + " call sites).");
            }
            return list;
        }
    }
}
