namespace SpaceAI.Core
{
    using UnityEngine;

    public interface SA_IDamageSendler
    {
        GameObject Owner { get; }
        GameObject Target { get; }
    }
}