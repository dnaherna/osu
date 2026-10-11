// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Configuration;
using osuTK;

namespace osu.Game.Rulesets.Mania.UI
{
    /// <summary>
    /// An overlay that captures and displays osu!mania mouse and touch input.
    /// </summary>
    public partial class ManiaTouchInputArea : VisibilityContainer
    {
        private readonly DrawableManiaRuleset drawableRuleset;

        // visibility state affects our child. we always want to handle input.
        public override bool PropagatePositionalInputSubTree => true;
        public override bool PropagateNonPositionalInputSubTree => true;

        [SettingSource("Spacing", "The spacing between receptors.")]
        public BindableFloat Spacing { get; } = new BindableFloat(10)
        {
            Precision = 1,
            MinValue = 0,
            MaxValue = 100,
        };

        [SettingSource("Opacity", "The receptor opacity.")]
        public BindableFloat Opacity { get; } = new BindableFloat(1)
        {
            Precision = 0.1f,
            MinValue = 0,
            MaxValue = 1
        };

        private GridContainer gridContainer = null!;
        private readonly IBindable<string> layouts;
        private bool? portrait;
        private bool layoutInvalid = true;

        public ManiaTouchInputArea(DrawableManiaRuleset drawableRuleset, ManiaRulesetConfigManager config)
        {
            this.drawableRuleset = drawableRuleset;
            layouts = config.GetBindable<string>(ManiaRulesetSetting.TouchLayouts);
            layouts.BindValueChanged(_ => layoutInvalid = true);

            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;

            RelativeSizeAxes = Axes.Both;
            Height = 0.5f;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            rebuildLayout(false);
        }

        protected override void Update()
        {
            base.Update();

            bool isPortrait = Parent!.DrawHeight > Parent.DrawWidth;
            if (layoutInvalid || portrait != isPortrait)
            {
                portrait = isPortrait;
                layoutInvalid = false;
                rebuildLayout(isPortrait);
            }
        }

        private void rebuildLayout(bool isPortrait)
        {
            var columns = drawableRuleset.Playfield.Stages.SelectMany(s => s.Columns).ToArray();
            var customLayout = ManiaTouchLayout.Get(layouts.Value, columns.Length, isPortrait);
            Height = customLayout == null ? 0.5f : 1;

            if (customLayout != null)
            {
                // Dispose old receptors first so active touches cannot leave actions held.
                ClearInternal();
                InternalChild = gridContainer = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    AlwaysPresent = true,
                    Alpha = State.Value == Visibility.Visible ? 1 : 0,
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Children = customLayout.Select((zone, i) => new ColumnInputReceptor
                                {
                                    RelativePositionAxes = Axes.Both,
                                    Position = zone.Position,
                                    Size = zone.Size,
                                    Action = { BindTarget = columns[i].Action },
                                }).ToArray()
                            }
                        }
                    }
                };
                return;
            }

            List<Drawable> receptorGridContent = new List<Drawable>();
            List<Dimension> receptorGridDimensions = new List<Dimension>();

            bool first = true;

            foreach (var stage in drawableRuleset.Playfield.Stages)
            {
                foreach (var column in stage.Columns)
                {
                    if (!first)
                    {
                        receptorGridContent.Add(new Gutter { Spacing = { BindTarget = Spacing } });
                        receptorGridDimensions.Add(new Dimension(GridSizeMode.AutoSize));
                    }

                    receptorGridContent.Add(new ColumnInputReceptor
                    {
                        Action = { BindTarget = column.Action },
                        Spacing = { BindTarget = Spacing },
                    });
                    receptorGridDimensions.Add(new Dimension());

                    first = false;
                }
            }

            ClearInternal();
            InternalChild = gridContainer = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                AlwaysPresent = true,
                Alpha = State.Value == Visibility.Visible ? 1 : 0,
                Content = new[] { receptorGridContent.ToArray() },
                ColumnDimensions = receptorGridDimensions.ToArray()
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Opacity.BindValueChanged(o => Alpha = o.NewValue, true);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            // Hide whenever the keyboard is used.
            Hide();
            return false;
        }

        protected override bool OnTouchDown(TouchDownEvent e)
        {
            Show();
            return true;
        }

        protected override void PopIn()
        {
            gridContainer.FadeIn(500, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            gridContainer.FadeOut(300);
        }

        public partial class ColumnInputReceptor : CompositeDrawable
        {
            public readonly IBindable<ManiaAction> Action = new Bindable<ManiaAction>();
            public readonly IBindable<float> Spacing = new BindableFloat();

            private readonly Box highlightOverlay;

            [Resolved]
            private ManiaInputManager? inputManager { get; set; }

            private int touchCount;

            public ColumnInputReceptor()
            {
                RelativeSizeAxes = Axes.Both;

                InternalChildren = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 10,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Alpha = 0.15f,
                            },
                            highlightOverlay = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Alpha = 0,
                                Blending = BlendingParameters.Additive,
                            }
                        }
                    }
                };
            }

            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
                // Extend input coverage to the gaps close to this receptor.
                => DrawRectangle.Inflate(new Vector2(Spacing.Value / 2, 0)).Contains(ToLocalSpace(screenSpacePos));

            protected override bool OnTouchDown(TouchDownEvent e)
            {
                if (touchCount++ == 0)
                    updateButton(true);
                return false; // handled by parent container to show overlay.
            }

            protected override void OnTouchUp(TouchUpEvent e)
            {
                if (--touchCount == 0)
                    updateButton(false);
            }

            private void updateButton(bool press)
            {
                if (press)
                {
                    inputManager?.KeyBindingContainer.TriggerPressed(Action.Value);
                    highlightOverlay.FadeTo(0.1f, 80, Easing.OutQuint);
                }
                else
                {
                    inputManager?.KeyBindingContainer.TriggerReleased(Action.Value);
                    highlightOverlay.FadeTo(0, 400, Easing.OutQuint);
                }
            }

            protected override void Dispose(bool isDisposing)
            {
                if (touchCount > 0)
                    inputManager?.KeyBindingContainer.TriggerReleased(Action.Value);

                base.Dispose(isDisposing);
            }
        }

        private partial class Gutter : Drawable
        {
            public readonly IBindable<float> Spacing = new Bindable<float>();

            public Gutter()
            {
                Spacing.BindValueChanged(s => Size = new Vector2(s.NewValue));
            }
        }
    }
}
