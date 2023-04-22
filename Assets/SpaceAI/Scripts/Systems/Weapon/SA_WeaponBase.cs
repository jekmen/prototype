using SpaceAI.Core;
using UnityEngine;

namespace SpaceAI.WeaponSystem
{
    public class SA_DamageBase : MonoBehaviour, IDamageSendler
    {
        [SerializeField] protected Rigidbody rb;
        [SerializeField] protected GameObject explousionEffectPrefab;
        [SerializeField] protected AudioSource explosionSound;
        [SerializeField] protected float lifeTimeEffect = 3;
        [SerializeField] protected int damage = 20;

        private GameObject owner;
        private GameObject target;

        public Rigidbody Rb => rb;
        public GameObject Owner => owner;
        public GameObject Target => target;

        public void SetOwner(GameObject owner)
        {
            this.owner = owner;
        }

        public void SetTarget(GameObject target)
        {
            this.target = target;
        }
    }

    public class SA_WeaponBase : MonoBehaviour
    {
        [HideInInspector] public GameObject Owner;
        [HideInInspector] public GameObject Target;
    }
}