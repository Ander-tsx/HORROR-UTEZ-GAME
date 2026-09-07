using NUnit.Framework;
using UnityEngine;
using UtezHorror.Core.Noise;

namespace UtezHorror.Tests
{
    public sealed class NoiseSystemTests
    {
        private sealed class FakeListener : INoiseListener
        {
            public Vector3 HearingPosition { get; set; }
            public float HearingMultiplier { get; set; } = 1f;

            public int HeardCount { get; private set; }
            public float LastLoudness { get; private set; }

            public void OnNoiseHeard(in NoiseEvent noise)
            {
                HeardCount++;
                LastLoudness = noise.Loudness;
            }
        }

        private FakeListener listener;

        [SetUp]
        public void SetUp()
        {
            listener = new FakeListener();
            NoiseSystem.Register(listener);
        }

        [TearDown]
        public void TearDown() => NoiseSystem.Unregister(listener);

        [Test]
        public void ListenerInsideRadiusHearsNoise()
        {
            listener.HearingPosition = new Vector3(5f, 0f, 0f);
            NoiseSystem.Emit(Vector3.zero, 10f, NoiseSource.Footstep);
            Assert.AreEqual(1, listener.HeardCount);
        }

        [Test]
        public void ListenerOutsideRadiusHearsNothing()
        {
            listener.HearingPosition = new Vector3(15f, 0f, 0f);
            NoiseSystem.Emit(Vector3.zero, 10f, NoiseSource.Footstep);
            Assert.AreEqual(0, listener.HeardCount);
        }

        [Test]
        public void LoudnessFallsOffWithDistance()
        {
            listener.HearingPosition = new Vector3(2f, 0f, 0f);
            NoiseSystem.Emit(Vector3.zero, 10f, NoiseSource.Footstep);
            float near = listener.LastLoudness;

            listener.HearingPosition = new Vector3(8f, 0f, 0f);
            NoiseSystem.Emit(Vector3.zero, 10f, NoiseSource.Footstep);
            float far = listener.LastLoudness;

            Assert.Greater(near, far);
            Assert.AreEqual(0.8f, near, 1e-4f);
            Assert.AreEqual(0.2f, far, 1e-4f);
        }

        [Test]
        public void HearingMultiplierExtendsReach()
        {
            listener.HearingPosition = new Vector3(15f, 0f, 0f);

            NoiseSystem.Emit(Vector3.zero, 10f, NoiseSource.Footstep);
            Assert.AreEqual(0, listener.HeardCount, "Should be out of range at multiplier 1.");

            listener.HearingMultiplier = 2f;
            NoiseSystem.Emit(Vector3.zero, 10f, NoiseSource.Footstep);
            Assert.AreEqual(1, listener.HeardCount, "Doubled hearing should reach 15 m.");
        }

        [Test]
        public void UnregisteredListenerStopsReceiving()
        {
            listener.HearingPosition = Vector3.zero;
            NoiseSystem.Unregister(listener);
            NoiseSystem.Emit(Vector3.zero, 10f, NoiseSource.Footstep);
            Assert.AreEqual(0, listener.HeardCount);
        }

        [Test]
        public void RegisteringTwiceDeliversOnce()
        {
            listener.HearingPosition = Vector3.zero;
            NoiseSystem.Register(listener);
            NoiseSystem.Emit(Vector3.zero, 10f, NoiseSource.Footstep);
            Assert.AreEqual(1, listener.HeardCount);
        }
    }
}
