using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace UndyingRetainers
{
    public class Household : IExposable, IRenameable
    {
        public int id;
        public string name;

        public string RenamableLabel
        {
            get => name;
            set => name = value;
        }

        public string BaseLabel => name;

        public string InspectLabel => name;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", 0);
            Scribe_Values.Look(ref name, "name");
        }
    }

    public class Dialog_RenameHousehold : Dialog_Rename<Household>
    {
        public Dialog_RenameHousehold(Household h) : base(h)
        {
        }
    }

    public class Hediff_RetainerContract : HediffWithComps
    {
        public Pawn master;
        public int householdId = -1;

        public Household Household => HouseholdUtility.Get(householdId);

        public override string LabelInBrackets
        {
            get
            {
                var parts = new List<string>();
                if (master != null)
                {
                    parts.Add("UR_Contract_Master".Translate(master.LabelShort));
                }
                Household h = Household;
                if (h != null)
                {
                    parts.Add(h.name);
                }
                return parts.Count == 0 ? null : string.Join("・", parts);
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
            {
                yield return g;
            }
            if (pawn.Faction != Faction.OfPlayer)
            {
                yield break;
            }
            string masterLabel = master == null ? (string)"UR_None".Translate() : master.LabelShort;
            yield return new Command_Action
            {
                defaultLabel = "UR_Gizmo_Master".Translate(masterLabel),
                defaultDesc = "UR_Gizmo_MasterDesc".Translate(),
                icon = RetainerUtility.RaiseIcon,
                action = () => Find.WindowStack.Add(new FloatMenu(HouseholdUtility.MasterOptions(pawn, m => master = m)))
            };
            Household h = Household;
            string hLabel = h == null ? (string)"UR_None".Translate() : h.name;
            yield return new Command_Action
            {
                defaultLabel = "UR_Gizmo_Household".Translate(hLabel),
                defaultDesc = "UR_Gizmo_HouseholdDesc".Translate(),
                icon = RetainerUtility.RaiseIcon,
                action = () => Find.WindowStack.Add(new FloatMenu(HouseholdUtility.HouseholdOptions(Household, id => householdId = id)))
            };
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving && master != null && master.Discarded)
            {
                master = null;
            }
            Scribe_References.Look(ref master, "master");
            Scribe_Values.Look(ref householdId, "householdId", -1);
        }
    }

    public static class HouseholdUtility
    {
        public static URGameComponent Comp => Current.Game?.GetComponent<URGameComponent>();

        public static Household Get(int id)
        {
            if (id < 0 || Comp == null)
            {
                return null;
            }
            return Comp.households.FirstOrDefault(h => h.id == id);
        }

        public static Household Create()
        {
            URGameComponent c = Comp;
            var h = new Household { id = c.nextHouseholdId++ };
            h.name = "UR_Household_Default".Translate((h.id + 1).ToString());
            c.households.Add(h);
            return h;
        }

        public static Hediff_RetainerContract Contract(Pawn p)
        {
            return p?.health?.hediffSet?.GetFirstHediffOfDef(URDefOf.UR_RetainerContract) as Hediff_RetainerContract;
        }

        public static List<FloatMenuOption> MasterOptions(Pawn exclude, System.Action<Pawn> set)
        {
            var opts = new List<FloatMenuOption> { new FloatMenuOption("UR_None".Translate(), () => set(null)) };
            foreach (Pawn c in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
            {
                if (c == exclude || c.IsRetainer())
                {
                    continue;
                }
                Pawn local = c;
                opts.Add(new FloatMenuOption(c.LabelShortCap, () => set(local)));
            }
            return opts;
        }

        public static List<FloatMenuOption> HouseholdOptions(Household current, System.Action<int> set)
        {
            var opts = new List<FloatMenuOption> { new FloatMenuOption("UR_None".Translate(), () => set(-1)) };
            if (Comp != null)
            {
                foreach (Household h in Comp.households)
                {
                    int id = h.id;
                    opts.Add(new FloatMenuOption(h.name, () => set(id)));
                }
            }
            opts.Add(new FloatMenuOption("UR_Household_New".Translate(), () =>
            {
                Household h = Create();
                set(h.id);
                Find.WindowStack.Add(new Dialog_RenameHousehold(h));
            }));
            if (current != null)
            {
                opts.Add(new FloatMenuOption("UR_Household_Rename".Translate(current.name), () => Find.WindowStack.Add(new Dialog_RenameHousehold(current))));
            }
            return opts;
        }
    }
}
