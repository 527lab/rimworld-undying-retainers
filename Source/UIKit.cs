using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;

namespace UndyingRetainers
{
    [StaticConstructorOnStartup]
    public static class UIKit
    {
        public static readonly Color AccentColor = new Color(0.27f, 0.40f, 0.55f);
        public static readonly Color DimText = new Color(0.62f, 0.62f, 0.62f);
        public static readonly Color SectionColor = new Color(0.80f, 0.86f, 0.95f);
        private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color PanelColor = new Color(0.10f, 0.10f, 0.11f, 0.6f);
        private static readonly Color BorderColor = new Color(1f, 1f, 1f, 0.15f);
        private static readonly Color OnFill = new Color(0.20f, 0.33f, 0.24f);
        private static readonly Color OnBorder = new Color(0.45f, 0.70f, 0.48f, 0.8f);
        private static readonly Color OffFill = new Color(0.13f, 0.13f, 0.14f);
        private static readonly Color BarColor = new Color(0.32f, 0.47f, 0.62f);

        private static bool painting;
        private static bool paintValue;

        public static float Section(float y, float width, string label)
        {
            GUI.color = SectionColor;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, y, width, 24f), label);
            GUI.color = LineColor;
            Widgets.DrawLineHorizontal(0f, y + 24f, width);
            GUI.color = Color.white;
            return y + 30f;
        }

        public static bool LabeledButton(Rect rect, string label, string value)
        {
            float lw = Mathf.Min(80f, rect.width * 0.35f);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = DimText;
            Widgets.Label(new Rect(rect.x, rect.y, lw, rect.height), label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            return Widgets.ButtonText(new Rect(rect.x + lw, rect.y, rect.width - lw, rect.height), value);
        }

        public static void Box(Rect r, Color fill, Color border)
        {
            Widgets.DrawBoxSolid(r, fill);
            GUI.color = border;
            Widgets.DrawBox(r);
            GUI.color = Color.white;
        }

        /// <summary>Segmented control; returns the selected index.</summary>
        public static int Segmented(Rect rect, string[] labels, int selected)
        {
            Box(rect, PanelColor, BorderColor);
            float w = rect.width / labels.Length;
            Text.Anchor = TextAnchor.MiddleCenter;
            for (int i = 0; i < labels.Length; i++)
            {
                Rect seg = new Rect(rect.x + i * w, rect.y, w, rect.height).ContractedBy(2f);
                if (i == selected)
                {
                    Widgets.DrawBoxSolid(seg, AccentColor);
                    GUI.color = Color.white;
                }
                else
                {
                    Widgets.DrawHighlightIfMouseover(seg);
                    GUI.color = DimText;
                }
                Widgets.Label(seg, labels[i]);
                GUI.color = Color.white;
                if (i != selected && Widgets.ButtonInvisible(seg))
                {
                    selected = i;
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                }
            }
            Text.Anchor = TextAnchor.UpperLeft;
            return selected;
        }

        /// <summary>Clickable / draggable 0-20 bar. Returns new value (-1 = keep, if allowed).</summary>
        public static int SkillBar(Rect r, int v, bool allowKeep)
        {
            Box(r, OffFill, BorderColor);
            Rect inner = r.ContractedBy(2f);
            if (v > 0)
            {
                Widgets.DrawBoxSolid(new Rect(inner.x, inner.y, inner.width * v / 20f, inner.height), BarColor);
            }
            GUI.color = new Color(1f, 1f, 1f, 0.08f);
            for (int t = 5; t < 20; t += 5)
            {
                Widgets.DrawLineVertical(inner.x + inner.width * t / 20f, inner.y, inner.height);
            }
            GUI.color = v < 0 ? DimText : Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(r, v < 0 ? (string)"UR_Profile_Keep".Translate() : v.ToString());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Widgets.DrawHighlightIfMouseover(r);

            Event e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && r.Contains(e.mousePosition))
            {
                v = Mathf.Clamp(Mathf.RoundToInt((e.mousePosition.x - inner.x) / inner.width * 20f), 0, 20);
                e.Use();
            }
            return v;
        }

        /// <summary>On/off tile. Press and drag across tiles to set them all to the same state.</summary>
        public static bool ToggleTile(Rect r, string label, bool on)
        {
            Event e = Event.current;
            if (e.rawType == EventType.MouseUp)
            {
                painting = false;
            }
            bool over = Mouse.IsOver(r);
            if (over && e.type == EventType.MouseDown && e.button == 0)
            {
                painting = true;
                paintValue = !on;
                on = paintValue;
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                e.Use();
            }
            else if (over && painting && e.type == EventType.MouseDrag && on != paintValue)
            {
                on = paintValue;
            }

            Box(r, on ? OnFill : OffFill, on ? OnBorder : BorderColor);
            if (over)
            {
                Widgets.DrawHighlight(r);
            }
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = on ? Color.white : DimText;
            Widgets.Label(r, label.Truncate(r.width - 6f));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            return on;
        }
    }
}
