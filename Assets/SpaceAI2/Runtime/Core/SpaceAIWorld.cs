using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace SpaceAI2
{
    [DefaultExecutionOrder(-100)]
    public class SpaceAIWorld : MonoBehaviour
    {
        private static SpaceAIWorld instance;

        [SerializeField, Min(10f)] private float registryCellSize = 250f;
        [Tooltip("Each pilot probes for obstacles once per this many frames; pilots are spread across frames.")]
        [SerializeField, Min(1)] private int sensingInterval = 3;
        [SerializeField] private LayerMask obstacleMask = Physics.DefaultRaycastLayers;

        public LayerMask ObstacleMask => obstacleMask;

        private readonly List<AIPilot> pilots = new List<AIPilot>();
        private readonly ObstacleSensing sensing = new ObstacleSensing();
        private readonly Stopwatch stopwatch = new Stopwatch();
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
        public int PilotCount => pilots.Count;
        public double LastUpdateMs { get; private set; }

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
            sensing.Dispose();
        }

        private void Update()
        {
            stopwatch.Restart();
            Registry.Rebuild();

            sensing.Interval = sensingInterval;
            sensing.Mask = obstacleMask;
            sensing.Run(pilots, Registry, Time.frameCount);

            float dt = Time.deltaTime;
            for (int i = pilots.Count - 1; i >= 0; i--)
                pilots[i].Tick(dt);
            LastUpdateMs = stopwatch.Elapsed.TotalMilliseconds;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;
    }
}
