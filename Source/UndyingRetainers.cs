using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace UndyingRetainers
{
    public class URSettings : ModSettings
    {
        public bool noFood = true;
        public bool noRest = true;
        public bool noJoy = true;
        public bool noMood = true;
        public bool undying = true;
        public float reformDays = 1.5f;

        // v1.1
        public bool householdBond = true;
        public bool masterRescue = true;
        public bool masterDraftFollow = true;

        // v1.2: colonist bar display of retainers. 0 = show all, 1 = only when combat related (default), 2 = hide all.
        public int retainerBarMode = 1;
        public List<RaisePreset> presets = new List<RaisePreset>();

        // Raise profile (P3 skills / work limits), used by the corpse button and the summon window.
        public int skillMode;
        public int allSkillLevel = 8;
        public Dictionary<string, int> perSkill = new Dictionary<string, int>();
        public List<string> disallowedWork = new List<string>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref skillMode, "skillMode", 0);
            Scribe_Values.Look(ref allSkillLevel, "allSkillLevel", 8);
            Scribe_Collections.Look(ref perSkill, "perSkill", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref disallowedWork, "disallowedWork", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (perSkill == null)
                {
                    perSkill = new Dictionary<string, int>();
                }
                if (disallowedWork == null)
                {
                    disallowedWork = new List<string>();
                }
            }
            Scribe_Values.Look(ref noFood, "noFood", true);
            Scribe_Values.Look(ref noRest, "noRest", true);
            Scribe_Values.Look(ref noJoy, "noJoy", true);
            Scribe_Values.Look(ref noMood, "noMood", true);
            Scribe_Values.Look(ref undying, "undying", true);
            Scribe_Values.Look(ref reformDays, "reformDays", 1.5f);
            Scribe_Values.Look(ref householdBond, "householdBond", true);
            Scribe_Values.Look(ref masterRescue, "masterRescue", true);
            Scribe_Values.Look(ref masterDraftFollow, "masterDraftFollow", true);
            Scribe_Values.Look(ref retainerBarMode, "retainerBarMode", 1);
            Scribe_Collections.Look(ref presets, "presets", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && presets == null)
            {
                presets = new List<RaisePreset>();
            }
        }
    }

    public class UndyingRetainersMod : Mod
    {
        public static URSettings Settings;

        public UndyingRetainersMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<URSettings>();
            new Harmony("david.undyingretainers").PatchAll(Assembly.GetExecutingAssembly());
            Log.Message("[UndyingRetainers] loaded");
        }

        public override string SettingsCategory() => "Undying Retainers 不朽家臣";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var l = new Listing_Standard();
            l.Begin(inRect);
            l.Label("UR_Settings_NeedsHeader".Translate());
            l.CheckboxLabeled("UR_Settings_NoFood".Translate(), ref Settings.noFood);
            l.CheckboxLabeled("UR_Settings_NoRest".Translate(), ref Settings.noRest);
            l.CheckboxLabeled("UR_Settings_NoJoy".Translate(), ref Settings.noJoy);
            l.CheckboxLabeled("UR_Settings_NoMood".Translate(), ref Settings.noMood);
            l.GapLine();
            l.CheckboxLabeled("UR_Settings_Undying".Translate(), ref Settings.undying);
            l.Label("UR_Settings_ReformDays".Translate(Settings.reformDays.ToString("0.0")));
            Settings.reformDays = Mathf.Round(l.Slider(Settings.reformDays, 0.5f, 10f) * 2f) / 2f;
            l.GapLine();
            l.CheckboxLabeled("UR_Settings_Bond".Translate(), ref Settings.householdBond, "UR_Settings_BondDesc".Translate());
            l.CheckboxLabeled("UR_Settings_MasterRescue".Translate(), ref Settings.masterRescue, "UR_Settings_MasterRescueDesc".Translate());
            l.CheckboxLabeled("UR_Settings_MasterDraftFollow".Translate(), ref Settings.masterDraftFollow, "UR_Settings_MasterDraftFollowDesc".Translate());
            l.GapLine();
            l.Label("UR_Settings_BarHeader".Translate());
            if (l.RadioButton("UR_Settings_BarAll".Translate(), Settings.retainerBarMode == 0, 12f, "UR_Settings_BarAllDesc".Translate()))
            {
                Settings.retainerBarMode = 0;
            }
            if (l.RadioButton("UR_Settings_BarCombat".Translate(), Settings.retainerBarMode == 1, 12f, "UR_Settings_BarCombatDesc".Translate()))
            {
                Settings.retainerBarMode = 1;
            }
            if (l.RadioButton("UR_Settings_BarNone".Translate(), Settings.retainerBarMode == 2, 12f, "UR_Settings_BarNoneDesc".Translate()))
            {
                Settings.retainerBarMode = 2;
            }
            l.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            if (Current.ProgramState == ProgramState.Playing)
            {
                RetainerUtility.ApplyNeedsToAll();
                BondUtility.Refresh();
                RetainerBar.Refresh(true);
            }
        }
    }

    [DefOf]
    public static class URDefOf
    {
        public static HediffDef UR_RetainerContract;
        public static HediffDef UR_NoFood;
        public static HediffDef UR_NoRest;
        public static HediffDef UR_NoJoy;
        public static HediffDef UR_NoMood;
        public static HediffDef UR_Undying;
        public static HediffDef UR_Reforming;

        static URDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(URDefOf));
        }
    }

    [StaticConstructorOnStartup]
    public static class RetainerUtility
    {
        private static Texture2D raiseIcon;

        public static Texture2D RaiseIcon
        {
            get
            {
                if (raiseIcon == null)
                {
                    raiseIcon = DefDatabase<ThingDef>.GetNamedSilentFail("MechSerumResurrector")?.uiIcon ?? BaseContent.BadTex;
                }
                return raiseIcon;
            }
        }

        public static bool IsRetainer(this Pawn p)
        {
            return p?.health?.hediffSet != null && p.health.hediffSet.HasHediff(URDefOf.UR_RetainerContract);
        }

        public static bool CanRaise(Corpse corpse)
        {
            Pawn p = corpse?.InnerPawn;
            return p != null && !p.Discarded && p.RaceProps.Humanlike;
        }

        public static bool Raise(Corpse corpse)
        {
            Pawn pawn = corpse.InnerPawn;
            var parms = new ResurrectionParams
            {
                restoreMissingParts = true,
                noLord = true,
                removeDiedThoughts = true
            };
            if (!ResurrectionUtility.TryResurrect(pawn, parms))
            {
                Messages.Message("UR_Message_RaiseFailed".Translate(pawn.LabelShort), MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            pawn.GetLord()?.RemovePawn(pawn);
            if (pawn.Faction != Faction.OfPlayer)
            {
                RecruitUtility.Recruit(pawn, Faction.OfPlayer);
            }
            pawn.workSettings?.EnableAndInitializeIfNotAlreadyInitialized();

            if (!pawn.IsRetainer())
            {
                pawn.health.AddHediff(URDefOf.UR_RetainerContract);
            }
            ApplyNeeds(pawn);
            ApplyProfile(pawn);

            Messages.Message("UR_Message_Raised".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.PositiveEvent);
            return true;
        }

        public static void ApplyProfile(Pawn p)
        {
            URSettings s = UndyingRetainersMod.Settings;
            if (p.skills != null && s.skillMode != 0)
            {
                foreach (SkillRecord r in p.skills.skills)
                {
                    if (r.TotallyDisabled)
                    {
                        continue;
                    }
                    if (s.skillMode == 1)
                    {
                        r.Level = s.allSkillLevel;
                    }
                    else if (s.perSkill.TryGetValue(r.def.defName, out int lv) && lv >= 0)
                    {
                        r.Level = lv;
                    }
                }
            }
            ApplyWorkProfile(p);
        }

        public static void ApplyWorkProfile(Pawn p)
        {
            ApplyWorkProfile(p, UndyingRetainersMod.Settings.disallowedWork);
        }

        public static void ApplyWorkProfile(Pawn p, IList<string> disallowed)
        {
            if (disallowed == null)
            {
                disallowed = new List<string>();
            }
            if (p.workSettings != null && p.workSettings.EverWork)
            {
                foreach (WorkTypeDef w in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                {
                    if (!w.visible || p.WorkTypeIsDisabled(w))
                    {
                        continue;
                    }
                    if (disallowed.Contains(w.defName))
                    {
                        p.workSettings.SetPriority(w, 0);
                    }
                    else if (p.workSettings.GetPriority(w) == 0)
                    {
                        p.workSettings.SetPriority(w, 3);
                    }
                }
            }
        }

        public static void ApplyNeeds(Pawn p)
        {
            URSettings s = UndyingRetainersMod.Settings;
            SetHediff(p, URDefOf.UR_NoFood, s.noFood);
            SetHediff(p, URDefOf.UR_NoRest, s.noRest);
            SetHediff(p, URDefOf.UR_NoJoy, s.noJoy);
            SetHediff(p, URDefOf.UR_NoMood, s.noMood);
            SetHediff(p, URDefOf.UR_Undying, s.undying);
            p.needs?.AddOrRemoveNeedsAsAppropriate();
        }

        private static void SetHediff(Pawn p, HediffDef def, bool present)
        {
            Hediff h = p.health.hediffSet.GetFirstHediffOfDef(def);
            if (present && h == null)
            {
                p.health.AddHediff(def);
            }
            else if (!present && h != null)
            {
                p.health.RemoveHediff(h);
            }
        }

        public static bool WouldBeDead(Pawn p)
        {
            Pawn_HealthTracker h = p.health;
            List<Hediff> list = h.hediffSet.hediffs;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].CauseDeathNow())
                {
                    return true;
                }
            }
            return h.ShouldBeDeadFromRequiredCapacity() != null || h.ShouldBeDeadFromLethalDamageThreshold();
        }

        public static bool IsReforming(Pawn p)
        {
            return p.health.hediffSet.HasHediff(URDefOf.UR_Reforming);
        }

        public static void StartReform(Pawn p)
        {
            if (IsReforming(p))
            {
                return;
            }
            int ticks = Mathf.Max(1, Mathf.RoundToInt(UndyingRetainersMod.Settings.reformDays * GenDate.TicksPerDay));
            var h = (Hediff_Reforming)HediffMaker.MakeHediff(URDefOf.UR_Reforming, p);
            h.endTick = Find.TickManager.TicksGame + ticks;
            p.health.AddHediff(h);
            Messages.Message("UR_Message_Reforming".Translate(p.LabelShort, ticks.ToStringTicksToPeriod()), p, MessageTypeDefOf.NeutralEvent);
        }

        public static void FinishReform(Pawn p)
        {
            HediffSet set = p.health.hediffSet;
            List<Hediff> remove = set.hediffs.Where(h =>
                h is Hediff_Reforming
                || (h is Hediff_Injury inj && !inj.IsPermanent())
                || (h is Hediff_MissingPart && h.Part != null && (h.Part.parent == null || !set.PartOrAnyAncestorHasDirectlyAddedParts(h.Part.parent)))
                || h.def == HediffDefOf.BloodLoss
                || (h.def.everCurableByItem && (h.IsLethal || h.IsAnyStageLifeThreatening()))).ToList();
            foreach (Hediff h in remove)
            {
                if (set.hediffs.Contains(h))
                {
                    p.health.RemoveHediff(h);
                }
            }
            Messages.Message("UR_Message_Reformed".Translate(p.LabelShort), p, MessageTypeDefOf.PositiveEvent);
        }

        public static void ApplyNeedsToAll()
        {
            foreach (Pawn p in PawnsFinder.AllMapsWorldAndTemporary_Alive.ToList())
            {
                if (p.IsRetainer())
                {
                    ApplyNeeds(p);
                }
            }
        }
    }

    public class URGameComponent : GameComponent
    {
        public List<Household> households = new List<Household>();
        public int nextHouseholdId;

        public URGameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref households, "households", LookMode.Deep);
            Scribe_Values.Look(ref nextHouseholdId, "nextHouseholdId", 0);
            if (households == null)
            {
                households = new List<Household>();
            }
        }

        public override void LoadedGame()
        {
            RetainerUtility.ApplyNeedsToAll();
            MasterBehavior.Reset();
            BondUtility.Refresh();
            RetainerBar.Refresh(true);
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            MasterBehavior.Tick(now);
            if (now % 30 == 0)
            {
                RetainerBar.Refresh(false);
            }
            if (now % 250 != 0)
            {
                return;
            }
            BondUtility.Refresh();
            foreach (Pawn p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction.ToList())
            {
                if (p.Dead || !p.IsRetainer())
                {
                    continue;
                }
                var reform = p.health.hediffSet.GetFirstHediffOfDef(URDefOf.UR_Reforming) as Hediff_Reforming;
                if (reform != null)
                {
                    if (now >= reform.endTick)
                    {
                        RetainerUtility.FinishReform(p);
                    }
                }
                else if (UndyingRetainersMod.Settings.undying && RetainerUtility.WouldBeDead(p))
                {
                    RetainerUtility.StartReform(p);
                }
            }
        }
    }

    public class Hediff_Reforming : HediffWithComps
    {
        public int endTick;

        public override string LabelInBrackets
        {
            get
            {
                int left = endTick - Find.TickManager.TicksGame;
                return left > 0 ? left.ToStringTicksToPeriod() : null;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref endTick, "endTick", 0);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_Pawn_Kill
    {
        public static bool Prefix(Pawn __instance)
        {
            if (UndyingRetainersMod.Settings == null || !UndyingRetainersMod.Settings.undying)
            {
                return true;
            }
            if (Current.ProgramState != ProgramState.Playing || __instance.Dead || __instance.Destroyed || !__instance.IsRetainer())
            {
                return true;
            }
            RetainerUtility.StartReform(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(Corpse), nameof(Corpse.GetGizmos))]
    public static class Patch_Corpse_GetGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Corpse __instance)
        {
            foreach (Gizmo g in __result)
            {
                yield return g;
            }
            if (RetainerUtility.CanRaise(__instance))
            {
                Corpse corpse = __instance;
                yield return new Command_Action
                {
                    defaultLabel = "UR_Command_Raise".Translate(),
                    defaultDesc = "UR_Command_RaiseDesc".Translate(),
                    icon = RetainerUtility.RaiseIcon,
                    action = () => RetainerUtility.Raise(corpse)
                };
            }
        }
    }
}
