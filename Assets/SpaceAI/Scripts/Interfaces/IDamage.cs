using UnityEngine;

namespace SpaceAI.Core
{
    public interface IDamage
    {
        void ApplyDamage(float damage, GameObject killer);

        void Dead();

        bool IsDead();
    }
}
