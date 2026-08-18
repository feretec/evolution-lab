using UnityEngine;

namespace EvolutionLab
{
    public sealed class CreatureBodyPart : MonoBehaviour
    {
        public Creature Owner { get; private set; }

        public int Index { get; private set; }

        public void Configure(Creature owner, int index)
        {
            Owner = owner;
            Index = index;
        }

        private void OnMouseDown()
        {
            if (Owner != null)
            {
                Owner.NotifyClicked();
            }
        }
    }
}
