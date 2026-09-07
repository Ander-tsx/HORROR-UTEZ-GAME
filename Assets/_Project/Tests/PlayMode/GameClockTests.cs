using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UtezHorror.Core;

namespace UtezHorror.Tests
{
    public sealed class GameClockTests
    {
        private GameObject host;
        private ShiftConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<ShiftConfig>();
            config.shiftDurationSeconds = 3f;   // 750 ms per phase: short, but wide enough not to skip one on a hitch
            config.fadingStart = 0.25f;
            config.darkStart = 0.5f;
            config.curfewStart = 0.75f;

            host = new GameObject("ClockHost");
            host.SetActive(false);              // configure before Awake/Start run
            GameClock clock = host.AddComponent<GameClock>();
            clock.Configure(config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator ClockWalksEveryPhaseAndEndsTheRun()
        {
            var phases = new List<ShiftPhase>();
            RunOutcome? outcome = null;

            void OnPhase(ShiftPhase p) => phases.Add(p);
            void OnEnded(RunOutcome o) => outcome = o;

            GameSignals.PhaseChanged += OnPhase;
            GameSignals.RunEnded += OnEnded;
            try
            {
                host.SetActive(true);

                float timeout = Time.time + 15f;
                while (outcome == null && Time.time < timeout) yield return null;
            }
            finally
            {
                GameSignals.PhaseChanged -= OnPhase;
                GameSignals.RunEnded -= OnEnded;
            }

            Assert.AreEqual(RunOutcome.TimeExpired, outcome,
                "A shift that runs out of time must end the run.");
            CollectionAssert.AreEqual(
                new[] { ShiftPhase.Arrival, ShiftPhase.Fading, ShiftPhase.Dark, ShiftPhase.Curfew },
                phases,
                "Phases must be reported once each, in order.");
        }

        [UnityTest]
        public IEnumerator PausedClockDoesNotAdvance()
        {
            host.SetActive(true);
            GameClock clock = host.GetComponent<GameClock>();
            clock.SetPaused(true);

            yield return new WaitForSeconds(0.3f);

            Assert.AreEqual(0f, clock.Normalised, 1e-3f, "A paused clock must not advance.");
            Assert.IsFalse(clock.IsRunning);
        }
    }
}
