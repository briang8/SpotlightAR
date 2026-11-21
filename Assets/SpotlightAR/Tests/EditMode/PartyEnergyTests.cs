using NUnit.Framework;
using SpotlightAR.Audio;

namespace SpotlightAR.Tests
{
    /// <summary>
    /// Checks the party mode crowd: it reacts to the room getting louder, settles when the
    /// sound stays the same, and works the same at any volume.
    /// </summary>
    public sealed class PartyEnergyTests
    {
        const float Frame = 1f / 30f;

        static float Run(PartyEnergy party, float decibels, float seconds)
        {
            float energy = 0f;
            for (float t = 0f; t < seconds; t += Frame) energy = party.Process(decibels, Frame);
            return energy;
        }

        [Test]
        public void SteadySound_StaysCalm()
        {
            var party = new PartyEnergy();
            Assert.That(Run(party, -5f, 8f), Is.LessThan(0.05f), "a loud but steady song doesn't max the crowd out");
        }

        [Test]
        public void GettingLouder_ExcitesTheCrowd()
        {
            var party = new PartyEnergy();
            Run(party, -20f, 6f);
            Assert.That(Run(party, -10f, 0.5f), Is.GreaterThan(0.5f));
        }

        [Test]
        public void StayingLoud_SettlesBackDown()
        {
            var party = new PartyEnergy();
            Run(party, -20f, 6f);
            Run(party, -10f, 0.5f);
            Assert.That(Run(party, -10f, 8f), Is.LessThan(0.15f));
        }

        [Test]
        public void QuietPart_DropsQuickly()
        {
            var party = new PartyEnergy();
            Run(party, -20f, 6f);
            Run(party, -10f, 0.5f);
            Assert.That(Run(party, -30f, 1.2f), Is.LessThan(0.1f));
        }

        [Test]
        public void AfterAQuietPart_TheNextBuildUpCountsAgain()
        {
            var party = new PartyEnergy();
            Run(party, -10f, 6f);
            Run(party, -25f, 2f);
            Assert.That(Run(party, -12f, 0.5f), Is.GreaterThan(0.5f));
        }

        [Test]
        public void WorksAtAnyVolume()
        {
            var party = new PartyEnergy();
            Run(party, -45f, 6f);
            Assert.That(Run(party, -35f, 0.5f), Is.GreaterThan(0.5f));
        }

        [Test]
        public void Silence_GivesZero()
        {
            var party = new PartyEnergy();
            Assert.That(Run(party, -80f, 2f), Is.EqualTo(0f));
        }
    }
}
