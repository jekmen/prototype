using UnityEngine;

namespace SpaceAI.DataManagment
{
    public class SA_ItemsStaf : ScriptableObject
    {
        public AudioClip[] HitSounds;
        public SA_ShipSystem[] ShipSystems;
        public GameObject ExplousionEffect;
        public ParticleSystem OnFireParticle;
    }
}