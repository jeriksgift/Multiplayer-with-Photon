# PlayerWithGFX

The repaired humanoid avatar, skin, armour material, rifle assets and animation controller are used by `Assets/Resources/PlayerWithGFX.prefab` only. The source `Player.prefab` and GameManager's spawn selection are unchanged.

The avatar uses corrected pelvis, side, arm, hand and finger mappings. The original colour texture was recovered from the generated FBX. Copied Photon demo humanoid clips have their capture-space translation and yaw removed; physical movement supplies translation and jumping. The controller blends idle, walk and run and switches to an airborne pose. Finger animation and two-hand IK keep the rifle grip.

`PlayerWithGFXPresentation` derives locomotion from movement, follows camera pitch, animates recoil and muzzle flash, and serializes pitch and shot count through the existing PhotonView. Online look, hits and damage use the shared controllers; GFX characters route movement through the jump controller's physics step. Characters without this component retain their existing movement behavior. The local owner sees a separate rifle view; its world body/rifle cast shadows while other players remain visible.

Both blue and red prefabs share `PlayerWithGFXJump`: a 1.8-metre jump setting with 2x rising gravity and 3x falling gravity, a short input buffer and edge grace, and ground checks that reject the player's own collider and steep walls. Walking is 4m/s and sprinting is 8m/s. Rise/apex/fall animation blends follow vertical velocity. The capsule uses a frictionless, non-bouncing material to prevent sticking to container sides. Play-mode route tests reached the 1.58m wood stack from the ground and the 3m container from the wood. The jump code was also tested with a Photon-owned player in a local offline room; no two-client network test was performed.

For direct scene testing outside a Photon room, this component enables prefab-specific movement and firing feedback: WASD, mouse look, Shift to sprint, Space to jump and left mouse to fire. Escape retains the existing cursor toggle. The direct test does not apply Photon damage.

Validation: humanoid avatar, reference checks, scene alignment, and an advancing Play-mode input test for walking, sprinting, jumping, landing, firing, muzzle flash and hand grip. Two connected multiplayer clients have not been tested. Diagnostics and captures are in `AgentScripts/` and `Screenshots/` at the project root. The pre-repair prefab backup is in `AgentScripts/Backups/`.
