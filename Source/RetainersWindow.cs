using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace UndyingRetainers
{
    public class MainTabWindow_Retainers : MainTabWindow
    {
        private static int tab;
        private readonly RosterPage roster = new RosterPage();
        private readonly SummonPage summon = new SummonPage();

        public override Vector2 RequestedTabSize => new Vector2(1060f, 660f);

        public override void DoWindowContents(Rect inRect)
        {
            Rect body = inRect;
            body.yMin += 34f;
            var tabs = new List<TabRecord>
            {
                new TabRecord("UR_Tab_Roster".Translate(), () => tab = 0, tab == 0),
                new TabRecord("UR_Tab_Summon".Translate(), () => tab = 1, tab == 1)
            };
            Widgets.DrawMenuSection(body);
            TabDrawer.DrawTabs(body, tabs);
            Rect content = body.ContractedBy(12f);
            if (tab == 0)
            {
                roster.Draw(content);
            }
            else
            {
                summon.Draw(content);
            }
        }

        public override void PostClose()
        {
            base.PostClose();
            summon.OnClose();
        }
    }
}
