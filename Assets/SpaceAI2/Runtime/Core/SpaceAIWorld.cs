using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI2
{
    [DefaultExecutionOrder(-100)]
    public class SpaceAIWorld : MonoBehaviour
    {
        private static SpaceAIWorld instance;

        [SerializeField, Min(10f)] private float registryCellSize = 250f;

        private readonly List<AIPilot> pilots = new List<AIPilot>();
        private ShipRegistry registry;

        public static SpaceAIWorld Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = FindObjectOfType<SpaceAIWorld>();
                if (instance == null) instance = new GameObject(nameof(SpaceAIWorld)).AddComponent<SpaceAIWorld>();
                return instance;
            }
        }

        public static bool TryGet(out SpaceAIWorld world)
        {
            world = instance;
            return world != null;
        }

        public ShipRegistry Registry => registry ??= new ShipRegistry(registryCellSize);

        internal void Add(AIPilot pilot)
        {
            if (!pilots.Contains(pilot)) pilots.Add(pilot);
        }

        internal void Remove(AIPilot pilot) => pilots.Remove(pilot);

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            Registry.Rebuild();
            float dt = Time.deltaTime;
            for (int i = pilots.Count - 1; i >= 0; i--)
                pilots[i].Tick(dt);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;
    }
}
