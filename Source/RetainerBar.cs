using System.Collections.Generic;
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
        // True only while ColonistBar.CheckRecacheEntries is running; the Sort postfix filters only then.
        public static bool InRecache;

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

        /// <summary>Remove hidden retainers from a pawn list (in place).</summary>
        public static void Filter(List<Pawn> pawns)
        {
            if (pawns == null)
            {
                return;
            }
            pawns.RemoveAll(p => !ShouldShow(p));
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

        // If a hidden retainer still shows up in the bar, our patch target is wrong: say so once in the log.
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
                        + "The patched method may differ in this RimWorld version; please report this log line.");
                    return;
                }
            }
        }
    }

    // Patch 1: mark the span in which the colonist bar rebuilds its entries.
    [HarmonyPatch]
    public static class Patch_ColonistBar_CheckRecacheEntries
    {
        public static bool Prepare()
        {
            bool ok = TargetMethod() != null;
            if (!ok)
            {
                Log.Warning("[UndyingRetainers] ColonistBar.CheckRecacheEntries not found; retainer colonist bar filter disabled.");
            }
            return ok;
        }

        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ColonistBar), "CheckRecacheEntries");
        }

        public static void Prefix()
        {
            RetainerBar.InRecache = true;
        }

        public static void Finalizer()
        {
            RetainerBar.InRecache = false;
        }
    }

    // Patch 2: the bar sorts its pawn list before laying out; drop hidden retainers there so layout stays consistent.
    [HarmonyPatch]
    public static class Patch_PlayerPawnsDisplayOrder_Sort
    {
        public static bool Prepare()
        {
            bool ok = TargetMethod() != null && Patch_ColonistBar_CheckRecacheEntries.TargetMethod() != null;
            if (!ok)
            {
                Log.Warning("[UndyingRetainers] PlayerPawnsDisplayOrderUtility.Sort not found; retainer colonist bar filter disabled.");
            }
            return ok;
        }

        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(PlayerPawnsDisplayOrderUtility), "Sort", new[] { typeof(List<Pawn>) });
        }

        public static void Postfix(List<Pawn> __0)
        {
            if (RetainerBar.InRecache)
            {
                RetainerBar.Filter(__0);
            }
        }
    }
}
