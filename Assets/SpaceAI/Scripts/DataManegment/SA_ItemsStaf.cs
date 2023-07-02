namespace SpaceAI.DataManagment
{
    using SpaceAI.WeaponSystem;
    using UnityEngine;

    public class SA_ItemsStaf : ScriptableObject
    {
        public AudioClip[] HitSounds;
        public GameObject ExplousionEffect;
        public ParticleSystem OnFireParticle;
        public GameObject ShieldPrefab;
        public SA_DamageSandler[] ShellPrefab;
    }
}