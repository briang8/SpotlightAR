using System.Collections.Generic;
using UnityEngine;

namespace SpotlightAR.Stages
{
    /// <summary>
    /// Drives every crowd member from two inputs: the live energy of the show (0..1)
    /// and the hype multiplier (1 to 3), which unlocks bigger reactions.
    /// </summary>
    public sealed class CrowdController : MonoBehaviour
    {
        [Tooltip("Leave empty to use every CrowdMember under this object.")]
        [SerializeField] List<CrowdMember> members = new List<CrowdMember>();

        int hype = 1;
        float energy;

        public int Hype => hype;

        void Awake()
        {
            if (members.Count == 0) GetComponentsInChildren(true, members);
        }

        /// <summary>Set the hype multiplier (1 to 3). Higher hype gives bigger movements.</summary>
        public void SetHype(int value)
        {
            int clamped = Mathf.Clamp(value, 1, 3);
            if (clamped > hype) Cheer();
            hype = clamped;
        }

        /// <summary>Live show energy, 0..1, usually the smoothed voice level.</summary>
        public void SetEnergy(float value)
        {
            energy = Mathf.Clamp01(value);
            // Hype boosts how strongly the crowd reacts to the same energy.
            float boosted = Mathf.Clamp01(energy * (0.8f + 0.2f * hype));
            foreach (CrowdMember member in members) member.SetEnergy(boosted);
        }

        public void Cheer()
        {
            foreach (CrowdMember member in members) member.Cheer();
        }
    }
}
