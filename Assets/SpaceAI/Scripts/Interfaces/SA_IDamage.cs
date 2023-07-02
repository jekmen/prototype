namespace SpaceAI.Core
{
    using UnityEngine;

    public interface SA_IDamage
    {
        void ApplyDamage(float damage, GameObject killer);
    }
}