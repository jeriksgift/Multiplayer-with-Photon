# Activity Based Learning (ABL) — Progress Report

## Team Deathmatch Multiplayer Game (Unity + Photon)

| | |
|---|---|
| **Project** | Multiplayer with Elympics — Team Deathmatch Prototype |
| **Student Name** | _<Your Name>_ |
| **Roll Number / USN** | _<Your Roll Number>_ |
| **Course / Semester** | _<Course & Semester>_ |
| **Faculty Guide** | _<Guide Name>_ |
| **Institution** | _<College Name>_ |
| **Activity Based Learning Session** | _<Session / Academic Year>_ |
| **Engine** | Unity 6000.6.0f1 (Unity 6.6), Universal Render Pipeline (URP) 17.6 |
| **Networking** | Photon Unity Networking (PUN 2.55) |
| **Repository** | `Multiplayer-with-Elympics` |

---

## Abstract

This report documents the six-week Activity Based Learning work carried out on a
multiplayer **Team Deathmatch (TDM)** game built with Unity and Photon. The project
was developed incrementally: starting from a functional lobby where players create or
join rooms, moving to a networked game scene with synchronized player avatars, then
adding shooting and damage, converting combat to Remote Procedure Calls (RPCs) for
network synchronization, adding a team-selection menu with ready-up and host start,
and finally implementing team switching and a team-based spawn system with eight
spawn points.

At the end of Week 6 the project supports the complete core loop: **launch → connect
to Photon → create/join a room → pick a team slot → ready up → host starts match →
players spawn on opposite sides → move, aim, shoot, take damage, die and respawn.**
The report explains what was built each week, how it was implemented in code, the
problems faced, and the scope that remains for a complete shipping-quality TDM game.

---

## 1. Introduction and Objectives

The objective of this ABL activity was to learn multiplayer game development
end-to-end by building a playable team-based shooter rather than by studying
networking theory in isolation. The work was organized around one concrete milestone
per week so that each networking concept could be demonstrated in a running build.

The specific objectives were:

1. Build a lobby UI and implement room creation/joining with Photon.
2. Create a game scene and verify that multiple clients can enter the same networked
   scene and see each other.
3. Implement player movement, shooting, and health/damage.
4. Synchronize combat across the network using Photon RPCs.
5. Design a team-selection and ready-up interface inside the lobby.
6. Implement the actual team-switch mechanism and a team-based spawn system so that
   opposing teams begin on opposite sides of the arena.

---

## 2. Technology Stack

| Layer | Technology | Purpose in the project |
|---|---|---|
| Game Engine | Unity 6000.6.0f1 | Scenes, prefabs, physics, rendering, UI |
| Render Pipeline | Universal Render Pipeline (URP) 17.6 | Lit materials, global volume, quality settings |
| Networking | Photon Unity Networking (PUN 2.55) / Photon Realtime | Rooms, player properties, RPCs, networked instantiation |
| Input | Unity Input System 1.20 | Move, Look, Jump, Sprint, Fire, Escape actions |
| UI | Unity uGUI + TextMeshPro | Lobby, room panel, slot grid, HUD, loading overlay |
| Physics | Unity PhysX (3D) | Character movement, ground check, raycast shooting |
| Version Control | Git | Weekly milestones and rollback points |
| Debugging | Ingame Debug Console | On-screen runtime log during multi-client tests |

**Photon model used:** PUN's **client-hosted (Master Client)** model. One client is
the Master Client and is responsible for starting the match; game state that must be
shared (team, slot, readiness) is stored in each player's **Custom Properties**, and
one-off events (damage, VFX) are delivered through **RPCs**.

---

## 3. System Architecture

### 3.1 Scene flow

| Stage | Scene / System | What happens |
|---|---|---|
| 1 | `Lobby.unity` | Player connects to Photon and enters a nickname and room name |
| 2 | Photon Room (max 8 players) | Player joins or creates a room; the room panel appears |
| 3 | `RoomManager` | Players claim Blue/Red slots and mark themselves ready |
| 4 | `RoomManager` (Master Client) | Host starts the match after team/ready validation |
| 5 | `Game.unity` | `PhotonNetwork.LoadLevel` loads the arena on every client |
| 6 | `GameManager` | Each client spawns its player on its own team's side |

> **Return to lobby:** not implemented yet; it is part of the planned match-end flow.

### 3.2 Runtime object responsibilities

| Script | Responsibility |
|---|---|
| `LobbyManager.cs` | Connects to Photon, joins the lobby, validates nickname, joins/creates a room |
| `RoomManager.cs` | Room-level state: 8 team slots, occupancy, readiness, host-only start |
| `RoomSlot.cs` | Visual state of one slot (empty / blue / red / ready, "You" marker) |
| `DataTypes.cs` | Shared `Team` enum (`Blue`, `Red`) |
| `LoadingManager.cs` | Persistent loading overlay across scene loads (`DontDestroyOnLoad`) |
| `GameManager.cs` | Instantiates the local player and selects a team spawn point |
| `PlayerMovement.cs` | First-person movement, mouse look, jump, ground check, camera cleanup |
| `WeaponSystem.cs` | Hitscan shooting from the camera centre and RPC damage request |
| `HealthComponent.cs` | Health, death, respawn, RPC damage and synchronized blood VFX |
| `PlayerUI.cs` | Shows the local player's nickname and removes other players' HUDs |
| `InputManager.cs` | Wraps the Input System actions and cursor locking |
| `Bullet.cs` | Legacy physics-bullet behaviour; retained but no longer used by the weapon |

### 3.3 Networked player prefab

`Assets/Resources/Player.prefab` is the single networked avatar. It carries:

- `PhotonView` (observed by `PhotonTransformView` and `PhotonRigidbodyView`)
- `Rigidbody` + `CapsuleCollider`
- `PlayerMovement`, `WeaponSystem`, `HealthComponent`, `PlayerUI`, `InputManager`
- A child `Main Camera` (used only by the owning client)
- A child `Canvas` / `PlayerHUD` for nickname and health
- A `DamageVFX` particle system used for the blood effect
- `Gun`, `GunPivot`, `Eyes`, `MuzzlePoint`, `FootPoint` transforms

The prefab lives in `Resources/` so that `PhotonNetwork.Instantiate("Player", …)` can
load it by name on every client.

---

## 4. Week-wise Progress

### Week 1 — Lobby UI and Photon Room Creation/Joining

**Planned task:** Design the lobby UI and implement Photon room creation and joining.

**Work completed**

- Built the lobby screen with:
  - a nickname input field,
  - an optional room-name input field,
  - a **Create / Join** button,
  - an **Exit** button,
  - a reusable loading overlay.
- Integrated Photon: the client connects using the settings asset, joins the Photon
  lobby, and then either joins a named room or joins a random room.
- Implemented the "join or create" flow:
  - Empty room name → `JoinRandomRoom()`.
  - Non-empty room name → `JoinRoom(name)`.
  - If the room does not exist (`OnJoinRoomFailed`), the same client creates it.
  - If no random room exists (`OnJoinRandomFailed`), a new room is created.
- Configured rooms with `MaxPlayers = 8`, matching the eventual 8-slot team layout.
- Added nickname validation and stored the nickname in `PhotonNetwork.NickName`, so it
  is available to every client for the slot list and the in-game HUD.

**Key implementation**

```csharp
PhotonNetwork.ConnectUsingSettings();          // LobbyManager.Awake
PhotonNetwork.JoinLobby();                     // OnConnectedToMaster
PhotonNetwork.JoinRoom(roomName);              // named room
PhotonNetwork.JoinRandomRoom();                // quick match
PhotonNetwork.CreateRoom(roomName, new RoomOptions { MaxPlayers = 8 });
```

**Outcome:** A player can open the game, enter a nickname, create a named room, and a
second client can join the same room. The lobby panel is hidden and the room panel is
shown on `OnJoinedRoom`.

**Evidence:** commit `b6965af` — *Completed Lobby page*.

---

### Week 2 — Game Scene and Multi-client Verification

**Planned task:** Add a basic game scene and test by connecting three players to the
same room and scene.

**Work completed**

- Created `Game.unity` with a ground plane, lighting, an arena boundary, and the
  `GameManager` object.
- Added a `Player` prefab in `Resources/` and spawned it with
  `PhotonNetwork.Instantiate`, so every client receives an avatar for every player.
- Enabled `PhotonNetwork.AutomaticallySyncScene` so that when the Master Client loads
  the game scene, all other clients follow automatically instead of remaining in the
  lobby.
- Added transform and rigidbody synchronization through `PhotonTransformView`
  (position + rotation) and `PhotonRigidbodyView` (velocity), giving smooth remote
  movement instead of teleporting.
- Prevented the "multiple first-person cameras / HUDs" problem: each client removes
  cameras and canvases that belong to other players, and only the local player keeps
  its camera and HUD.
- Verified the setup by launching three clients, joining the same room, and confirming
  that all three appeared in the same scene and could see each other move.

**Key implementation**

```csharp
PhotonNetwork.AutomaticallySyncScene = true;
PhotonNetwork.Instantiate(playerPrefab.name, spawn.position, spawn.rotation);
```

**Outcome:** Three clients successfully shared one room and one scene with
synchronized player avatars. This validated the networking foundation before combat
was added.

**Evidence:** commit `3274a8e` — *Completed Player Instantiation and synchronization*.

---

### Week 3 — Basic Shooting and Damage

**Planned task:** Complete the basic shooting functionality and player damage.

**Work completed**

- Implemented a first-person movement controller with walk/run speed, mouse look,
  jump, ground raycast, and cursor lock/unlock.
- Added a hitscan weapon: on Fire, a ray is cast from the centre of the screen
  (`Camera.main.ScreenPointToRay`) up to 100 m, and the first collider hit is resolved.
- Added `HealthComponent` with `maxHealth = 100` and `bulletDamage = 20`, so a player
  is eliminated in five hits.
- Added death handling: on zero health the player's renderers, colliders, movement,
  weapon, and gravity are disabled, and the player respawns after two seconds.
- Added a HUD health bar that listens to the `HealthChanged` event.
- Clamped the vertical camera angle so the view cannot flip upside down.
- Fixed a nickname validation bug caused by the input field returning a one-character
  string even when empty.

**Key implementation (initial, local version)**

```csharp
if (Physics.Raycast(ray, out hit, 100f))
{
    if (hit.collider.TryGetComponent<HealthComponent>(out var health))
        health.Damage(bulletDamage);       // direct local call — later replaced
}
```

**Outcome:** Shooting worked and reduced the target's health correctly, but damage was
only applied on the shooter's machine. Other clients did not see the same health state,
which became the motivation for Week 4.

**Evidence:** commits `c4ed638` (weapon system), `3e8a281` (health, damage, death and
respawn), `9962bad` (camera clamp), `d523d56` (nickname validation).

---

### Week 4 — Network Synchronization of Shooting and Damage using RPCs

**Planned task:** Modify the shooting and damage code so that it is synchronized over
the network using RPCs.

**Work completed**

- Replaced the direct `healthComponent.Damage(...)` call with a Photon RPC addressed
  to the owner of the damaged player's `PhotonView`:

  ```csharp
  var target = healthComponent.GetComponent<PhotonView>();
  target.RPC(nameof(HealthComponent.Damage), target.Owner,
             bulletDamage, hit.Value.point, hit.Value.normal);
  ```

- Marked `HealthComponent.Damage` with `[PunRPC]` so that the call executes on the
  target client with the correct damage value and hit information.
- Made the **blood particle effect** a second RPC broadcast to **all** clients:

  ```csharp
  [PunRPC]
  private void PlayBloodEffectRpc(Vector3 hitPoint, Vector3 hitNormal)
  {
      damageParticleSystem.transform.position = hitPoint;
      damageParticleSystem.transform.rotation = Quaternion.LookRotation(hitNormal);
      damageParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      damageParticleSystem.Play();
  }
  ```

  `Damage` invokes it with `RpcTarget.All`, so every player sees the impact at the same
  world position, not just the shooter and the victim.

- Routed death and respawn visuals through `EnableOrDisableGraphics`, also an RPC sent
  to all clients, keeping the dead player hidden and non-collidable for everyone.
- Removed the old local damage path (left commented in the source as a reference to the
  pre-RPC implementation), making the RPC the single source of truth for damage.

**Outcome:** A shot fired by one client now visibly damages the target on the target's
client, and the blood effect appears for all clients. This was the first genuinely
network-synchronized gameplay event in the project.

**Evidence:** commit `25fae22` — *Made blood particle system and synchronized it using rpc*.

---

### Week 5 — Team Switching Menu in the Player Lobby

**Planned task:** Add a team-switching menu to the player lobby.

**Work completed**

- Introduced the `DataTypes.Team` enum (`Blue`, `Red`).
- Rebuilt the room panel as an 8-slot grid (4 Blue, 4 Red) using a reusable `Slot`
  prefab and a responsive `GridLayoutGroup` cell-size script.
- Each slot displays the occupant's nickname, a colour for its team, a green state
  when the player is ready, and a `(You)` marker for the local player.
- Implemented slot selection:
  - Clicking an empty slot claims it for the local player.
  - Clicking your own slot releases it and deselects your team.
  - Clicking a slot already taken by another player does nothing.
- Added a **Ready / Cancel Ready** button that is enabled only after choosing a slot,
  and a **Start** button that is visible only to the Master Client.
- Added a readiness/team validation rule for the host: the match can start only when
  the minimum number of slotted players is reached, **all slotted players are ready**,
  and **both Blue and Red have at least one player**.
- Upgraded the project from Unity 6.5 to Unity 6.6 during this milestone and re-tested
  the lobby flow.

**Key implementation**

```csharp
public const string KeyTeam      = "team";
public const string KeySlot      = "slot";
public const string KeyTeamIndex = "teamIndex";
public const string KeyReady     = "ready";

PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable {
    { KeyTeam, (int)team }, { KeySlot, slotIndex },
    { KeyTeamIndex, teamIndex }, { KeyReady, ready }
});
```

The lobby refreshes itself on `OnJoinedRoom`, `OnPlayerEnteredRoom`,
`OnPlayerLeftRoom`, and `OnPlayerPropertiesUpdate`, so all clients see the same slot
state without a dedicated state-sync message.

**Outcome:** Players can choose a side, see who else is on each team, ready up, and the
host can begin the match only under valid team conditions.

**Evidence:** commit `e6e59a3` (team UI, `RoomManager`, `RoomSlot`, `Slot` prefab,
`Lobby.unity`) and `dbb760e` (quality settings).

---

### Week 6 — Team Switch Mechanism and Team-based Spawn Points

**Planned task:** Implement the team-switch mechanism and add more spawn points so
that players spawn opposite to each other by team.

**Work completed**

- Finalized the team-switch mechanism as a **two-way slot system**:
  - A player switching from Blue to Red simply clicks a free Red slot; the custom
    properties are rewritten atomically.
  - A player can return to "no team" by clicking their own slot again.
  - The current owner of a slot is resolved deterministically: players are sorted by
    actor number, and the first player claiming a slot keeps it. If two clients claim
    the same slot in the same frame, the loser is automatically deselected on refresh.
- Stored `teamIndex` (the player's position within their team) so that a team's first
  player takes spawn 0, the second takes spawn 1, and so on.
- Expanded the game scene from a single spawn point to **eight spawn points**, four per
  team:

  | Team | Spawn points (x, z) | Facing |
  |---|---|---|
  | Blue | `(-24, -12)`, `(-24, -4)`, `(-24, 4)`, `(-24, 12)` | +X (towards Red) |
  | Red | `(24, -12)`, `(24, -4)`, `(24, 4)`, `(24, 12)` | −X (towards Blue) |

  The two lines are 48 units apart and staggered along Z, so teammates do not overlap
  and both teams start facing the enemy side.
- Rewrote `GameManager` to hold two separate spawn arrays (`blueSpawnPoints`,
  `redSpawnPoints`), read the local player's team and team index, and reserve a spawn:
  - Prefer the spawn matching the player's `teamIndex`.
  - If that one is already reserved, fall back to the first free spawn on the same side.
  - If no spawn is free, fall back to the origin with a warning instead of failing.
- Made respawn reuse the same team-aware spawn lookup, so a dead player returns to
  their own side rather than the centre of the map.
- Re-tested with multiple clients to confirm that Blue players appear on the left line
  and Red players on the right line, and that switching teams before the match changes
  the spawn side.

**Key implementation**

```csharp
// GameManager.Start
if (TryGetSpawnLoc(RoomManager.GetLocalTeam(),
                   RoomManager.GetLocalTeamIndex(), out Transform spawn))
{
    PhotonNetwork.Instantiate(playerPrefab.name, spawn.position, spawn.rotation);
}

// RoomManager — team index is derived from the slot's position in its team
private int GetTeamIndexForSlot(int slotIndex)
{
    DataTypes.Team team = slots[slotIndex].Team;
    int index = 0;
    for (int i = 0; i < slotIndex; i++)
        if (slots[i].Team == team) index++;
    return index;
}
```

**Outcome:** Players can switch teams from the lobby, the slot grid stays consistent
across all clients, and each team begins on its own side of the arena with enough
spawn points for a full 4v4 match.

**Evidence:** commit `6a64197` — *Added team switching mechanism and added more spawn
points in the game scene*.

---

## 5. Current Feature Status

| Feature | Status | Notes |
|---|---|---|
| Photon connection and lobby | ✅ Complete | Connect, join lobby, create/join room |
| Nickname and room name | ✅ Complete | Validated, stored in `PhotonNetwork.NickName` |
| Room capacity | ✅ Complete | 8 players |
| Networked player spawn | ✅ Complete | `PhotonNetwork.Instantiate` from `Resources/Player` |
| Movement and camera synchronization | ✅ Complete | `PhotonTransformView` + `PhotonRigidbodyView` |
| First-person movement, look, jump, sprint | ✅ Complete | Input System actions |
| Hitscan shooting | ✅ Complete | 100 m ray from screen centre |
| Health, death, respawn | ✅ Complete | 100 HP, 20 damage per hit, 2 s respawn |
| Networked damage and blood VFX | ✅ Complete | RPC to owner + `RpcTarget.All` VFX |
| Team selection UI | ✅ Complete | 4 Blue + 4 Red slots |
| Ready-up and host start | ✅ Complete | Both teams required, all slotted players ready |
| Team switching | ✅ Complete | Click a free slot on the other team |
| Team-based spawn points | ✅ Complete | 4 per team, opposite sides, index-based |
| Score, kills, match timer, win condition | ❌ Not implemented | Planned next |
| Kill feed / scoreboard | ❌ Not implemented | Planned next |
| Weapon variety, reload, ammo | ❌ Not implemented | Single hitscan weapon |
| Player animations and audio | ❌ Not implemented | Placeholder capsule visuals |
| Server-authoritative validation | ❌ Not implemented | Client-hosted PUN model |
| Reconnection / host migration | ❌ Not implemented | PUN default behaviour only |

---

## 6. Testing and Validation

Testing was performed manually with multiple Unity Editor/standalone clients on the
same machine and over the local network.

| Test | Method | Result |
|---|---|---|
| Lobby connection | Start one client, verify `OnConnectedToMaster` and `OnJoinedLobby` | Pass |
| Create room | Enter a unique room name and press Create/Join | Room created with 8 max players |
| Join existing room | Second client enters the same room name | Both clients in the same room |
| Random matchmaking | Leave room name empty | Client joins or creates a random room |
| Three-client scene sync | Three clients join the same room, host starts the game | All three appeared in `Game.unity` and saw each other move |
| Shooting and damage | Fire five shots at another player | Target's health reached zero and the death state triggered |
| RPC damage | Shoot from one client while observing the target client | Damage applied on the target; blood VFX shown on all clients |
| Team selection | Click Blue and Red slots on multiple clients | Slots stayed consistent; occupied slots were not stealable |
| Ready validation | Try to start with one team only / unready player | Start button remained disabled |
| Team switching | Claim a Blue slot, then a free Red slot | Player moved to Red and kept the correct slot |
| Spawn sides | Start a match with players on both teams | Blue spawned at x = −24, Red at x = +24, facing each other |
| Respawn | Eliminate a player and wait 2 s | Player respawned with full health on their own team's side |

**Observed edge cases handled during testing**

- Clients remaining in the lobby because `AutomaticallySyncScene` was false on one
  client — fixed by enabling it on every client before connecting.
- Multiple cameras/HUDs rendering after remote players spawned — fixed with per-owner
  camera and canvas cleanup, including a periodic sweep for late-arriving prefabs.
- Two players selecting the same slot — resolved by deterministic slot ownership based
  on actor number, with the losing client automatically deselected.
- Invalid nickname accepted because the input field reported length 1 when empty —
  fixed by trimming and checking for a meaningful length.

---

## 7. Challenges and Solutions

| Challenge | Cause | Solution |
|---|---|---|
| Remote clients stayed in the lobby | `AutomaticallySyncScene` was not enabled on every client | Enabled it in `LobbyManager.Awake` and again before `LoadLevel` |
| Damage only affected the shooter's view | Health was modified through a direct method call | Converted damage to a `[PunRPC]` call routed to the target's owner |
| Blood effect appeared in the wrong place / only locally | VFX position was set only on the shooter | Broadcast `PlayBloodEffectRpc` with `RpcTarget.All` and the exact hit point/normal |
| Multiple first-person cameras and HUDs | Every instantiated player prefab contains a camera and canvas | Destroy components belonging to other `PhotonView` owners; re-sweep for late spawns |
| Two players claiming the same team slot | Concurrent custom-property writes | Sort players by actor number and treat the first claim as authoritative, then refresh |
| Players from both teams spawning on top of each other | Single/shared spawn logic | Separate Blue/Red spawn arrays, team index preference, and a "taken" check |
| Host could start an invalid match | No validation on the Start button | Require minimum players, all slotted players ready, and at least one player per team |
| Input field reported an empty nickname as valid | TMP input field length behaviour | Trim the string and reject length ≤ 1 before connecting |

---

## 8. Learning Outcomes

1. **Photon fundamentals** — connecting, lobbies, rooms, the Master Client, and the
   difference between `JoinRoom`, `JoinRandomRoom`, and `CreateRoom`.
2. **Player state vs. scene state** — using `CustomProperties` for persistent shared
   state (team, slot, readiness) and RPCs for instantaneous events (damage, VFX).
3. **Networked instantiation** — how `PhotonNetwork.Instantiate` assigns ownership and
   why the prefab must be in a `Resources` folder.
4. **Ownership-aware code** — every gameplay script checks `photonView.IsMine` before
   reading input or applying local movement.
5. **Synchronization components** — when to use `PhotonTransformView` and
   `PhotonRigidbodyView`, and why smoothing matters for remote players.
6. **Scene synchronization** — `AutomaticallySyncScene` is a per-client setting, and a
   single client with it disabled breaks the whole match start.
7. **UI state management** — keeping an 8-slot team grid consistent across clients by
   rebuilding it from authoritative player properties on every change event.
8. **Iterative refactoring** — replacing a working local damage path with an RPC-based
   one, and keeping the old path commented for comparison.
9. **Debugging distributed systems** — reproducing bugs with multiple clients and
   using in-game logs to identify which client owned which piece of state.

---

## 9. Limitations and Future Scope

**Current limitations**

- Combat is **client-authoritative**: the victim's client applies the damage it
  receives. A modified client could therefore misreport health or damage.
- Health is not stored in player custom properties, so other clients do not know a
  remote player's exact HP; they only see the synchronized death state and VFX.
- Respawn sets `transform.position` directly while a `Rigidbody` is attached; a
  physics-aware teleport (`Rigidbody.position` / `MovePosition`) would be safer.
- `Bullet.cs` is a leftover from the earlier physics-projectile approach and is no
  longer used by `WeaponSystem`.
- The camera/HUD cleanup uses `FindGameObjectsWithTag` and a periodic sweep, which is
  functional but not the most efficient approach.
- There is no score, kill count, match timer, win condition, or return-to-lobby flow.
- The host cannot hand over or recover cleanly if the Master Client disconnects.

**Planned next steps**

1. Add a match manager with team scores, kill/death tracking, a match timer, and a win
   screen.
2. Add a scoreboard and kill feed driven by RPCs or room properties.
3. Move health and score to room/player custom properties or a server-authoritative
   model (Photon Quantum/Fusion or a dedicated server) to prevent cheating.
4. Add weapon variety (fire rate, spread, reload, ammo) and shooting audio.
5. Add character animations and team-coloured materials.
6. Add a proper return-to-lobby/rematch flow and host migration handling.
7. Improve performance by creating the local camera/HUD only for the owning player
   instead of destroying remote copies.

---

## 10. Conclusion

The six-week activity produced a working team deathmatch prototype that covers the
full path from menu to combat. Week 1 established the lobby and Photon room flow;
Week 2 proved that multiple clients can share a synchronized scene; Week 3 delivered
movement, shooting, health and respawn; Week 4 converted combat into network events
using RPCs; Week 5 built the team-selection and ready-up experience; and Week 6
completed the team-switch mechanism and the team-based spawn layout.

The most valuable outcome is not only the playable build, but the understanding of
*where* multiplayer state should live: persistent shared state in player/room
properties, one-off events in RPCs, and ownership checks around every piece of local
input. The project provides a solid base on which scoring, match rules, and stronger
authority can be added in the next phase.

---

## Appendix A — Source Map

| Path | Description |
|---|---|
| `Assets/Scenes/Lobby.unity` | Menu, room panel, 8-slot team grid, loading overlay |
| `Assets/Scenes/Game.unity` | TDM arena with 8 spawn points and `GameManager` |
| `Assets/Resources/Player.prefab` | Networked player avatar and HUD |
| `Assets/Prefabs/Slot.prefab` | Reusable team-slot UI element |
| `Assets/Scripts/LobbyManager.cs` | Photon connection and room join/create |
| `Assets/Scripts/RoomManager.cs` | Slots, team assignment, readiness, host start |
| `Assets/Scripts/RoomSlot.cs` | Slot visuals and state |
| `Assets/Scripts/GameManager.cs` | Spawn selection and player instantiation |
| `Assets/Scripts/PlayerMovement.cs` | First-person movement and camera handling |
| `Assets/Scripts/WeaponSystem.cs` | Hitscan shooting and RPC damage request |
| `Assets/Scripts/HealthComponent.cs` | Health, death, respawn, blood VFX RPC |
| `Assets/Scripts/PlayerUI.cs` | Local nickname HUD and remote canvas cleanup |
| `Assets/Scripts/InputManager.cs` | Input System wrapper and cursor lock |
| `Assets/Scripts/DataTypes.cs` | Team enum |
| `Assets/Scripts/LoadingManager.cs` | Persistent loading screen |
| `Assets/Scripts/Bullet.cs` | Legacy projectile behaviour (unused) |

## Appendix B — Photon Room/Player Properties

| Key | Type | Meaning |
|---|---|---|
| `team` | `int` (`0` = Blue, `1` = Red) | The team the player selected |
| `slot` | `int` | Index of the occupied slot in the 8-slot grid, `-1` = none |
| `teamIndex` | `int` | Position within the team, used to choose a spawn point |
| `ready` | `bool` | Whether the player has pressed Ready |

## Appendix C — Development Log (Git Milestones)

| Commit | Date | Milestone |
|---|---|---|
| `10489c9` | 2026-07-17 | Initial project setup |
| `b6965af` | 2026-07-20 | Lobby UI and Photon room flow (Week 1) |
| `3274a8e` | 2026-07-25 | Player instantiation and synchronization (Week 2) |
| `c4ed638` | 2026-07-25 | Basic weapon system (Week 3) |
| `d523d56` | 2026-07-25 | Nickname validation fix (Week 3) |
| `9962bad` | 2026-07-25 | Camera vertical rotation clamp (Week 3) |
| `3e8a281` | 2026-07-26 | Health, damage, death and respawn (Week 3) |
| `25fae22` | 2026-08-14 | RPC damage and synchronized blood VFX (Week 4) |
| `e6e59a3` | 2026-09-03 | Team selection UI and Unity 6.6 upgrade (Week 5) |
| `dbb760e` | 2026-09-03 | Quality settings update |
| `6a64197` | 2026-10-09 | Team switching and 8 team-based spawn points (Week 6) |

---

*End of report.*
