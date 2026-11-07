# SpaceAI 2 — Guide

Space combat AI for Unity 2022.3+: physics flight, dogfighting, formations, guns, turrets, missiles and point
defense, built to run hundreds of ships. Everything lives in the `SpaceAI2` namespace.

## Quick start

1. Open `Scenes/Quickstart` and press Play. Two wings of four fight; the feed at the top left comes from the ships'
   UnityEvents, F3 toggles the AI overlay, Tab switches the camera.
2. To make your own ship, select a model and run **Tools > SpaceAI2 > Create Ship From Model**, or add one component
   by hand:
   - Add `SpaceShip` to an empty GameObject with your model as a child (nose along +Z).
   - Assign a **Ship profile**. With a profile assigned the ship adds and configures the rest itself:
     `Rigidbody`, `ShipMotor`, a collider (if it has none), `ShipHealth`, `ShipGun`, `MissileLauncher`,
     and `AIPilot` + `CombatBrain` or `PlayerPilot`, depending on **Control**.
   - Pick a **Pilot** (skill), **Weapon** and **Missile** profile. Empty weapon or missile = no gun or launcher.
   - Set **Team**. Ships on different teams fight each other.
3. Press Play. The scene services (`SpaceAIWorld`, `ProjectileSystem`, `MissileSystem`) are created on demand; the
   wizard and the scene builders add them up front so you can assign tracer and trail materials.

Setup never removes components: switching Control from AI to Player leaves the AI components for you to delete.
A ship without a profile on `SpaceShip` is configured entirely by hand, as in the test scenes.

## Create Ship From Model

**Tools > SpaceAI2 > Create Ship From Model** takes a model asset or a scene object and builds:

- a root with `SpaceShip` and the chosen profiles, the model as the `Model` child (with optional rotation so the nose
  faces +Z) and `ShipBank`, which leans the model in turns without touching physics;
- a sphere, box or convex mesh collider fitted to the model's renderers (colliders on the model are removed);
- gun muzzles at the nose and missile rails under the hull, as child transforms you can move;
- the scene services if the scene has none, and optionally a prefab.

It warns when the model's size differs a lot from the profile's radius: the AI keeps distance and avoids traffic by
the profile radius, so big ships need their own profile.

## Profiles

All tuning lives in ScriptableObjects (**Create > SpaceAI2 > …**). Examples are in `Profiles/`.

| Profile | What it sets |
|---|---|
| `ShipProfile` | Speeds (min, cruise, max, boost), acceleration, pitch/yaw/roll rates, turn response, grip (how fast velocity follows the nose), hull, shield, radius. |
| `PilotProfile` | Skill: aim error, fire tolerance, spray, turn skill, reaction time, hysteresis and commit time between behaviors, missile warning delay, break timing, aggression, caution, patience, subsystem priority. `PilotAce`, `PilotVeteran`, `PilotRookie` are tuned presets. |
| `WeaponProfile` | Gun or turret weapon: damage, blast radius (flak), fire rate, muzzle speed, range, spread, tracer look, or a hitscan beam. |
| `MissileProfile` | Missiles and torpedoes: guidance (heat, radar, unguided), lock time and cone, salvo, speed, turn, fuel, countermeasure resistance, warhead, and whether point defense can shoot it. |
| `FormationProfile` | Wedge, echelon, diamond or custom slots, spacing, break-up and rejoin rules. |

## How the AI works

- `AIPilot` flies: it steers toward an aim point with lead and lag pursuit, keeps a firing solution, avoids
  obstacles with a forward probe and evasive maneuvers, and keeps clear of friendly traffic.
- `CombatBrain` decides: every tick it scores behaviors — patrol, intercept, pursuit, yo-yo, defend, extend, head-on,
  merge, formation, missile evasion, attack runs on large ships — and switches with hysteresis. Target selection
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
In the `LoadTest200` scene (100 v 100 with 300 asteroids) the AI and projectiles cost about 3 ms per frame in a
release build of the editor scripts.

## Demo scenes

### Combat effects

Effects are plain Particle System prefabs on Unity's built-in **Particles/Standard Unlit** shader; there are
no custom shaders or post effects. They live in `Content/Effects`:

| Prefab | Played for |
|---|---|
| `MuzzleFlash` | Every gun shot, tinted with the weapon's tracer colour |
| `HitHull`, `HitShield` | Damage to a ship, split by `ShipHealth.LastShieldDamage` |
| `HitSurface` | Shots hitting rocks and other colliders |
| `ExplosionShip`, `ExplosionMissile` | Ship deaths, missile and flak detonations, missiles shot down |
| `EngineExhaust` | One per nozzle, looping |

`CombatEffects` (one per scene) holds the prefab references, listens to `ProjectileSystem` and `MissileSystem`,
and plays pooled copies. Effects beyond **Cull Distance** from the camera are skipped (explosions use their own,
longer distance); **Max Per Effect** caps each pool and reuses the oldest copy. World-space particles follow
`SpaceAI.ShiftOrigin`. Call `CombatEffects.Hit` or `CombatEffects.Explosion` to play effects from your own code.

Ships get `ShipEffects` at start when the scene has `CombatEffects` and the **Visual Effects** checkbox on
`SpaceShip` is on. It puts an exhaust at each nozzle under `ShipBank.Visual`, scales it with throttle, boost
and engine power, and plays hits and the death explosion. Add it by hand to set nozzle positions for another model.

Edit the prefabs directly to change the look. **Tools > SpaceAI2 > Build Effect Prefabs** regenerates the muzzle
flash, hits and exhaust (overwriting edits to them); the explosion prefabs are hand-tuned and are never rebuilt.
Scene builders and the Create Ship wizard add `CombatEffects` to any scene with weapons.

Open `EffectsShowcase` or rebuild it with **Tools > SpaceAI2 > Build Effects Showcase Scene**.
Keys **1–6** select engines, shield, laser, missile, explosion and hull impacts. **Space** toggles automatic cycling.
The showcase fires the actual projectile and missile systems at a stationary fighter.

Every scene is built by a menu item under **Tools > SpaceAI2** and can be rebuilt at any time.

| Scene | Shows | Keys |
|---|---|---|
| Quickstart | Ships set up by `SpaceShip` + profiles, UnityEvents, AI overlay | Tab, F3 |
| EffectsShowcase | Engines, reactive shield, laser, missiles, explosions, hull sparks | 1–6, Space |
| FlightTest | Player flight model and HUD | mouse, WASD, Shift boost |
| PursuitTest | Lead/lag pursuit of a drone or the player | Tab, T, R |
| ObstacleTest | Obstacle avoidance through an asteroid field | Tab, R |
| GunneryTest | Aiming and firing solutions per weapon | 1–n, Tab, R |
| Dogfight1v1, Dogfight10v10, Dogfight10v10Asteroids | Dogfighting behaviors and stats | Tab, R |
| FormationTest | Squads, orders, break-up and rejoin | Tab, 1–4, E, R |
| MissileTest | Missile types, lock, evasion, torpedoes | 1–n, Tab, R |
| TurretTest | Capital ship turrets, flak, point defense, subsystems, attack runs | see on-screen help |
| LoadTest200 | 200 ships and the cost overlay | Tab |
| DogfightFar | 10 v 10 fought 50 km from the origin with a floating origin | Tab |

Moving from SpaceAI 1.x? See [Migration.md](Migration.md).
