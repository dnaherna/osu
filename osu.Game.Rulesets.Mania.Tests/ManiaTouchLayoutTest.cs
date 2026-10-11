// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Rulesets.Mania.Configuration;
using osuTK;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public class ManiaTouchLayoutTest
    {
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(7)]
        [TestCase(18)]
        public void TestRoundTripAndIsolation(int keys)
        {
            var zones = ManiaTouchLayout.CreateDefault(keys);
            zones[0].Move(new Vector2(0.05f, 0.15f), true);
            string json = JsonConvert.SerializeObject(new Dictionary<string, ManiaTouchLayout[]>
            {
                [ManiaTouchLayout.Key(keys, false)] = zones,
            });

            var restored = ManiaTouchLayout.Get(json, keys, false);
            Assert.That(restored, Has.Length.EqualTo(keys));
            Assert.That(restored![0].Position, Is.EqualTo(zones[0].Position));
            Assert.That(ManiaTouchLayout.Get(json, keys, true), Is.Null);
            Assert.That(ManiaTouchLayout.Get(json, keys + 1, false), Is.Null);
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("null")]
        [TestCase("{\"4-landscape\":null}")]
        [TestCase("{\"4-landscape\":[null,null,null,null]}")]
        public void TestInvalidLayoutFallsBack(string json)
        {
            Assert.That(ManiaTouchLayout.Get(json, 4, false), Is.Null);
        }

        [Test]
        public void TestRejectOffscreenLayout()
        {
            var zones = ManiaTouchLayout.CreateDefault(4);
            zones[0].X = 2;
            string json = JsonConvert.SerializeObject(new Dictionary<string, ManiaTouchLayout[]> { ["4-landscape"] = zones });
            Assert.That(ManiaTouchLayout.Get(json, 4, false), Is.Null);
        }

        [Test]
        public void TestSnapAndClamp()
        {
            var zone = new ManiaTouchLayout { Width = 0.2f, Height = 0.25f };
            zone.Move(new Vector2(0.126f, 0.174f), true);
            Assert.That(zone.X, Is.EqualTo(0.15f).Within(0.0001));
            Assert.That(zone.Y, Is.EqualTo(0.15f).Within(0.0001));
            zone.Move(new Vector2(2, -1), true);
            Assert.That(zone.X, Is.EqualTo(0.8f).Within(0.0001));
            Assert.That(zone.Y, Is.Zero);
            Assert.That(zone.IsValid, Is.True);
            zone.Move(new Vector2(0.123f, 0.234f), false);
            Assert.That(zone.X, Is.EqualTo(0.123f));
            Assert.That(zone.Y, Is.EqualTo(0.234f));
        }
    }
}
