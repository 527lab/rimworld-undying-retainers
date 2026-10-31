using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace UndyingRetainers
{
    public class Hediff_Bond : HediffWithComps
    {
        public int members;

        public override string LabelInBrackets
        {
            get { return "UR_Bond_Members".Translate(members); }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref members, "members", 0);
        }
    }

    [DefOf]
    public static class BondDefOf
    {
        public static HediffDef UR_HouseholdBond;

        static BondDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BondDefOf));
        }
    }

    public static class BondUtility
    {
        private struct GroupKey
        {
            public int householdId;
            public object place;

            public override bool Equals(object obj)
            {
                if (!(obj is GroupKey))
                {
                    return false;
                }
                var o = (GroupKey)obj;
                return o.householdId == householdId && ReferenceEquals(o.place, place);
            }

            public override int GetHashCode()
            {
                return householdId * 397 ^ (place == null ? 0 : place.GetHashCode());
            }
        }

        private static object PlaceOf(Pawn p)
        {
            Map map = p.MapHeld;
            if (map != null)
            {
                return map;
            }
            return p.GetCaravan();
        }

        private static bool IsPresent(Pawn p)
        {
            return !p.Dead && !p.Downed && !RetainerUtility.IsReforming(p);
        }

        private static int ValidHouseholdId(Hediff_RetainerContract c)
        {
            if (c == null || c.householdId < 0 || HouseholdUtility.Get(c.householdId) == null)
            {
                return -1;
            }
            return c.householdId;
        }

        public static void Refresh()
        {
            if (Current.ProgramState != ProgramState.Playing || BondDefOf.UR_HouseholdBond == null)
            {
                return;
            }
            var source = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction;
            if (source == null)
            {
                return;
            }
            var pawns = new List<Pawn>(source);

            if (UndyingRetainersMod.Settings == null || !UndyingRetainersMod.Settings.householdBond)
            {
                for (int i = 0; i < pawns.Count; i++)
                {
                    RemoveBond(pawns[i]);
                }
                return;
            }

            var counts = new Dictionary<GroupKey, int>();
            var retainers = new List<Pawn>();
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p.health?.hediffSet == null)
                {
                    continue;
                }
                if (!p.IsRetainer())
                {
                    RemoveBond(p);
                    continue;
                }
                retainers.Add(p);
                int id = ValidHouseholdId(HouseholdUtility.Contract(p));
                if (id < 0 || !IsPresent(p))
                {
                    continue;
                }
                object place = PlaceOf(p);
                if (place == null)
                {
                    continue;
                }
                var key = new GroupKey { householdId = id, place = place };
                int c;
                counts.TryGetValue(key, out c);
                counts[key] = c + 1;
            }

            for (int i = 0; i < retainers.Count; i++)
            {
                Pawn p = retainers[i];
                Hediff_RetainerContract contract = HouseholdUtility.Contract(p);
                object place = PlaceOf(p);
                int n = 0;
                int id = ValidHouseholdId(contract);
                if (id >= 0 && place != null)
                {
                    int c;
                    if (counts.TryGetValue(new GroupKey { householdId = id, place = place }, out c))
                    {
                        n = c - (IsPresent(p) ? 1 : 0);
                    }
                }
                Pawn master = contract?.master;
                if (master != null && place != null && !master.Dead && !master.Discarded && !master.Downed
                    && master.Faction == Faction.OfPlayer && ReferenceEquals(PlaceOf(master), place))
                {
                    n++;
                }
                Apply(p, n);
            }
        }

        private static void RemoveBond(Pawn p)
        {
            Hediff h = p.health?.hediffSet?.GetFirstHediffOfDef(BondDefOf.UR_HouseholdBond);
            if (h != null)
            {
                p.health.RemoveHediff(h);
            }
        }

        private static int LevelForCount(int n)
        {
            if (n <= 0)
            {
                return 0;
            }
            if (n == 1)
            {
                return 1;
            }
            return n <= 3 ? 2 : 3;
        }

        private static void Apply(Pawn p, int n)
        {
            int level = LevelForCount(n);
            if (level == 0)
            {
                RemoveBond(p);
                return;
            }
            var bond = p.health.hediffSet.GetFirstHediffOfDef(BondDefOf.UR_HouseholdBond) as Hediff_Bond;
            if (bond == null)
            {
                p.health.AddHediff(BondDefOf.UR_HouseholdBond);
                bond = p.health.hediffSet.GetFirstHediffOfDef(BondDefOf.UR_HouseholdBond) as Hediff_Bond;
                if (bond == null)
                {
                    return;
                }
            }
            // Severity == level (1..3). Severity 0 would make the game remove the hediff (Hediff.ShouldRemove).
            float sev = level;
            if (bond.Severity != sev)
            {
                bond.Severity = sev;
            }
            bond.members = n;
        }

        public static int Level(Pawn p)
        {
            Hediff h = p?.health?.hediffSet?.GetFirstHediffOfDef(BondDefOf.UR_HouseholdBond);
            if (h == null)
            {
                return 0;
            }
            int lv = UnityEngine.Mathf.RoundToInt(h.Severity);
            return lv < 1 ? 1 : (lv > 3 ? 3 : lv);
        }

        public static string LevelLabel(Pawn p)
        {
            switch (Level(p))
            {
                case 1:
                    return "I";
                case 2:
                    return "II";
                case 3:
                    return "III";
                default:
                    return "-";
            }
        }
    }
}
