# SpaceAI 2 — Guide

Space combat AI for Unity 2022.3+: physics flight, dogfighting, formations, guns, turrets, missiles and point
defense, built to run hundreds of ships. Everything lives in the `SpaceAI2` namespace.

## Quick start

1. Open `Scenes/Quickstart` and press Play. A patrol wing and a pirate wing of four fight over a freighter on a trade route, escorted by a patrol cruiser and a fighter (see Factions); the feed at the top left comes from the ships'
   UnityEvents, F3 toggles the AI overlay, Tab switches the camera.
2. Drop the prefabs from `Prefabs` into your own scene: `SpaceAI Systems` once per scene, then `Fighter`
   (blue team), `Fighter Red`, `Player Fighter` or `Cruiser`. Change pilot, weapon, missile and team on the
   instance's `SpaceShip`.
3. To make your own ship, select a model and run **Tools > SpaceAI2 > Create Ship From Model**, or add one component
   by hand:
   - Add `SpaceShip` to an empty GameObject with your model as a child (nose along +Z).
   - Assign a **Ship profile**. With a profile assigned the ship adds and configures the rest itself:
     `Rigidbody`, `ShipMotor`, a collider (if it has none), `ShipHealth`, `ShipGun`, `MissileLauncher`,
     and `AIPilot` + `CombatBrain` or `PlayerPilot`, depending on **Control**.
   - Pick a **Pilot** (skill), **Weapon** and **Missile** profile. Empty weapon or missile = no gun or launcher.
   - Set **Team**. Ships on different teams fight each other.
4. Press Play. The scene services (`SpaceAIWorld`, `ProjectileSystem`, `MissileSystem`) are created on demand; the
   `SpaceAI Systems` prefab and the wizard add them up front with tracer, trail and effect assets assigned.

Setup never removes components: switching Control from AI to Player leaves the AI components for you to delete.
A ship without a profile on `SpaceShip` is configured entirely by hand.

## Create Ship From Model

**Tools > SpaceAI2 > Create Ship From Model** takes a model asset or a scene object and builds:

- a root with `SpaceShip` and the chosen profiles, the model as the `Model` child (with optional rotation so the nose
  faces +Z) and `ShipBank`, which leans the model in turns without touching physics;
- a sphere, box or convex mesh collider fitted to the model's renderers (colliders on the model are removed);
- gun muzzles at the nose and missile rails under the hull, as child transforms you can move;
- the `SpaceAI Systems` prefab if the scene has no services, and optionally a prefab of the ship.

It warns when the model's size differs a lot from the profile's radius: the AI keeps distance and avoids traffic by
the profile radius, so big ships need their own profile.

## Profiles

All tuning lives in ScriptableObjects (**Create > SpaceAI2 > …**). Examples are in `Profiles/`.

| Profile | What it sets |
|---|---|
| `ShipProfile` | Speeds (min, cruise, max, boost), acceleration, pitch/yaw/roll rates, turn response, grip (how fast velocity follows the nose), hull, shield, radius. |
| `PilotProfile` | Skill: aim error, fire tolerance, spray, turn skill, reaction time, hysteresis and commit time between behaviors, missile warning delay, break timing, aggression, caution, patience, subsystem priority. `PilotAce`, `PilotVeteran`, `PilotRookie` are tuned presets. |
| `WeaponProfile` | Gun or turret weapon: damage, blast radius (flak), fire rate, muzzle speed, range, spread, tracer look, or a hitscan beam. |
| `MissileProfile` | Missiles and torpedoes: guidance (heat, radar, unguided), lock time and cone, salvo, speed, turn, fuel, countermeasure resistance, warhead, whether point defense can shoot it, and the model drawn for it (a box without one). |
| `FormationProfile` | Wedge, echelon, diamond or custom slots, spacing, break-up and rejoin rules. |

## How the AI works

- `AIPilot` flies: it steers toward an aim point with lead and lag pursuit, keeps a firing solution, avoids
  obstacles with a forward probe and evasive maneuvers, and keeps clear of friendly traffic.
- `CombatBrain` decides: every tick it scores behaviors — patrol, intercept, pursuit, yo-yo, defend, extend, head-on,
  merge, formation, missile evasion, attack runs on large ships, orders, flee — and switches with hysteresis. Target selection
  weighs distance, angle, threat, damage and how many allies already engage that target.
- `Formation` leads a squad: wingmen hold slots, break off to engage and rejoin. Orders: `HoldFormation()`,
  `Cover()`, `Attack(target)`, `Attack(target, priority)`, `Return()`.
- Large ships carry `ShipTurret`s (guns, flak, point defense against missiles) and `Subsystem`s (engines, weapons,
  shields) that fighters target by the pilot's or the wing's priority.

## Scripting

```csharp
using SpaceAI2;

ship.Team = 1;
brain.ForcedTarget = enemy;              // attack this ship while it lives
brain.ForcedSubsystem = enemy.Subsystems[0];
formation.Attack(capitalShip, TargetPriority.Engines);
launcher.LockTarget = enemy; launcher.Launch();
SpaceShip nearest = SpaceAIWorld.Instance.Registry.FindNearestEnemy(ship, 3000f);
```

### Factions

A `Faction` asset (**Create > SpaceAI2 > Faction**) names a side. Put it in the ship's **Faction** field. Ships of
one faction never fight each other; toward other factions each asset sets a **Default Stance** and per-faction
**Relations** (hostile, neutral or friendly). When two factions list each other differently, the less friendly
stance wins. AI ships attack only hostile ships, guard friendly ones and leave neutrals alone, so a neutral
faction with `CombatBrain` just patrols until something attacks it. Ships without a faction use **Team**: different
teams fight.

**When Attacked** decides what a faction does when a ship that is not its enemy damages one of its ships:
`AttackerShip` makes that one ship an enemy of the whole faction until it is disabled (or `Factions.Forgive`),
`AttackerFaction` turns the faction hostile to the attacker's faction, `Ignore` lets it pass. Friendly fire from
allies is ignored unless **Respond To Friendly Fire** is on.

At runtime relations are a reputation from -100 to 100 per pair of factions: hostile at -30 or less, friendly at 30
or more. Examples are in `Profiles/Factions` (patrol, pirates, traders).

```csharp
Factions.SetStance(pirates, traders, Stance.Neutral);   // a truce
Factions.AddReputation(player, traders, 10f);           // a delivered cargo
Factions.MarkHostile(playerShip, patrol);               // wanted by the patrol
Factions.RelationChanged += (a, b, stance) => Debug.Log($"{a.name} and {b.name} are now {stance}");
bool fight = ship.IsEnemy(other), ally = ship.IsAlly(other);
```

### Orders

What an AI ship does between fights is its order, set in the **Orders** section of its `CombatBrain` or from code.
Patrol is the default: the ship wanders around its patrol area. A **Route** makes it fly a `ShipRoute` (an object
with the **Ship Route** component; its children in order are the waypoints, and one route serves any number of
ships). An **Escort** keeps station behind its charge. Combat interrupts an order, and the ship resumes it after.

**Engagement** decides when the ship fights. `Attack` takes on any enemy (an escort only those near its charge or
attacking it). `Defend` fights only ships that attack it or its charge. `Flee` never fights: it runs at full boost
from enemies hunting it inside the **Flee Range** (or any enemy at close range), weaving under fire, and returns to
its route once clear. `Passive` ignores enemies and keeps to its order, leaving them to its turrets; it suits large
ships. The freighter in Quickstart flies a loop with `Passive`, and the patrol cruiser and one fighter escort it.

```csharp
brain.FollowRoute(tradeRoute);          // from the nearest waypoint
brain.MoveTo(station.transform);        // OrderCompleted fires on arrival, then the ship patrols there
brain.Escort(freighter);
brain.Engagement = Engagement.Defend;
brain.OrderCompleted += (b, order) => b.FollowRoute(homeRoute);
```

A formation follows its leader, so a route or move order on the leader moves the whole squad.

### Events

On `SpaceShip`, as UnityEvents in the inspector (**Events** foldout) and as properties for code:

| UnityEvent | Arguments |
|---|---|
| `OnDestroyed` | the ship |
| `OnDamaged` | the ship, damage |
| `OnTargetAcquired` | the ship, its new target (raised by the AI) |
| `OnMissileLaunched` | the ship, the locked target or null |

C# events for code that needs more detail: `ShipHealth.Damaged/Destroyed` (with `HitInfo`), `SpaceShip.Hit`,
`CombatBrain.BehaviorChanged/TargetChanged`, `AIPilot.ManeuverStarted/Arrived`, `ShipGun.Fired`,
`MissileLauncher.Launched`, `Subsystem.Destroyed`.

A destroyed ship is deactivated, not destroyed: call `Health.Restore()` and `RestoreSubsystems()` and reactivate it
to respawn (see `Demo/ShipEventLog`).

## Debugging

- **Gizmos** (select the object): `SpaceShip` — radius, tightest turn circles at cruise speed, gun range;
  `CombatBrain` — sensor range, patrol area, line to the target; `Formation` — slots and wingmen, broken ones red;
  `ShipTurret` — traverse sector at the elevation limits; `AIPilot` — aim point, obstacle probe, escape direction.
- **`AIDebugOverlay`** (put it on the camera): labels each AI ship with its behavior, avoidance maneuver and target,
  and lists the behavior scores of the ship nearest the screen center or the one you set as **Inspected**. F3 toggles.
- **`PerformanceOverlay`**: the asset's own cost per frame (AI, projectiles, missiles) beside all scripts and physics.
  The Unity profiler shows the same split under `SpaceAI.*` markers.

## Large worlds

Add `FloatingOrigin` to the player ship or the camera rig. Past its threshold (5 km by default) the whole scene,
including trails, particles, projectiles and missiles, shifts back to the origin. Your own systems that store world
positions outside transforms subscribe to `SpaceAI.OriginShifted` and subtract the offset. `SpaceAI.ShiftOrigin`
can also be called directly, e.g. by a networking layer.

## Performance

`SpaceAIWorld` updates all pilots from one `Update`, ships are read through per-frame snapshots, target scoring and
traffic checks are cached and staggered, and projectiles and missiles are plain arrays stepped by one system each.
With 100 v 100 ships, 300 asteroids and half the fighters carrying missiles, the AI costs about 4 ms per frame and
projectiles and missiles under 1 ms together in a release build of the editor scripts. `PerformanceOverlay` on the
Dogfight camera shows the split.

## Combat effects

Effects are plain Particle System prefabs on Unity's built-in **Particles/Standard Unlit** shader, plus one
custom shader for the shield bubble (`Shield.shader`); there are no post effects. They live in `Content/Effects`:

| Prefab | Played for |
|---|---|
| `MuzzleFlash` | Every gun shot, tinted with the weapon's tracer colour |
| `HitHull`, `HitShield` | Damage to a ship, split by `ShipHealth.LastShieldDamage` |
| `HitSurface` | Shots hitting rocks and other colliders |
| `ExplosionShip`, `ExplosionMissile` | Ship deaths, missile and flak detonations; a small pop for missiles shot down, out of fuel or hitting before arming |
| `EngineExhaust` | One per nozzle, looping |
| `MissileFlame` | Each missile and torpedo while its motor burns, scaled by the profile's **Width** and tinted with **Flame Color** |

`CombatEffects` (one per scene) holds the prefab references, listens to `ProjectileSystem` and `MissileSystem`,
and plays pooled copies. Effects beyond **Cull Distance** from the camera are skipped (explosions use their own,
longer distance); **Max Per Effect** caps each pool and reuses the oldest copy. World-space particles follow
`SpaceAI.ShiftOrigin`. Call `CombatEffects.Hit` or `CombatEffects.Explosion` to play effects from your own code.
With a `MissileFlame` prefab set, `CombatEffects` turns off `MissileSystem.DrawFlames`, the built-in flame and
blast spheres. Missile smoke stays a `TrailRenderer`; the `SpaceAI Systems` prefab sets its **Trail Material** to
`MissileTrail.mat`, a soft-edged particle material.

Ships get `ShipEffects` at start when the scene has `CombatEffects` and the **Visual Effects** checkbox on
`SpaceShip` is on. It puts an exhaust at each nozzle under `ShipBank.Visual`, scales it with throttle, boost
and engine power, and plays hits and the death explosion. A shield hit also draws the shield: an ellipsoid of
**Shield Size** around the model where a patch of refracted, team-coloured energy ripples out from each of the last
four hits and fades in about a second. It uses `CombatEffects`' **Shield Material** (`Shield.mat`). Add it by hand to set nozzle positions for another model, and raise **Exhaust Scale** for a large ship.

Edit the prefabs directly to change the look. **Tools > SpaceAI2 > Build Effect Prefabs** regenerates the muzzle
flash, hits, exhaust, missile flame, trail material and the shield material if missing (overwriting edits to them); the explosion prefabs are hand-tuned and are never rebuilt.
`CombatEffects` is part of the `SpaceAI Systems` prefab; the Create Ship wizard adds it to any scene with weapons.

## Prefabs and demo scenes

| Prefab | Contents |
|---|---|
| `SpaceAI Systems` | `SpaceAIWorld`, `ProjectileSystem`, `MissileSystem`, `CombatEffects`, one per scene |
| `Fighter`, `Fighter Red` | AI fighter with a cannon, veteran pilot; the red variant is team 1 with the red livery |
| `Player Fighter` | Fighter flown with `PlayerPilot`, cannon and heat seekers; the model has a cockpit interior and a `Cockpit Eye` point for the camera |
| `Cruiser` | 52 m cruiser with point defense, two flak and a twin main gun turrets, and three engines, each a subsystem |
| `Transport` | 137 m freighter of the traders faction with four point defense turrets and an engine subsystem, `Passive` |

| Scene | Shows | Keys |
|---|---|---|
| Quickstart | Patrol, pirates and traders as three factions, a freighter on a route with two escorts, UnityEvents and faction changes feeding an event log, respawns, AI overlay | Tab, F3 |
| Dogfight | 10 v 10 among 300 asteroids, aces with radar missiles and veterans with heat seekers, stats and cost overlay | Tab, R |
| Dogfight Player | The same fight with you in one of the blue seats, starting in the cockpit | C, R |

`PlayerPilot` flies with a mouse stick (W/S throttle, Shift boost, A/D yaw, Q/E roll, Space recenters, Esc frees
the cursor), fires the guns on the left button and launches a missile on the right. The seeker picks the enemy
closest to the nose within twice the missile's lock angle and locks once it is inside the lock cone for the lock
time; `FlightHud` frames that target, yellow while locking and red when locked, and draws a lead ring ahead of every enemy within 1.5 gun ranges: put it on the crosshair and the guns hit (the ring turns red there; it is dim beyond gun range). `ChaseCamera` switches to the view
from its **Cockpit** transform with C. In the cockpit, `CockpitDisplays` drives the three screens: speed, shield
and hull bars, an attitude horizon, missile and lock state, and a top-down radar with the distance to the target.
The interior is one merged mesh (`Content/Models/Cockpit_Interior_Merged.asset`); only the moving screen parts are
separate objects.

The asteroids use six low-poly rock meshes and three materials from `Content/Asteroids`. The sky is `Content/Skybox/SpaceSky.mat`: a
nebula panorama with stars drawn by its shader (`SpaceAI2/Skybox/Space`), so they stay one pixel sharp at any
resolution; density, brightness, size and rotation are material settings.

Moving from SpaceAI 1.x? See [Migration.md](Migration.md).
