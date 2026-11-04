using System.Text;
using UnityEngine;

namespace SpaceAI2.Demo
{
    /// <summary>Squads on a route through asteroids, with break-up and rejoin stats and keys for squad orders.</summary>
    public class FormationDemo : MonoBehaviour
    {
        private class Stats
        {
            public int Breaks;
            public int Rejoins;
            public int Late;
            public float Total;
            public float Longest;
        }

        [SerializeField] private Formation[] squads;
        [SerializeField] private GameObject[] enemies;
        [SerializeField] private ChaseCamera chaseCamera;
        [SerializeField] private FlightHud hud;

        private Stats[] stats;
        private int view;
        private float elapsed;
        private GUIStyle style;
        private readonly StringBuilder text = new StringBuilder();

        public void Setup(Formation[] formations, GameObject[] enemyShips, ChaseCamera cameraRig, FlightHud flightHud)
        {
            squads = formations;
            enemies = enemyShips;
            chaseCamera = cameraRig;
            hud = flightHud;
        }

        private void Start()
        {
            stats = new Stats[squads.Length];
            for (int i = 0; i < squads.Length; i++)
            {
                stats[i] = new Stats();
                squads[i].MemberBroke += OnBroke;
                squads[i].MemberRejoined += OnRejoined;
            }
            ShowView(0);
        }

        private void OnDestroy()
        {
            foreach (Formation squad in squads)
            {
                if (squad == null) continue;
                squad.MemberBroke -= OnBroke;
                squad.MemberRejoined -= OnRejoined;
            }
        }

        private void OnBroke(Formation squad, CombatBrain member) => stats[System.Array.IndexOf(squads, squad)].Breaks++;

        private void OnRejoined(Formation squad, CombatBrain member, float seconds)
        {
            Stats s = stats[System.Array.IndexOf(squads, squad)];
            s.Rejoins++;
            s.Total += seconds;
            s.Longest = Mathf.Max(s.Longest, seconds);
            if (seconds > squad.Profile.RejoinTime) s.Late++;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab)) ShowView((view + 1) % squads.Length);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1)) foreach (Formation squad in squads) squad.HoldFormation();
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2)) foreach (Formation squad in squads) squad.Cover();
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3)) foreach (Formation squad in squads) squad.Attack(NearestEnemy(squad));
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4)) foreach (Formation squad in squads) squad.Return();
            if (UnityEngine.Input.GetKeyDown(KeyCode.E)) ToggleEnemies();
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) ResetStats();
        }

        public void ToggleEnemies()
        {
            foreach (GameObject enemy in enemies) enemy.SetActive(!enemy.activeSelf);
        }

        public void ResetStats()
        {
            elapsed = 0f;
            for (int i = 0; i < stats.Length; i++) stats[i] = new Stats();
        }

        private SpaceShip NearestEnemy(Formation squad)
        {
            SpaceShip nearest = null;
            float nearestDistance = float.MaxValue;
            if (squad.Leader == null) return null;
            foreach (GameObject enemy in enemies)
            {
                if (!enemy.activeInHierarchy) continue;
                float distance = (enemy.transform.position - squad.Leader.Position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = enemy.GetComponent<SpaceShip>();
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        private void ShowView(int index)
        {
            view = index;
            SpaceShip leader = squads[view].Leader;
            if (leader == null) return;
            chaseCamera.Target = leader.transform;
            hud.Bind(leader.Motor, null);
        }

        private void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };
            text.Clear();
            text.Append($"Formations · {elapsed:0} s\n");
            for (int i = 0; i < squads.Length; i++)
            {
                Formation squad = squads[i];
                Stats s = stats[i];
                int count = squad.Members.Count;
                float average = s.Rejoins > 0 ? s.Total / s.Rejoins : 0f;
                text.Append($"{(i == view ? "> " : "  ")}{squad.name}: {squad.Profile.Shape} · {squad.Order} · in slot {count - squad.BrokenCount}/{count} · " +
                            $"breaks {s.Breaks} · rejoin avg {average:0.0} s, max {s.Longest:0.0} s (target {squad.Profile.RejoinTime:0} s, late {s.Late})\n");
            }
            text.Append("Tab switch squad · 1 hold · 2 cover · 3 attack · 4 return · E enemies on/off · R reset stats");
            GUI.Label(new Rect(16, Screen.height - 130, 1400, 120), text.ToString(), style);
        }
    }
}
