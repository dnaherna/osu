// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Mania.Tests
{
    public partial class TestSceneManiaTouchLayoutEditor : ScreenTestScene
    {
        private ManiaRulesetConfigManager config = null!;
        private ManiaTouchLayoutEditor editor = null!;

        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("load editor", () =>
            {
                config = new ManiaRulesetConfigManager(null, new ManiaRuleset().RulesetInfo);
                LoadScreen(editor = new ManiaTouchLayoutEditor(config));
            });
            AddUntilStep("editor loaded", () => editor.IsLoaded && editor.ChildrenOfType<CompositeDrawable>().Any(d => d.Name == "Touch zone 1"));
        }

        [Test]
        public void TestTouchDragAndSaveAfterReset()
        {
            AddStep("reset layout", () => button("Reset").TriggerClick());
            AddStep("touch first zone", () => InputManager.BeginTouch(new Touch(TouchSource.Touch1, firstZone().ScreenSpaceDrawQuad.Centre)));
            AddStep("move finger upwards", () =>
            {
                var parent = firstZone().Parent!;
                InputManager.MoveTouchTo(new Touch(TouchSource.Touch1, parent.ToScreenSpace(new Vector2(parent.DrawWidth * 0.125f, parent.DrawHeight * 0.45f))));
            });
            AddStep("release finger", () => InputManager.EndTouch(new Touch(TouchSource.Touch1, firstZone().ScreenSpaceDrawQuad.Centre)));
            AddAssert("zone moved and snapped", () => firstZone().Y, () => Is.EqualTo(0.2f).Within(0.001));
            AddStep("save", () => button("Save").TriggerClick());
            AddAssert("overlay enabled", () => config.Get<bool>(ManiaRulesetSetting.TouchOverlay));
            AddAssert("moved zone persisted", () => ManiaTouchLayout.Get(config.Get<string>(ManiaRulesetSetting.TouchLayouts), 4, editor.DrawHeight > editor.DrawWidth)?[0].Y,
                () => Is.EqualTo(0.2f).Within(0.001));
        }

        [Test]
        public void TestCancelDoesNotEnableOverlay()
        {
            AddStep("cancel", () => button("Cancel").TriggerClick());
            AddAssert("no layout saved", () => config.Get<string>(ManiaRulesetSetting.TouchLayouts), () => Is.Empty);
            AddAssert("overlay still disabled", () => !config.Get<bool>(ManiaRulesetSetting.TouchOverlay));
        }

        private CompositeDrawable firstZone() => editor.ChildrenOfType<CompositeDrawable>().Single(d => d.Name == "Touch zone 1");
        private RoundedButton button(string text) => editor.ChildrenOfType<RoundedButton>().Single(b => b.Text.ToString() == text);
    }
}
