using UnityEngine;

namespace SpaceAI.Core
{
    public interface SA_IDamage
    {
        void ApplyDamage(float damage, GameObject killer);
    }
}
