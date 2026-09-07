using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UtezHorror.Core;

namespace UtezHorror.Tests
{
    public sealed class PowerSystemTests
    {
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [UnityTest]
        public IEnumerator RepairCannotBeImmediatelyFollowedByAnotherOutage()
        {
            host = new GameObject("Power");
            PowerSystem power = host.AddComponent<PowerSystem>();
            yield return null;   // let Start() broadcast the initial state

            Assert.IsTrue(power.IsPowerOn);
            Assert.IsTrue(power.CutPower(), "First outage should be allowed.");
            Assert.IsFalse(power.IsPowerOn);

            power.RestorePower();
            Assert.IsTrue(power.IsPowerOn);

            Assert.IsFalse(power.CanCutPower,
                "The generator must stay up for its cooldown before it can fail again.");
            Assert.IsFalse(power.CutPower(),
                "Cutting power during the cooldown must be refused, not silently applied.");
            Assert.IsTrue(power.IsPowerOn);
        }

        [UnityTest]
        public IEnumerator PowerChangesAreBroadcastOnce()
        {
            int changes = 0;
            void OnPower(bool _) => changes++;

            GameSignals.PowerChanged += OnPower;
            try
            {
                host = new GameObject("Power");
                PowerSystem power = host.AddComponent<PowerSystem>();
                yield return null;

                changes = 0;
                power.CutPower();
                power.CutPower();   // already out: must not re-broadcast

                Assert.AreEqual(1, changes);
            }
            finally
            {
                GameSignals.PowerChanged -= OnPower;
            }
        }
    }
}
