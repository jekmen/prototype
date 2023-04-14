using UnityEngine;

namespace SpaceAI.DataManagment
{
    public class SA_ItemsStaf : ScriptableObject
    {
        public AudioClip[] HitSounds;
        public GameObject ExplousionEffect;
        public ParticleSystem OnFireParticle;
        public GameObject ShieldPrefab;
    }
}