using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI.Core
{
    public interface IDamageSendler
    {
        GameObject Owner { get; }
        GameObject Target { get; }
    }
}