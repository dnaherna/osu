// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Rulesets.Mania.UI
{
    public partial class ManiaTouchLayoutEditor : OsuScreen
    {
        public override string Title => "Mania touch layout";
        public override bool HideOverlaysOnEnter => true;
        public override bool RequiresPortraitOrientation => config.Get<ManiaMobileLayout>(ManiaRulesetSetting.MobileLayout) == ManiaMobileLayout.Portrait;
        protected override OverlayActivation InitialOverlayActivationMode => OverlayActivation.Disabled;
        protected override bool InitialBackButtonVisibility => false;

        private readonly ManiaRulesetConfigManager config;

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        private readonly Bindable<int> keyCount = new Bindable<int>(4);
        private readonly BindableBool snap = new BindableBool(true);
        private readonly BindableFloat zoneWidth = new BindableFloat(0.2f) { MinValue = 0.025f, MaxValue = 1, Precision = 0.025f };
        private readonly BindableFloat zoneHeight = new BindableFloat(0.2f) { MinValue = 0.025f, MaxValue = 1, Precision = 0.025f };
        private Container preview = null!;
        private Container zonesContainer = null!;
        private OsuSpriteText heading = null!;
        private ManiaTouchLayout[] zones = Array.Empty<ManiaTouchLayout>();
        private int selected;
        private bool updatingControls;
        private bool? portrait;
        private bool reset;

        public ManiaTouchLayoutEditor(ManiaRulesetConfigManager config)
        {
            this.config = config;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray3 },
                preview = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Masking = true,
                    CornerRadius = 8,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray2 },
                        // A reference for the centred note playfield; this does not change the skin.
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            RelativePositionAxes = Axes.X,
                            X = 0.35f,
                            Width = 0.3f,
                            Colour = Color4.White,
                            Alpha = 0.04f,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Y = 10,
                            Text = "Centre of note playfield",
                            Font = OsuFont.Default.With(size: 16),
                            Alpha = 0.5f,
                        },
                        zonesContainer = new Container { RelativeSizeAxes = Axes.Both },
                    }
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(20),
                    Spacing = new Vector2(0, 10),
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        heading = new OsuSpriteText { Font = OsuFont.Default.With(size: 26, weight: "Bold") },
                        new OsuSpriteText { Text = "Drag zones to move; select a zone to resize.", Font = OsuFont.Default.With(size: 16) },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(10, 0),
                            Children = new Drawable[]
                            {
                                new FormDropdown<int>
                                {
                                    Width = 0.2f,
                                    Caption = "Keys",
                                    Items = Enumerable.Range(1, 18),
                                    Current = keyCount,
                                },
                                new FormCheckBox
                                {
                                    Width = 0.3f,
                                    Caption = "Snap to grid (5%)",
                                    Current = snap,
                                },
                            }
                        }
                    }
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Left = 20, Right = 20, Bottom = 20 },
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
                    Children = new Drawable[]
                    {
                        new FormSliderBar<float> { Width = 0.2f, Caption = "Zone width", Current = zoneWidth, KeyboardStep = 0.025f, LabelFormat = v => $"{v:P0}" },
                        new FormSliderBar<float> { Width = 0.2f, Caption = "Zone height", Current = zoneHeight, KeyboardStep = 0.025f, LabelFormat = v => $"{v:P0}" },
                        new RoundedButton { Width = 110, Text = "Reset", Action = resetLayout },
                        new RoundedButton { Width = 110, Text = "Cancel", Action = () => this.Exit() },
                        new RoundedButton { Width = 110, Text = "Save", Action = save },
                    }
                },
            };

            for (int i = 1; i < 20; i++)
            {
                preview.Add(new Box { RelativePositionAxes = Axes.X, RelativeSizeAxes = Axes.Y, X = i / 20f, Width = 1, Alpha = 0.08f });
                preview.Add(new Box { RelativePositionAxes = Axes.Y, RelativeSizeAxes = Axes.X, Y = i / 20f, Height = 1, Alpha = 0.08f });
            }

            keyCount.BindValueChanged(_ => loadLayout());
            zoneWidth.BindValueChanged(_ => resizeSelected());
            zoneHeight.BindValueChanged(_ => resizeSelected());
        }

        protected override void Update()
        {
            base.Update();

            bool isPortrait = DrawHeight > DrawWidth;
            if (portrait != isPortrait)
            {
                portrait = isPortrait;
                loadLayout();
            }

            // Fit a preview of the entire gameplay surface without distorting its aspect ratio.
            float scale = Math.Min((DrawWidth - 40) / DrawWidth, Math.Max(1, DrawHeight - 300) / DrawHeight);
            preview.Size = DrawSize * scale;
            preview.Y = 15;
        }

        private void loadLayout()
        {
            heading.Text = $"Mania touch layout · {keyCount.Value}K · {(portrait == true ? "Portrait" : "Landscape")}";
            zones = ManiaTouchLayout.Get(config.Get<string>(ManiaRulesetSetting.TouchLayouts), keyCount.Value, portrait == true)
                    ?? ManiaTouchLayout.CreateDefault(keyCount.Value);
            reset = false;
            rebuildZones();
        }

        private void resetLayout()
        {
            zones = ManiaTouchLayout.CreateDefault(keyCount.Value);
            reset = true;
            rebuildZones();
        }

        private void rebuildZones()
        {
            zonesContainer.Clear();
            for (int i = 0; i < zones.Length; i++)
            {
                int index = i;
                zonesContainer.Add(new EditorZone(zones[i], i + 1, () => select(index), () => snap.Value, () => reset = false));
            }

            select(0);
        }

        private void select(int index)
        {
            selected = index;
            updatingControls = true;
            zoneWidth.Value = zones[index].Width;
            zoneHeight.Value = zones[index].Height;
            updatingControls = false;

            for (int i = 0; i < zonesContainer.Count; i++)
                zonesContainer[i].Alpha = i == index ? 1 : 0.55f;
        }

        private void resizeSelected()
        {
            if (updatingControls || zones.Length == 0)
                return;

            var zone = zones[selected];
            zone.Width = zoneWidth.Value;
            zone.Height = zoneHeight.Value;
            zone.Move(zone.Position, false);
            reset = false;
        }

        private void save()
        {
            var layouts = ManiaTouchLayout.Read(config.Get<string>(ManiaRulesetSetting.TouchLayouts));
            string key = ManiaTouchLayout.Key(keyCount.Value, portrait == true);
            if (reset)
                layouts.Remove(key);
            else
                layouts[key] = zones;

            config.SetValue(ManiaRulesetSetting.TouchLayouts, JsonConvert.SerializeObject(layouts));
            config.SetValue(ManiaRulesetSetting.TouchOverlay, true);
            this.Exit();
        }

        private partial class EditorZone : CompositeDrawable
        {
            private readonly ManiaTouchLayout zone;
            private readonly Action select;
            private readonly Func<bool> snap;
            private readonly Action changed;
            private Vector2 dragOffset;
            private TouchSource? activeTouch;

            public EditorZone(ManiaTouchLayout zone, int number, Action select, Func<bool> snap, Action changed)
            {
                this.zone = zone;
                this.select = select;
                this.snap = snap;
                this.changed = changed;
                Name = $"Touch zone {number}";
                RelativePositionAxes = RelativeSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 8;
                BorderThickness = 2;
                BorderColour = Color4.White;
                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(80, 150, 200, 130) },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = number.ToString(),
                        Font = OsuFont.Default.With(size: 26, weight: "Bold"),
                    },
                };
            }

            protected override void Update()
            {
                base.Update();
                Position = zone.Position;
                Size = zone.Size;
            }

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                if (e.Button != MouseButton.Left)
                    return false;

                select();
                return true;
            }

            protected override bool OnTouchDown(TouchDownEvent e)
            {
                if (activeTouch != null)
                    return true;

                activeTouch = e.Touch.Source;
                select();
                dragOffset = normalise(e.ScreenSpaceTouch.Position) - zone.Position;
                return true;
            }

            protected override void OnTouchMove(TouchMoveEvent e)
            {
                if (e.Touch.Source != activeTouch)
                    return;

                zone.Move(normalise(e.ScreenSpaceTouch.Position) - dragOffset, snap());
                changed();
            }

            protected override void OnTouchUp(TouchUpEvent e)
            {
                if (e.Touch.Source == activeTouch)
                    activeTouch = null;
            }

            protected override bool OnDragStart(DragStartEvent e)
            {
                if (e.Button != MouseButton.Left)
                    return false;

                dragOffset = normalise(e.ScreenSpaceMouseDownPosition) - zone.Position;
                return true;
            }

            protected override void OnDrag(DragEvent e)
            {
                zone.Move(normalise(e.ScreenSpaceMousePosition) - dragOffset, snap());
                changed();
            }

            private Vector2 normalise(Vector2 screenPosition)
            {
                var local = Parent!.ToLocalSpace(screenPosition);
                return new Vector2(local.X / Parent.DrawWidth, local.Y / Parent.DrawHeight);
            }
        }
    }
}
