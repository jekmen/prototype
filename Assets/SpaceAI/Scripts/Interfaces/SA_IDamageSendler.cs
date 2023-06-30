using UnityEngine;

namespace SpaceAI.Core
{
    public interface SA_IDamageSendler
    {
        GameObject Owner { get; }
        GameObject Target { get; }
    }
}