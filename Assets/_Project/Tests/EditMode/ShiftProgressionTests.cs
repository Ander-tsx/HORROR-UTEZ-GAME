using NUnit.Framework;
using UnityEngine;
using UtezHorror.Core;
using UtezHorror.Enemies;

namespace UtezHorror.Tests
{
    public sealed class ShiftProgressionTests
    {
        private ShiftConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<ShiftConfig>();
            config.fadingStart = 0.15f;
            config.darkStart = 0.40f;
            config.curfewStart = 0.75f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(config);

        [TestCase(0.00f, ShiftPhase.Arrival)]
        [TestCase(0.14f, ShiftPhase.Arrival)]
        [TestCase(0.15f, ShiftPhase.Fading)]
        [TestCase(0.39f, ShiftPhase.Fading)]
        [TestCase(0.40f, ShiftPhase.Dark)]
        [TestCase(0.74f, ShiftPhase.Dark)]
        [TestCase(0.75f, ShiftPhase.Curfew)]
        [TestCase(1.00f, ShiftPhase.Curfew)]
        public void PhaseAtReturnsExpectedPhase(float progress, ShiftPhase expected)
        {
            Assert.AreEqual(expected, config.PhaseAt(progress));
        }

        [Test]
        public void EnemyIsNotEligibleBeforeItsEarliestPhase()
        {
            var enemy = ScriptableObject.CreateInstance<EnemyConfig>();
            enemy.earliestPhase = ShiftPhase.Dark;

            Assert.IsFalse(enemy.IsEligible(ShiftPhase.Fading, powerOn: true));
            Assert.IsTrue(enemy.IsEligible(ShiftPhase.Dark, powerOn: true));
            Assert.IsTrue(enemy.IsEligible(ShiftPhase.Curfew, powerOn: true));

            Object.DestroyImmediate(enemy);
        }

        [Test]
        public void BlackoutOnlyEnemyStaysAwayWhilePowerIsOn()
        {
            var enemy = ScriptableObject.CreateInstance<EnemyConfig>();
            enemy.earliestPhase = ShiftPhase.Arrival;
            enemy.requiresBlackout = true;

            Assert.IsFalse(enemy.IsEligible(ShiftPhase.Curfew, powerOn: true));
            Assert.IsTrue(enemy.IsEligible(ShiftPhase.Curfew, powerOn: false));

            Object.DestroyImmediate(enemy);
        }
    }
}
