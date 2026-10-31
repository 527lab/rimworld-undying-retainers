using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace UndyingRetainers
{
    /// <summary>
    /// Master-related polling behaviors (rescue / death notice / draft follow).
    /// All state is runtime only and never saved.
    /// </summary>
    public static class MasterBehavior
    {
        private const int DraftInterval = 30;
        private const int RescueInterval = 60;
        private const int DeathInterval = 250;
        private const int RescueCooldown = 600;

        private static readonly Dictionary<Pawn, List<Pawn>> groups = new Dictionary<Pawn, List<Pawn>>();
        private static readonly List<List<Pawn>> listPool = new List<List<Pawn>>();
        private static readonly Dictionary<Pawn, int> rescueCooldown = new Dictionary<Pawn, int>();
        private static readonly Dictionary<Pawn, bool> lastDrafted = new Dictionary<Pawn, bool>();
        private static readonly List<Pawn> tmpKeys = new List<Pawn>();

        public static void Reset()
        {
            ClearGroups();
            rescueCooldown.Clear();
            lastDrafted.Clear();
            tmpKeys.Clear();
        }

        public static void Tick(int now)
        {
            bool doDraft = now % DraftInterval == 0;
            bool doRescue = now % RescueInterval == 0;
            bool doDeath = now % DeathInterval == 0;
            if (!doDraft && !doRescue && !doDeath)
            {
                return;
            }
            if (Current.Game == null || UndyingRetainersMod.Settings == null)
            {
                return;
            }

            BuildGroups();
            if (groups.Count == 0)
            {
                if (doDeath)
                {
                    rescueCooldown.Clear();
                    lastDrafted.Clear();
                }
                return;
            }

            if (doDeath)
            {
                CheckMasterDeaths();
            }
            if (doRescue && UndyingRetainersMod.Settings.masterRescue)
            {
                RescueMasters(now);
            }
            if (doDraft)
            {
                FollowDraft();
            }
            if (doDeath)
            {
                Cleanup();
            }
            ClearGroups();
        }

        // Groups living retainers by their master (masters that are null are skipped).
        private static void BuildGroups()
        {
            ClearGroups();
            List<Pawn> all = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction;
            if (all == null)
            {
                return;
            }
            for (int i = 0; i < all.Count; i++)
            {
                Pawn p = all[i];
                if (p == null || p.Dead || p.Discarded || !p.IsRetainer())
                {
                    continue;
                }
                Hediff_RetainerContract c = HouseholdUtility.Contract(p);
                if (c == null || c.master == null)
                {
                    continue;
                }
                List<Pawn> list;
                if (!groups.TryGetValue(c.master, out list))
                {
                    list = RentList();
                    groups[c.master] = list;
                }
                list.Add(p);
            }
        }

        private static List<Pawn> RentList()
        {
            if (listPool.Count > 0)
            {
                List<Pawn> l = listPool[listPool.Count - 1];
                listPool.RemoveAt(listPool.Count - 1);
                return l;
            }
            return new List<Pawn>();
        }

        private static void ClearGroups()
        {
            foreach (KeyValuePair<Pawn, List<Pawn>> kv in groups)
            {
                kv.Value.Clear();
                listPool.Add(kv.Value);
            }
            groups.Clear();
        }

        // B2
        private static void CheckMasterDeaths()
        {
            tmpKeys.Clear();
            foreach (KeyValuePair<Pawn, List<Pawn>> kv in groups)
            {
                if (kv.Key.Dead || kv.Key.Discarded)
                {
                    tmpKeys.Add(kv.Key);
                }
            }
            for (int i = 0; i < tmpKeys.Count; i++)
            {
                Pawn master = tmpKeys[i];
                List<Pawn> retainers = groups[master];
                string name = master.LabelShort;
                int count = 0;
                Pawn lookTarget = null;
                for (int j = 0; j < retainers.Count; j++)
                {
                    Hediff_RetainerContract c = HouseholdUtility.Contract(retainers[j]);
                    if (c == null)
                    {
                        continue;
                    }
                    c.master = null;
                    count++;
                    if (lookTarget == null)
                    {
                        lookTarget = retainers[j];
                    }
                }
                if (count > 0)
                {
                    Messages.Message("UR_Message_MasterDied".Translate(name, count), lookTarget, MessageTypeDefOf.NeutralEvent);
                }
                groups.Remove(master);
                retainers.Clear();
                listPool.Add(retainers);
                rescueCooldown.Remove(master);
                lastDrafted.Remove(master);
            }
            tmpKeys.Clear();
        }

        // B1
        private static void RescueMasters(int now)
        {
            foreach (KeyValuePair<Pawn, List<Pawn>> kv in groups)
            {
                Pawn master = kv.Key;
                if (master.Dead || master.Discarded || !master.Spawned || !master.Downed || master.Faction != Faction.OfPlayer || master.IsPrisoner || master.InBed())
                {
                    continue;
                }
                int last;
                if (rescueCooldown.TryGetValue(master, out last) && now - last < RescueCooldown && now >= last)
                {
                    continue;
                }

                Pawn best = null;
                float bestDist = float.MaxValue;
                List<Pawn> retainers = kv.Value;
                for (int i = 0; i < retainers.Count; i++)
                {
                    Pawn r = retainers[i];
                    if (!r.Spawned || r.Map != master.Map || r.Downed || r.Dead || RetainerUtility.IsReforming(r) || r.Drafted)
                    {
                        continue;
                    }
                    if (r.jobs == null)
                    {
                        continue;
                    }
                    Job cur = r.CurJob;
                    if (cur != null && (cur.def == JobDefOf.Rescue || cur.playerForced))
                    {
                        continue;
                    }
                    if (!HealthAIUtility.CanRescueNow(r, master, false))
                    {
                        continue;
                    }
                    float d = r.Position.DistanceToSquared(master.Position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = r;
                    }
                }
                if (best == null)
                {
                    continue;
                }

                rescueCooldown[master] = now;
                Building_Bed bed = RestUtility.FindBedFor(master, best, false, false, master.GuestStatus);
                if (bed == null)
                {
                    bed = RestUtility.FindBedFor(master, best, false, true, master.GuestStatus);
                }
                if (bed == null)
                {
                    continue;
                }
                Job job = JobMaker.MakeJob(JobDefOf.Rescue, master, bed);
                job.count = 1;
                best.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }
        }

        // B3
        private static void FollowDraft()
        {
            bool enabled = UndyingRetainersMod.Settings.masterDraftFollow;
            foreach (KeyValuePair<Pawn, List<Pawn>> kv in groups)
            {
                Pawn master = kv.Key;
                bool now = master.Drafted;
                bool before;
                if (!lastDrafted.TryGetValue(master, out before))
                {
                    lastDrafted[master] = now;
                    continue;
                }
                if (before == now)
                {
                    continue;
                }
                lastDrafted[master] = now;
                if (!enabled)
                {
                    continue;
                }
                List<Pawn> retainers = kv.Value;
                for (int i = 0; i < retainers.Count; i++)
                {
                    Pawn r = retainers[i];
                    if (!r.Spawned || r.Map != master.Map || r.Downed || r.Dead || RetainerUtility.IsReforming(r))
                    {
                        continue;
                    }
                    if (r.drafter == null || !r.drafter.ShowDraftGizmo)
                    {
                        continue;
                    }
                    r.drafter.Drafted = now;
                }
            }
        }

        // Drops dead / discarded / no-longer-master keys so nothing lingers.
        private static void Cleanup()
        {
            tmpKeys.Clear();
            foreach (Pawn p in rescueCooldown.Keys)
            {
                if (p == null || p.Discarded || p.Dead || !groups.ContainsKey(p))
                {
                    tmpKeys.Add(p);
                }
            }
            for (int i = 0; i < tmpKeys.Count; i++)
            {
                rescueCooldown.Remove(tmpKeys[i]);
            }
            tmpKeys.Clear();
            foreach (Pawn p in lastDrafted.Keys)
            {
                if (p == null || p.Discarded || p.Dead || !groups.ContainsKey(p))
                {
                    tmpKeys.Add(p);
                }
            }
            for (int i = 0; i < tmpKeys.Count; i++)
            {
                lastDrafted.Remove(tmpKeys[i]);
            }
            tmpKeys.Clear();
        }
    }
}
