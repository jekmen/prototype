using SpaceAI.Core;
using UnityEngine;

namespace SpaceAI.WeaponSystem
{
    public class SA_DamageBase : MonoBehaviour, IDamageSendler
    {
        [SerializeField] protected Rigidbody _rb;
        [SerializeField] protected GameObject _explousionEffectPrefab;
        [SerializeField] protected AudioSource _explosionSound;
        [SerializeField] protected float _lifeTimeEffect = 3;
        [SerializeField] protected int _damage = 20;

        private GameObject _owner;
        private GameObject _target;

        public Rigidbody Rb => _rb;
        public GameObject Owner => _owner;
        public GameObject Target => _target;

        public void SetOwner(GameObject owner)
        {
            _owner = owner;
        }

        public void SetTarget(GameObject target)
        {
            _target = target;
        }
    }

    public class SA_WeaponBase : MonoBehaviour
    {
        [HideInInspector] public GameObject Owner;
        [HideInInspector] public GameObject Target;
    }
}