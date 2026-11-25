using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ninjazon.Game
{
    // =================================================================== HUD ==
    public sealed class Hud : UIScreen
    {
        private readonly Image[] pips;
        private readonly TextMeshProUGUI distance;
        private readonly TextMeshProUGUI score;
        private readonly RectTransform multBadge;
        private readonly TextMeshProUGUI multText;
        private readonly Image comboBar;
        private readonly TextMeshProUGUI credits;
        private readonly Image dashRing;
        private readonly Image dashIcon;
        private readonly Image dashGlow;
        private readonly TextMeshProUGUI dashLabel;
        private readonly RectTransform[] powerSlots = new RectTransform[3];
        private readonly Image[] powerRings = new Image[3];
        private readonly Image[] powerIcons = new Image[3];
        private readonly TextMeshProUGUI toast;
        private readonly RectTransform hints;
        private readonly CanvasGroup hintsGroup;
        private int shownPips = -1;
        private int shownMax = -1;
        private int shownMult = -1;
        private int shownCredits = -1;
        private bool dashWasReady = true;

        public Hud(Transform parent, System.Action onPause) : base(parent, "HUD")
        {
            // --- top-left: trace shields + distance
            pips = UI.Pips(Root, 5, 50f, 10f, Palette.Cyan, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -42f));
            distance = UI.Label(Root, "0 M", 34f, Palette.Text, false, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -110f), new Vector2(400f, 50f));

            // --- top-centre: score + multiplier
            score = UI.Label(Root, "0", 76f, Palette.Text, true, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(700f, 90f));
            multBadge = UI.Panel(Root, "Mult", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(150f, 62f),
                Palette.Magenta, Palette.Magenta, true);
            multText = UI.Label(multBadge, "x1", 36f, Palette.Ink, true, TextAlignmentOptions.Center, false);
            UI.Fill(multText.rectTransform);
            comboBar = UI.Bar(Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -192f), new Vector2(240f, 18f), Palette.Magenta);

            // --- top-right: credits + pause
            credits = UI.CreditPill(Root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-150f, -36f), 210f);
            UI.IconButton(Root, "pause", Palette.Cyan, new Vector2(1f, 1f), new Vector2(-74f, -78f), 96f, onPause);

            // --- bottom-right: dash meter
            var dash = UI.Rect(Root, "Dash", new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-130f, 165f), new Vector2(150f, 150f));
            dashGlow = UI.Image(dash, "Glow", Art.Get("FX/glow"), Palette.Cyan.WithAlpha(0.5f));
            dashGlow.rectTransform.sizeDelta = new Vector2(260f, 260f);
            var disc = UI.Image(dash, "Disc", Art.Get("UI/disc_smooth"), new Color(0.05f, 0.04f, 0.13f, 0.85f));
            UI.Fill(disc.rectTransform);
            var ringBack = UI.Image(dash, "RingBack", Art.Get("UI/ring_smooth"), Palette.Cyan.WithAlpha(0.18f));
            UI.Fill(ringBack.rectTransform);
            dashRing = UI.Image(dash, "Ring", Art.Get("UI/ring_smooth"), Palette.Cyan);
            UI.Fill(dashRing.rectTransform);
            dashRing.type = Image.Type.Filled;
            dashRing.fillMethod = Image.FillMethod.Radial360;
            dashRing.fillOrigin = (int)Image.Origin360.Top;
            dashRing.fillClockwise = true;
            dashIcon = UI.Icon(dash, "dash", Palette.Cyan, 70f);
            dashLabel = UI.Label(dash, "DASH", 24f, Palette.Cyan, true, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(200f, 34f));

            // --- bottom-left: active power-ups
            for (var i = 0; i < 3; i++)
            {
                var slot = UI.Rect(Root, "Power" + i, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(90f + i * 120f, 90f), new Vector2(100f, 100f));
                var d = UI.Image(slot, "Disc", Art.Get("UI/disc_smooth"), new Color(0.05f, 0.04f, 0.13f, 0.85f));
                UI.Fill(d.rectTransform);
                powerRings[i] = UI.Image(slot, "Ring", Art.Get("UI/ring_smooth"), Color.white);
                UI.Fill(powerRings[i].rectTransform);
                powerRings[i].type = Image.Type.Filled;
                powerRings[i].fillMethod = Image.FillMethod.Radial360;
                powerRings[i].fillOrigin = (int)Image.Origin360.Top;
                powerIcons[i] = UI.Icon(slot, "shield", Color.white, 54f);
                powerSlots[i] = slot;
                slot.gameObject.SetActive(false);
            }

            // --- centre: toasts
            toast = UI.Label(Root, "", 84f, Palette.Amber, true, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 110f));
            toast.alpha = 0f;

            // --- bottom-centre: control hints
            hints = UI.Rect(Root, "Hints", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(1260f, 86f));
            hintsGroup = hints.gameObject.AddComponent<CanvasGroup>();
            AddHint(hints, -420f, "hand", "TAP", "JUMP", "SPACE");
            AddHint(hints, 0f, "arrow_up", "SWIPE", "LANE", "W/S");
            AddHint(hints, 420f, "arrow_up", "SWIPE", "DASH", "D", -90f);
        }

        private static void AddHint(Transform parent, float x, string icon, string gesture, string action, string key, float rotation = 0f)
        {
            var chip = UI.Panel(parent, "Hint" + action, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f),
                new Vector2(390f, 80f), Palette.Muted.WithAlpha(0.6f), new Color(0.04f, 0.03f, 0.1f, 0.75f), false);
            var i = UI.Icon(chip, icon, Palette.Cyan, 46f);
            i.rectTransform.anchorMin = i.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            i.rectTransform.anchoredPosition = new Vector2(44f, 0f);
            i.rectTransform.localRotation = Quaternion.Euler(0, 0, rotation);
            var t = UI.Label(chip, $"<color=#8C93C4>{gesture}</color> {action}", 28f, Palette.Text, true, TextAlignmentOptions.Left);
            UI.Fill(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(84f, 0f);
            t.rectTransform.offsetMax = new Vector2(-90f, 0f);
            var k = UI.Label(chip, key, 18f, Palette.Muted, false, TextAlignmentOptions.Center, false);
            k.rectTransform.anchorMin = k.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            k.rectTransform.pivot = new Vector2(1f, 0.5f);
            k.rectTransform.anchoredPosition = new Vector2(-14f, 0f);
            k.rectTransform.sizeDelta = new Vector2(80f, 30f);
        }

        public void SetHintsVisible(bool on, bool instant = false)
        {
            if (instant) hintsGroup.alpha = on ? 1f : 0f;
            else Tw.Fade(hintsGroup, on ? 1f : 0f, 0.6f);
        }

        public void SetPips(int current, int max)
        {
            if (current == shownPips && max == shownMax) return;
            shownMax = max;
            var lost = current < shownPips;
            for (var i = 0; i < pips.Length; i++)
            {
                pips[i].transform.parent.gameObject.SetActive(i < max);
                var on = i < current;
                pips[i].color = on ? (current == 1 ? Palette.Red : Palette.Cyan) : Palette.Cyan.WithAlpha(0.08f);
            }
            if (lost && current >= 0 && current < pips.Length) Tw.Punch(pips[current].transform.parent, 0.6f, 0.45f);
            shownPips = current;
        }

        public void SetScore(int value, float meters)
        {
            score.text = value.ToString("N0");
            distance.text = $"{meters:N0} M";
        }

        public void SetMultiplier(int mult, float progress)
        {
            comboBar.fillAmount = mult >= 5 ? 1f : progress;
            if (mult == shownMult) return;
            // No badge at x1: a grey "x1" plate just reads as a disabled button.
            multBadge.gameObject.SetActive(mult > 1);
            comboBar.transform.parent.gameObject.SetActive(mult > 1);
            multText.text = "x" + mult;
            var c = mult >= 5 ? Palette.Amber : mult >= 3 ? Palette.Magenta : mult >= 2 ? Palette.Violet : Palette.Muted;
            multBadge.Find("Fill").GetComponent<Image>().color = c;
            multBadge.Find("Frame").GetComponent<Image>().color = Color.Lerp(c, Color.white, 0.5f);
            comboBar.color = c;
            if (mult > shownMult && shownMult > 0) Tw.Punch(multBadge, 0.45f, 0.4f);
            shownMult = mult;
        }

        public void SetCredits(int value)
        {
            if (value == shownCredits) return;
            credits.text = value.ToString("N0");
            if (value > shownCredits && shownCredits >= 0) Tw.Punch(credits.transform.parent, 0.12f, 0.2f);
            shownCredits = value;
        }

        public void SetDash(float ready01)
        {
            dashRing.fillAmount = ready01;
            var ready = ready01 >= 1f;
            var pulse = ready ? 0.55f + Mathf.Sin(Time.unscaledTime * 6f) * 0.2f : 0.08f;
            dashGlow.color = Palette.Cyan.WithAlpha(pulse);
            dashIcon.color = ready ? Palette.Cyan : Palette.Muted;
            dashLabel.color = ready ? Palette.Cyan : Palette.Muted;
            if (ready && !dashWasReady) Tw.Punch(dashIcon.transform.parent, 0.25f, 0.35f);
            dashWasReady = ready;
        }

        public void SetPower(int slot, bool active, PowerKind kind, float remaining01)
        {
            var s = powerSlots[slot];
            if (s.gameObject.activeSelf != active)
            {
                s.gameObject.SetActive(active);
                if (active) { s.localScale = Vector3.zero; Tw.Scale(s, Vector3.one, 0.35f); }
            }
            if (!active) return;
            var c = World.PowerColor(kind);
            powerIcons[slot].sprite = Art.Get("UI/icon_" + World.PowerIcon(kind));
            powerIcons[slot].color = c;
            powerRings[slot].color = c;
            powerRings[slot].fillAmount = remaining01;
            // Blink in the last 20% so the player knows it's about to end.
            var blink = remaining01 < 0.2f && Mathf.Repeat(Time.unscaledTime * 6f, 1f) > 0.5f;
            powerIcons[slot].color = blink ? c.WithAlpha(0.3f) : c;
        }

        public void Toast(string title, Color color, float hold = 0.9f)
        {
            toast.text = title;
            toast.color = color;
            toast.rectTransform.localScale = Vector3.one * 0.5f;
            Tw.Scale(toast.rectTransform, Vector3.one, 0.35f, Ease.OutBack);
            Tw.Value(hold + 0.5f, v =>
            {
                var a = v < 0.12f ? v / 0.12f : v > 0.75f ? 1f - (v - 0.75f) / 0.25f : 1f;
                toast.alpha = a;
            }, Ease.Linear, 0f, null, "toast");
        }
    }

    // ================================================================= MENU ==
    public sealed class MenuScreen : UIScreen
    {
        private readonly TextMeshProUGUI credits;
        private readonly TextMeshProUGUI best;
        private readonly TextMeshProUGUI rank;
        private readonly Image[] stars = new Image[3];
        private readonly TextMeshProUGUI logo;
        private readonly TextMeshProUGUI logoGhost;
        private float flickerClock;

        public MenuScreen(Transform parent, GameDirector d) : base(parent, "Menu")
        {
            var col = UI.Rect(Root, "Column", new Vector2(0.6f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1100f, 1000f));

            logoGhost = UI.Label(col, "NINJAZON", 150f, Palette.Magenta.WithAlpha(0.85f), true, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-8f, -14f), new Vector2(1100f, 200f));
            logo = UI.Label(col, "NINJAZON", 150f, Palette.Cyan, true, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1100f, 200f));

            // Backing plate so the rank/best lines don't float over the busy skyline.
            UI.Panel(col, "RankPlate", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -226f), new Vector2(780f, 126f),
                Palette.Violet, Palette.Panel);
            rank = UI.Label(col, "COURIER RANK 1", 34f, Palette.Text, true, TextAlignmentOptions.Right,
                new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(60f, -234f), new Vector2(600f, 50f));
            for (var i = 0; i < 3; i++)
            {
                stars[i] = UI.Icon(col, "star", Palette.Amber, 58f);
                stars[i].rectTransform.anchorMin = stars[i].rectTransform.anchorMax = new Vector2(0.5f, 1f);
                stars[i].rectTransform.anchoredPosition = new Vector2(112f + i * 64f, -258f);
            }
            best = UI.Label(col, "", 30f, Palette.Muted, false, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -298f), new Vector2(1100f, 46f));

            UI.Button(col, "RUN", "play", Palette.Cyan, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(560f, 150f),
                d.StartRun, true, 64f, "whoosh");
            UI.Button(col, "SHOP", "cart", Palette.Amber, new Vector2(0.5f, 0.5f), new Vector2(-320f, -220f), new Vector2(270f, 104f), d.OpenShop, false, 32f);
            UI.Button(col, "CONTRACTS", "target", Palette.Magenta, new Vector2(0.5f, 0.5f), new Vector2(30f, -220f), new Vector2(400f, 104f), d.OpenMissions, false, 30f);
            UI.Button(col, null, "gear", Palette.Muted, new Vector2(0.5f, 0.5f), new Vector2(330f, -220f), new Vector2(104f, 104f), () => d.OpenSettings(), false);

            credits = UI.CreditPill(Root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -36f), 220f);
        }

        public void Refresh(Profile p)
        {
            credits.text = p.Data.credits.ToString("N0");
            best.text = p.Data.bestScore > 0 ? $"BEST  {p.Data.bestScore:N0}   //   {p.Data.bestDistance:N0} M" : "";
            rank.text = "COURIER RANK " + p.Rank;
            for (var i = 0; i < 3; i++) stars[i].color = i < p.StarsIntoRank ? Palette.Amber : Palette.Muted.WithAlpha(0.3f);
        }

        public override void Show()
        {
            base.Show();
            // Staggered entrance: each child pops in a little after the previous.
            var col = Root.Find("Column");
            for (var i = 0; i < col.childCount; i++)
            {
                var c = col.GetChild(i);
                c.localScale = new Vector3(0.85f, 0.85f, 1f);
                Tw.Scale(c, Vector3.one, 0.45f, Ease.OutBack, 0.04f * i);
            }
        }

        public void Tick()
        {
            if (!Visible) return;
            // Neon sign flicker on the logo.
            flickerClock -= Time.unscaledDeltaTime;
            var on = true;
            if (flickerClock < 0f)
            {
                if (flickerClock < -0.18f) flickerClock = Random.Range(1.5f, 4.5f);
                else on = Mathf.Repeat(Time.unscaledTime * 22f, 1f) > 0.5f;
            }
            logo.alpha = on ? 1f : 0.35f;
            logoGhost.rectTransform.anchoredPosition = new Vector2(-8f + Mathf.Sin(Time.unscaledTime * 1.3f) * 3f, -14f);
        }
    }

    // ================================================================= SHOP ==
    public sealed class ShopScreen : UIScreen
    {
        private readonly GameDirector d;
        private readonly TextMeshProUGUI credits;
        private readonly RectTransform upgradesPage;
        private readonly RectTransform trailsPage;
        private readonly UI.NeonButton tabUpgrades;
        private readonly UI.NeonButton tabTrails;
        private readonly List<System.Action> refreshers = new();

        public ShopScreen(Transform parent, GameDirector director) : base(parent, "Shop")
        {
            d = director;
            var dim = UI.Image(Root, "Dim", Art.Get("FX/pixel", 4f), Palette.Ink.WithAlpha(0.78f));
            UI.Fill(dim.rectTransform);
            dim.raycastTarget = true;
            var panel = UI.Panel(Root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560f, 940f), Palette.Amber);
            UI.Label(panel, "BLACK MARKET", 64f, Palette.Amber, true, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(900f, 80f));
            UI.IconButton(panel, "back", Palette.Text, new Vector2(0f, 1f), new Vector2(80f, -70f), 92f, d.Back, "ui_back");
            credits = UI.CreditPill(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -32f), 240f);

            tabUpgrades = UI.Button(panel, "UPGRADES", "arrow_up", Palette.Amber, new Vector2(0.5f, 1f), new Vector2(-200f, -160f), new Vector2(360f, 84f),
                () => ShowTab(false), true, 32f);
            tabTrails = UI.Button(panel, "TRAILS", "scarf", Palette.Amber, new Vector2(0.5f, 1f), new Vector2(200f, -160f), new Vector2(360f, 84f),
                () => ShowTab(true), false, 32f);

            upgradesPage = UI.Rect(panel, "Upgrades", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(1440f, 700f));
            for (var i = 0; i < Upgrades.Count; i++) BuildUpgradeRow(i);
            trailsPage = UI.Rect(panel, "Trails", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(1440f, 700f));
            for (var i = 0; i < Trails.Count; i++) BuildTrailCard(i);
            ShowTab(false);
        }

        private void ShowTab(bool trails)
        {
            upgradesPage.gameObject.SetActive(!trails);
            trailsPage.gameObject.SetActive(trails);
            Style(tabUpgrades, !trails);
            Style(tabTrails, trails);
        }

        private static void Style(UI.NeonButton b, bool selected)
        {
            b.Paint(Palette.Amber, selected);
        }

        private void BuildUpgradeRow(int id)
        {
            var y = 280f - id * 140f;
            var row = UI.Panel(upgradesPage, "Row" + id, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(1420f, 124f),
                Palette.Violet.WithAlpha(0.6f), Palette.PanelLight, false);
            var iconBg = UI.Image(row, "IconBg", Art.Sliced("UI/chamfer_fill", 20f), Palette.Ink, true);
            iconBg.rectTransform.anchorMin = iconBg.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            iconBg.rectTransform.anchoredPosition = new Vector2(76f, 0f);
            iconBg.rectTransform.sizeDelta = new Vector2(96f, 96f);
            var icon = UI.Icon(iconBg.transform, Upgrades.Icons[id], Palette.Amber, 58f);
            icon.rectTransform.anchoredPosition = Vector2.zero;
            UI.Label(row, Upgrades.Names[id], 36f, Palette.Text, true, TextAlignmentOptions.Left,
                new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(150f, 4f), new Vector2(600f, 48f));
            var desc = UI.Label(row, "", 24f, Palette.Muted, false, TextAlignmentOptions.Left,
                new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(150f, -6f), new Vector2(940f, 40f));
            // Level pips sit on the title line, right-aligned before the buy button,
            // so the description underneath has the full width.
            var pips = UI.Pips(row, Upgrades.MaxLevel(id), 34f, 8f, Palette.Amber, new Vector2(1f, 0.5f), new Vector2(1f, 0f), new Vector2(-330f, 6f));
            UI.NeonButton buy = null;
            buy = UI.Button(row, "0", "coin", Palette.Amber, new Vector2(1f, 0.5f), new Vector2(-160f, 0f), new Vector2(270f, 92f), () =>
            {
                if (d.Profile.BuyUpgrade(id))
                {
                    UI.Audio?.Play("ui_buy");
                    Tw.Punch(row, 0.06f, 0.3f);
                    RefreshAll();
                }
                else
                {
                    UI.Audio?.Play("ui_deny");
                    Tw.Value(0.35f, v => buy.rect.anchoredPosition = new Vector2(-160f + Mathf.Sin(v * 40f) * (1f - v) * 14f, 0f), Ease.Linear);
                }
            }, true, 34f, null);
            refreshers.Add(() =>
            {
                var level = d.Profile.Level(id);
                var max = Upgrades.MaxLevel(id);
                desc.text = level < max ? $"{Upgrades.Describe(id, level)}  >  <color=#FFB020>{Upgrades.Describe(id, level + 1)}</color>" : Upgrades.Describe(id, level);
                for (var i = 0; i < pips.Length; i++) pips[i].color = i < level ? Palette.Amber : Palette.Amber.WithAlpha(0.08f);
                if (level >= max)
                {
                    buy.label.text = "MAXED";
                    buy.ShowIcon(false);
                    buy.button.interactable = false;
                }
                else
                {
                    var cost = Upgrades.Costs[id][level];
                    var afford = d.Profile.Data.credits >= cost;
                    buy.label.text = cost.ToString("N0");
                    buy.Paint(afford ? Palette.Amber : Palette.Muted, afford);
                }
            });
        }

        private void BuildTrailCard(int i)
        {
            var colIndex = i % 4;
            var rowIndex = i / 4;
            var count = rowIndex == 0 ? 4 : Trails.Count - 4;
            var x = (colIndex - (count - 1) * 0.5f) * 340f;
            var y = rowIndex == 0 ? 170f : -175f;
            var color = Trails.Colors[i];
            var card = UI.Panel(trailsPage, "Trail" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(310f, 320f),
                color.WithAlpha(0.7f), Palette.PanelLight, false);
            // Preview: the runner mid-stride with this trail's scarf colour, and the trail
            // itself as pixel speed-streaks behind him (that's what you are buying).
            for (var s = 0; s < 3; s++)
            {
                var streak = UI.Image(card, "Streak", Art.Get("FX/pixel", 4f), color.WithAlpha(0.9f - s * 0.25f));
                streak.rectTransform.anchoredPosition = new Vector2(-62f - s * 10f, 70f - s * 26f);
                streak.rectTransform.sizeDelta = new Vector2(120f - s * 30f, 10f);
            }
            var sheet = Art.RunnerSheet(color, i == 0);
            var preview = UI.Image(card, "Preview", Art.Slice(sheet, new UnityEngine.Rect(400, 662, 190, 195), new Vector2(0.5f, 0.5f), 100f, "preview"), Color.white);
            preview.rectTransform.anchoredPosition = new Vector2(10f, 46f);
            preview.rectTransform.sizeDelta = new Vector2(164f, 168f);
            UI.Label(card, Trails.Names[i], 26f, color, true, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(300f, 40f));
            UI.NeonButton btn = null;
            btn = UI.Button(card, "0", "coin", color, new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(250f, 70f), () =>
            {
                var owned = d.Profile.OwnsTrail(i);
                if (d.Profile.BuyOrEquipTrail(i))
                {
                    UI.Audio?.Play(owned ? "ui_tap" : "ui_buy");
                    Tw.Punch(card, 0.08f, 0.3f);
                    d.ApplyTrail();
                    RefreshAll();
                }
                else UI.Audio?.Play("ui_deny");
            }, true, 28f, null);
            refreshers.Add(() =>
            {
                var owned = d.Profile.OwnsTrail(i);
                var equipped = d.Profile.Data.trail == i;
                btn.ShowIcon(!owned);
                btn.label.text = equipped ? "EQUIPPED" : owned ? "EQUIP" : Trails.Costs[i].ToString("N0");
                var afford = owned || d.Profile.Data.credits >= Trails.Costs[i];
                // Same rule as upgrades: gold = you can buy it. Owned trails use their own colour.
                btn.Paint(owned ? color : afford ? Palette.Amber : Palette.Muted, afford && !equipped);
                btn.button.interactable = !equipped;
            });
        }

        public void RefreshAll()
        {
            credits.text = d.Profile.Data.credits.ToString("N0");
            foreach (var r in refreshers) r();
        }

        public override void Show()
        {
            RefreshAll();
            base.Show();
        }
    }

    // ============================================================= MISSIONS ==
    public sealed class MissionsScreen : UIScreen
    {
        private readonly GameDirector d;
        private readonly TextMeshProUGUI rank;
        private readonly Image[] stars = new Image[3];
        private readonly MissionCard[] cards = new MissionCard[3];

        public sealed class MissionCard
        {
            public RectTransform root;
            public TextMeshProUGUI text, progress, reward;
            public Image bar, icon, done; // done = gold star shown once the contract is complete

            public void Bind(MissionSlot m)
            {
                text.text = Missions.Text(m).ToUpper();
                var p = Mathf.Min(m.progress, m.target);
                // Completed = gold: gold star replaces the contract icon, gold bar, "CLAIMED".
                progress.text = m.complete ? "CLAIMED" : $"{p:N0} / {m.target:N0}";
                progress.color = m.complete ? Palette.Amber : Palette.Muted;
                bar.fillAmount = m.complete ? 1f : p / (float)m.target;
                bar.color = m.complete ? Palette.Amber : Palette.Magenta;
                reward.text = "+" + m.reward;
                icon.gameObject.SetActive(!m.complete);
                done.gameObject.SetActive(m.complete);
            }
        }

        public static MissionCard BuildCard(Transform parent, Vector2 pos, float width, float height = 150f)
        {
            var c = new MissionCard();
            c.root = UI.Panel(parent, "Mission", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(width, height),
                Palette.Magenta.WithAlpha(0.55f), Palette.PanelLight, false);
            c.icon = UI.Icon(c.root, "trophy", Palette.Magenta, 64f);
            c.icon.rectTransform.anchorMin = c.icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            c.icon.rectTransform.anchoredPosition = new Vector2(64f, 0f);
            c.done = UI.Icon(c.root, "star", Palette.Amber, 64f);
            c.done.rectTransform.anchorMin = c.done.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            c.done.rectTransform.anchoredPosition = new Vector2(64f, 0f);
            c.text = UI.Label(c.root, "", 30f, Palette.Text, true, TextAlignmentOptions.Left,
                new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(122f, 10f), new Vector2(width - 360f, 44f));
            c.text.textWrappingMode = TextWrappingModes.Normal;
            c.text.enableAutoSizing = true;
            c.text.fontSizeMin = 20f;
            c.text.fontSizeMax = 30f;
            c.bar = UI.Bar(c.root, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(122f, -8f), new Vector2(width - 600f, 26f), Palette.Magenta);
            c.progress = UI.Label(c.root, "", 24f, Palette.Muted, false, TextAlignmentOptions.Left,
                new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(width - 462f, -2f), new Vector2(200f, 36f));
            var rewardIcon = UI.Icon(c.root, "coin", Palette.Amber, 44f);
            rewardIcon.rectTransform.anchorMin = rewardIcon.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            rewardIcon.rectTransform.anchoredPosition = new Vector2(-200f, 0f);
            // Right-aligned to the card's inner edge so "+160" can never spill past the frame.
            c.reward = UI.Label(c.root, "", 36f, Palette.Amber, true, TextAlignmentOptions.Right,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-32f, 0f), new Vector2(140f, 50f));
            return c;
        }

        public MissionsScreen(Transform parent, GameDirector director) : base(parent, "Missions")
        {
            d = director;
            var dim = UI.Image(Root, "Dim", Art.Get("FX/pixel", 4f), Palette.Ink.WithAlpha(0.78f));
            UI.Fill(dim.rectTransform);
            dim.raycastTarget = true;
            var panel = UI.Panel(Root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1320f, 760f), Palette.Magenta);
            UI.Label(panel, "CONTRACTS", 64f, Palette.Magenta, true, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(900f, 80f));
            UI.IconButton(panel, "back", Palette.Text, new Vector2(0f, 1f), new Vector2(80f, -70f), 92f, d.Back, "ui_back");
            rank = UI.Label(panel, "", 36f, Palette.Text, true, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-60f, -130f), new Vector2(700f, 50f));
            for (var i = 0; i < 3; i++)
            {
                stars[i] = UI.Icon(panel, "star", Palette.Amber, 48f);
                stars[i].rectTransform.anchorMin = stars[i].rectTransform.anchorMax = new Vector2(0.5f, 1f);
                stars[i].rectTransform.anchoredPosition = new Vector2(220f + i * 56f, -155f);
            }
            // Below the hint line (ends 236 from the top), 15 apart, ~35 above the panel bottom.
            for (var i = 0; i < 3; i++) cards[i] = BuildCard(panel, new Vector2(0f, 60f - i * 165f), 1200f);
        }

        public override void Show()
        {
            var p = d.Profile;
            rank.text = "COURIER RANK " + p.Rank;
            for (var i = 0; i < 3; i++) stars[i].color = i < p.StarsIntoRank ? Palette.Amber : Palette.Muted.WithAlpha(0.3f);
            for (var i = 0; i < 3; i++)
            {
                cards[i].Bind(p.Data.missions[i]);
                cards[i].root.localScale = new Vector3(0.9f, 0.9f, 1f);
                Tw.Scale(cards[i].root, Vector3.one, 0.4f, Ease.OutBack, 0.06f * i);
            }
            base.Show();
        }
    }

    // ================================================================ PAUSE ==
    public sealed class PauseScreen : UIScreen
    {
        private readonly GameDirector d;
        private readonly TextMeshProUGUI[] lines = new TextMeshProUGUI[3];

        public PauseScreen(Transform parent, GameDirector director) : base(parent, "Pause")
        {
            d = director;
            var dim = UI.Image(Root, "Dim", Art.Get("FX/pixel", 4f), Palette.Ink.WithAlpha(0.7f));
            UI.Fill(dim.rectTransform);
            dim.raycastTarget = true;
            var panel = UI.Panel(Root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 860f), Palette.Cyan);
            UI.Label(panel, "PAUSED", 76f, Palette.Cyan, true, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(800f, 90f));
            for (var i = 0; i < 3; i++)
                lines[i] = UI.Label(panel, "", 28f, Palette.Text, false, TextAlignmentOptions.Center,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f - i * 44f), new Vector2(940f, 40f));
            UI.Button(panel, "RESUME", "play", Palette.Cyan, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(560f, 120f), d.Resume, true, 48f);
            UI.Button(panel, "RESTART", null, Palette.Magenta, new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(560f, 96f), d.Restart, false, 36f);
            UI.Button(panel, "SETTINGS", "gear", Palette.Muted, new Vector2(0.5f, 0.5f), new Vector2(-145f, -240f), new Vector2(270f, 96f), () => d.OpenSettings(), false, 30f);
            UI.Button(panel, "MENU", "back", Palette.Muted, new Vector2(0.5f, 0.5f), new Vector2(145f, -240f), new Vector2(270f, 96f), d.GoToMenu, false, 30f);
        }

        public override void Show()
        {
            var ms = d.Profile.Data.missions;
            for (var i = 0; i < 3; i++)
            {
                var m = ms[i];
                var progress = d.LiveMissionProgress(m);
                lines[i].text = m.complete || progress >= m.target
                    ? $"<color=#3DFFA0>[DONE]</color>  {Missions.Text(m).ToUpper()}"
                    : $"{Missions.Text(m).ToUpper()}   <color=#FF2A8A>{Mathf.Min(progress, m.target)}/{m.target}</color>";
            }
            base.Show();
        }
    }

    // ============================================================= SETTINGS ==
    public sealed class SettingsScreen : UIScreen
    {
        private readonly System.Action<bool> setVibration, setShake, setHints;
        private readonly Slider music, sfx;
        private readonly GameDirector d;

        public SettingsScreen(Transform parent, GameDirector director) : base(parent, "Settings")
        {
            d = director;
            var data = d.Profile.Data;
            var dim = UI.Image(Root, "Dim", Art.Get("FX/pixel", 4f), Palette.Ink.WithAlpha(0.8f));
            UI.Fill(dim.rectTransform);
            dim.raycastTarget = true;
            var panel = UI.Panel(Root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 840f), Palette.Cyan);
            UI.Label(panel, "SETTINGS", 64f, Palette.Text, true, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(800f, 80f));
            UI.IconButton(panel, "back", Palette.Text, new Vector2(0f, 1f), new Vector2(80f, -70f), 92f, d.Back, "ui_back");
            music = UI.Slider(panel, "MUSIC", new Vector2(0f, 230f), data.musicVolume, v => { data.musicVolume = v; d.ApplySettings(false); });
            sfx = UI.Slider(panel, "SOUND FX", new Vector2(0f, 130f), data.sfxVolume, v => { data.sfxVolume = v; d.ApplySettings(false); });
            setVibration = UI.Switch(panel, "VIBRATION", new Vector2(0f, 30f), data.vibration, v => { data.vibration = v; d.ApplySettings(true); });
            setShake = UI.Switch(panel, "SCREEN SHAKE", new Vector2(0f, -70f), data.screenShake, v => { data.screenShake = v; d.ApplySettings(true); });
            setHints = UI.Switch(panel, "CONTROL HINTS", new Vector2(0f, -170f), data.controlHints, v => { data.controlHints = v; d.ApplySettings(true); });
            UI.Button(panel, "REPLAY TRAINING", "hand", Palette.Amber, new Vector2(0.5f, 0.5f), new Vector2(0f, -310f), new Vector2(560f, 100f),
                d.ReplayTutorial, false, 32f);
        }

        public override void Show()
        {
            var data = d.Profile.Data;
            music.SetValueWithoutNotify(data.musicVolume);
            sfx.SetValueWithoutNotify(data.sfxVolume);
            setVibration(data.vibration);
            setShake(data.screenShake);
            setHints(data.controlHints);
            base.Show();
        }
    }

    // ============================================================= CONTINUE ==
    public sealed class ContinueScreen : UIScreen
    {
        private readonly Image ring;
        private readonly UI.NeonButton accept;
        private readonly TextMeshProUGUI costText;

        public ContinueScreen(Transform parent, GameDirector d) : base(parent, "Continue")
        {
            var dim = UI.Image(Root, "Dim", Art.Get("FX/pixel", 4f), Palette.Ink.WithAlpha(0.55f));
            UI.Fill(dim.rectTransform);
            dim.raycastTarget = true;
            UI.Label(Root, "TRACE LOCKED", 110f, Palette.Magenta, true, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 270f), new Vector2(1600f, 130f));
            var ringRoot = UI.Rect(Root, "Timer", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(190f, 190f));
            var back = UI.Image(ringRoot, "Back", Art.Get("UI/ring_smooth"), Palette.Mint.WithAlpha(0.15f));
            UI.Fill(back.rectTransform);
            ring = UI.Image(ringRoot, "Ring", Art.Get("UI/ring_smooth"), Palette.Mint);
            UI.Fill(ring.rectTransform);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            var heart = UI.Icon(ringRoot, "heart", Palette.Mint, 90f);
            heart.rectTransform.anchoredPosition = Vector2.zero;
            accept = UI.Button(Root, "CONTINUE", "coin", Palette.Mint, new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(620f, 130f),
                d.AcceptContinue, true, 44f, "powerup");
            costText = UI.Label(Root, "", 30f, Palette.Amber, true, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -262f), new Vector2(900f, 44f));
            UI.Button(Root, "NO THANKS", null, Palette.Muted, new Vector2(0.5f, 0.5f), new Vector2(0f, -390f), new Vector2(360f, 84f), d.DeclineContinue, false, 30f);
        }

        public void Setup(int cost, int walletCredits)
        {
            costText.text = $"COSTS {cost:N0} CREDITS";
            accept.button.interactable = walletCredits >= cost;
        }

        public void SetTimer(float remaining01) => ring.fillAmount = remaining01;
    }

    // ============================================================== RESULTS ==
    public sealed class ResultsScreen : UIScreen
    {
        private readonly TextMeshProUGUI score;
        private readonly TextMeshProUGUI newBest;
        private readonly TextMeshProUGUI earned;
        private readonly MissionsScreen.MissionCard[] cards = new MissionsScreen.MissionCard[3];
        private readonly GameDirector d;

        public ResultsScreen(Transform parent, GameDirector director) : base(parent, "Results")
        {
            d = director;
            var dim = UI.Image(Root, "Dim", Art.Get("FX/pixel", 4f), Palette.Ink.WithAlpha(0.72f));
            UI.Fill(dim.rectTransform);
            dim.raycastTarget = true;
            var panel = UI.Panel(Root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1500f, 960f), Palette.Cyan);
            UI.Label(panel, "SHIFT REPORT", 44f, Palette.Cyan, true, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(900f, 60f));
            score = UI.Label(panel, "0", 150f, Palette.Text, true, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(1200f, 160f));
            newBest = UI.Label(panel, "NEW BEST!", 40f, Palette.Amber, true, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -226f), new Vector2(900f, 56f));
            earned = UI.Label(panel, "", 50f, Palette.Amber, true, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -338f), new Vector2(1400f, 50f));
            // Cards fit between the "+credits" line (ends ~117 above centre) and the buttons
            // (top edge 344 below centre): 3 x 120 tall + 14 gaps, with ~40 clear above the buttons.
            for (var i = 0; i < 3; i++) cards[i] = MissionsScreen.BuildCard(panel, new Vector2(0f, 25f - i * 134f), 1300f, 120f);
            UI.Button(panel, "RUN AGAIN", "play", Palette.Cyan, new Vector2(0.5f, 0f), new Vector2(-150f, 76f), new Vector2(560f, 120f), d.StartRunFromResults, true, 46f, "whoosh");
            UI.Button(panel, "SHOP", "cart", Palette.Amber, new Vector2(0.5f, 0f), new Vector2(290f, 76f), new Vector2(260f, 104f), d.OpenShop, false, 32f);
            UI.Button(panel, null, "back", Palette.Muted, new Vector2(0.5f, 0f), new Vector2(510f, 76f), new Vector2(104f, 104f), d.GoToMenu, false);
        }

        public void Present(int finalScore, bool isBest, int creditsEarned, MissionSlot[] missions, bool[] justCompleted)
        {
            Show();
            // Not a record: show the best to beat (small, muted). A record pops in later.
            newBest.text = isBest ? "NEW BEST!" : $"BEST  {d.Profile.Data.bestScore:N0}";
            newBest.color = isBest ? Palette.Amber : Palette.Muted;
            newBest.fontSize = isBest ? 40f : 28f;
            newBest.gameObject.SetActive(!isBest);
            score.text = "0";
            earned.text = "";
            // Count the score up for drama, then reveal the best/credits lines.
            Tw.Value(1.1f, v =>
            {
                score.text = Mathf.RoundToInt(finalScore * v).ToString("N0");
                if (Mathf.Repeat(v * 30f, 1f) < 0.2f && v < 1f) UI.Audio?.Play("tick", 0.4f, 1f + v);
            }, Ease.OutCubic, 0.25f, () =>
            {
                Tw.Punch(score.transform, 0.15f, 0.35f);
                if (isBest)
                {
                    newBest.gameObject.SetActive(true);
                    newBest.transform.localScale = Vector3.zero;
                    Tw.Scale(newBest.transform, Vector3.one, 0.5f, Ease.OutElastic);
                    UI.Audio?.Play("new_best");
                }
                earned.text = $"+{creditsEarned:N0} CREDITS";
                Tw.Punch(earned.transform, 0.2f, 0.3f);
            });
            for (var i = 0; i < 3; i++)
            {
                cards[i].Bind(missions[i]);
                if (justCompleted[i])
                {
                    var c = cards[i];
                    c.done.gameObject.SetActive(false);
                    c.icon.gameObject.SetActive(true);
                    c.bar.fillAmount = 0.75f;
                    Tw.Delay(1.6f + 0.45f * i, () =>
                    {
                        c.done.gameObject.SetActive(true);
                        c.icon.gameObject.SetActive(false);
                        c.bar.fillAmount = 1f;
                        Tw.Punch(c.root, 0.08f, 0.4f);
                        c.done.transform.localScale = Vector3.zero;
                        Tw.Scale(c.done.transform, Vector3.one, 0.45f, Ease.OutElastic);
                        UI.Audio?.Play("mission");
                    });
                }
            }
        }
    }

    // ============================================================= TUTORIAL ==
    public sealed class TutorialOverlay : UIScreen
    {
        private readonly TextMeshProUGUI title;
        private readonly TextMeshProUGUI sub;
        private readonly Image hand;
        private readonly Image arrow;
        private Prompt prompt;

        public TutorialOverlay(Transform parent) : base(parent, "Tutorial")
        {
            var vignette = UI.Image(Root, "Vignette", Art.Get("FX/vignette"), new Color(1f, 1f, 1f, 0.9f));
            UI.Fill(vignette.rectTransform);
            var card = UI.Panel(Root, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(160f, 160f), new Vector2(760f, 220f), Palette.Cyan,
                new Color(0.05f, 0.04f, 0.13f, 0.92f));
            title = UI.Label(card, "", 60f, Palette.Cyan, true, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(740f, 80f));
            sub = UI.Label(card, "", 28f, Palette.Text, false, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -45f), new Vector2(740f, 44f));
            hand = UI.Icon(Root, "hand", Color.white, 150f);
            hand.rectTransform.anchorMin = hand.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            arrow = UI.Icon(Root, "arrow_up", Palette.Cyan, 110f);
            arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        }

        private bool laneDown;

        public void Present(Prompt p, bool down = true)
        {
            prompt = p;
            laneDown = down;
            switch (p)
            {
                case Prompt.Jump: title.text = "TAP TO JUMP"; sub.text = "clear barricades and gaps  //  SPACE"; break;
                case Prompt.Lane:
                    title.text = down ? "SWIPE DOWN" : "SWIPE UP";
                    sub.text = down ? "change lane to dodge  //  S or DOWN" : "change lane to dodge  //  W or UP";
                    break;
                case Prompt.Dash: title.text = "SWIPE RIGHT"; sub.text = "dash-slice through drones  //  D"; break;
                case Prompt.DoubleJump: title.text = "TAP AGAIN!"; sub.text = "double jump in mid-air"; break;
            }
            Show();
        }

        public void Tick()
        {
            if (!Visible) return;
            var t = Mathf.Repeat(Time.unscaledTime * 1.4f, 1f);
            var center = new Vector2(160f, -110f);
            var hrt = hand.rectTransform;
            var art = arrow.rectTransform;
            switch (prompt)
            {
                case Prompt.Lane:
                    var dir = laneDown ? -1f : 1f;
                    hrt.anchoredPosition = center + new Vector2(0f, -dir * 60f + dir * Tw.Evaluate(Ease.OutCubic, t) * 150f);
                    art.anchoredPosition = center + new Vector2(130f, dir * 20f);
                    art.localRotation = Quaternion.Euler(0, 0, laneDown ? 180f : 0f);
                    break;
                case Prompt.Dash:
                    hrt.anchoredPosition = center + new Vector2(-80f + Tw.Evaluate(Ease.OutCubic, t) * 190f, 0f);
                    art.anchoredPosition = center + new Vector2(170f, 100f);
                    art.localRotation = Quaternion.Euler(0, 0, -90f);
                    break;
                default:
                    var press = t < 0.25f ? 1f - t / 0.25f : 0f;
                    hrt.anchoredPosition = center + new Vector2(0f, -press * 18f);
                    hrt.localScale = Vector3.one * (1f - press * 0.12f);
                    art.anchoredPosition = center + new Vector2(130f, 40f + Tw.Evaluate(Ease.OutCubic, t) * 40f);
                    art.localRotation = Quaternion.identity;
                    break;
            }
            hand.color = Color.white.WithAlpha(0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f));
        }
    }
}
