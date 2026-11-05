# Migrating from SpaceAI 1.x

SpaceAI 2 is a rewrite. Its API is not compatible with 1.x: the two can live in one project (different namespaces,
`SpaceAI` vs `SpaceAI2`), so you can move ships over one at a time and delete the old folder at the end.

## What changed and why

| 1.x | 2.0 |
|---|---|
| `SA_ShipController` on the ship, everything else created at runtime by reflection | `SpaceShip` with profile references; it adds real components you can see and tweak |
| Settings in `SA_ShipConfigurationManager` assets, but ships run from XML in `StreamingAssets/Configurations` | ScriptableObject profiles used directly at runtime; no XML, no file copying in builds |
| `GroupType` enum (`Enemy`, `Player`) + `GroupTypesToAction` | `SpaceShip.Team` (int); any other team is hostile |
| `SA_EventsBus` with `SA_ShipRegistryEvent`, `SA_ShipUnRegisterEvent`, target request events | Direct calls and per-ship events: UnityEvents on `SpaceShip`, C# events on the components |
| `SA_Manager` required in the scene: targeting, spawning, death cleanup, bullet pool | `SpaceAIWorld`, `ProjectileSystem`, `MissileSystem`, created on demand; spawning is yours |
| FSM with three states (Idle, Attack, Turn) | Utility AI with eleven scored behaviors, pilot skill profiles, formations |
| Bullets and missiles as pooled GameObjects with Rigidbodies | Projectiles and missiles as data stepped by one system, drawn in batches |
| Destroyed ships are `Destroy`ed | Destroyed ships are deactivated and can be restored |

## Mapping

### Ship

| 1.x | 2.0 |
|---|---|
| `SA_ShipController` / `SA_BaseShip` | `SpaceShip` (+ `ShipMotor`, `AIPilot`, `CombatBrain` added by setup) |
| Ship size from the `MeshFilter` | `ShipProfile.Radius` and the collider (the wizard fits both to the model) |
| Inspector "Validate" (gravity off, drag) | Not needed: `ShipMotor` configures the `Rigidbody` itself |
| `Group` / `GroupType` | `Team` |
| `SetCurrentEnemy(go)`, `CurrentEnemy` | `CombatBrain.ForcedTarget`, `CombatBrain.Target` |
| `SetTarget(Vector3)`, `SetFollowTarget(bool)` | `AIPilot.FlyTo(transform)` / patrol area on `CombatBrain`, or a `Formation` |
| `SA_IDamage.ApplyDamage(damage, killer)` | `ship.ReceiveHit(new HitInfo { Ship = ship, Source = attacker, Damage = …, Point = … })` |

### Configuration (`SA_ShipConfigurationManager`)

| 1.x field | 2.0 |
|---|---|
| `MainConfig.Speed`, `MoveSpeedIncrease` | `ShipProfile.CruiseSpeed`/`MaxSpeed`/`BoostSpeed`, `Acceleration` |
| `MainConfig.RotationSpeed`, `RotationSpeedMain` | `ShipProfile.PitchRate`, `YawRate`, `RollRate` (degrees per second), `TurnResponse` |
| `MainConfig.ShipMass` | Not used: flight is velocity-driven, mass only matters for collisions with other bodies |
| `MainConfig.HP` | `ShipProfile.Hull` |
| `MainConfig.CollisionDamage`, `DurableForce` | Not built in; handle `SpaceShip.Collided` |
| `MainConfig.patrolPoint`, `flyDistance` | `CombatBrain` patrol area (transform) and patrol radius |
| `AIConfig.ShipTargetScanRange` | `CombatBrain` sensor range |
| `AIConfig.TargetRequestFrequency` | `CombatBrain` target interval |
| `ShieldsConfiguration.ShieldPower` | `ShipProfile.Shield`; regeneration delay and rate on `ShipHealth` |
| `ShieldsConfiguration` visuals (`ShieldPrefab`, decay, react speed) | Not built in; drive your shield effect from `OnDamaged` |
| `Options.useTurrets`, `independentTurrets` | Add `ShipTurret`s; each picks its own targets by its role |
| `ShipSystems` (custom `SA_IShipSystem` by reflection) | Your own components next to `SpaceShip`; subscribe to its events |
| `ItemsStaf` (hit sounds, explosion, fire particle) | Your effects on `OnDamaged` / `OnDestroyed` |

The 1.x values do not convert one to one (1.x turned by interpolation, 2.0 by rates in degrees per second), so start
from `Profiles/Fighter` or `Profiles/Cruiser` and tune by flying the ship in `FlightTest`.

### Weapons

| 1.x | 2.0 |
|---|---|
| `SA_WeaponLaunchManager` (Laser) | `ShipGun` + `WeaponProfile`; muzzles are child transforms on `ShipGun.Muzzles` |
| `FireRate`, `Spread`, `bulletSpeed`, `SA_DamageBase.damage` | `WeaponProfile.FireRate`, `Spread`, `MuzzleSpeed`, `Damage` |
| `Ammo`, `ReloadTime`, `InfinityAmmo` | Guns have unlimited ammo; `MissileLauncher.Ammo` (-1 = unlimited) and `MissileProfile.Reload` |
| `SA_WeaponLaunchManager` (Missiles), `SA_Missile`, `SA_MissileTypes` | `MissileLauncher` + `MissileProfile` (`Guidance`: Unguided, Heat, Radar) |
| `SA_DamageSandler.Explosive`, `ExplosionRadius` | `WeaponProfile.BlastRadius`, `MissileProfile.BlastRadius` |
| `SA_Turret` (`turnRate`, traverse and elevation limits) | `ShipTurret.Configure(role, yawHalfAngle, minPitch, maxPitch, rate, acceleration)` |
| `SA_WeaponController.LaunchWeapon` | AI fires itself; for players `ShipGun.Trigger = true`, `MissileLauncher.Launch()` |

### Events

| 1.x | 2.0 |
|---|---|
| `SA_ShipRegistryEvent` | `SpaceShip` OnEnable, or `SpaceAIWorld.Instance.Registry` |
| `SA_ShipUnRegisterEvent` | `SpaceShip.OnDestroyed` / `ShipHealth.Destroyed` |
| `SA_ShipTargetRequesEvent`, `SA_TurretTargetRequestEvent` | Not needed: brains and turrets query the registry; listen to `OnTargetAcquired` |
| `SA_EventsBus.AddEventListener<T>` | Subscribe to the ship's UnityEvents in the inspector or its C# events in code |

### Scene

| 1.x | 2.0 |
|---|---|
| `SA_Manager` (required) | Nothing required; add `SpaceAIWorld`, `ProjectileSystem`, `MissileSystem` to set materials |
| `SA_Manager` spawning (`shipPrefabs`, `shipCount`, `initTime`) | Instantiate prefabs yourself; a ship registers itself when enabled |
| `SA_Camera` | `Demo/ChaseCamera` (minimal) or your own camera |
| `SA_AsteroidsManager` | Any colliders work as obstacles; asteroid scenes are built by the scene builders |

## Steps

1. Import SpaceAI 2 next to 1.x.
2. For each ship model: **Tools > SpaceAI2 > Create Ship From Model**, pick profiles. Copy speeds, hull and shield
   from the old configuration into a new `ShipProfile` using the table above; reuse muzzle positions by moving the
   generated muzzle transforms.
3. Replace `SA_EventsBus` listeners with the ship's events, and `GroupType` checks with `Team`.
4. Replace `SA_Manager` spawning with your spawner; remove `SA_Manager` from scenes that only use SpaceAI 2 ships.
5. Delete `Assets/SpaceAI` and `StreamingAssets/Configurations` once no scene uses them.
