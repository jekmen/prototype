using UnityEngine;

namespace SpaceAI.WeaponSystem
{
    public class SA_DamageBase : MonoBehaviour
    {
        public GameObject Effect;
        public GameObject Owner;
        public float LifeTimeEffect = 3;
        public int Damage = 20;
    }

    public class SA_WeaponBase : MonoBehaviour
    {
        [HideInInspector] public GameObject Owner;
        [HideInInspector] public GameObject Target;

        public Vector3 TorqueSpeedAxis;
        public GameObject TorqueObject;
    }
}