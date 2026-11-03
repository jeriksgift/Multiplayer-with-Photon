# Toys Death Match interface

Adapted all five supplied references: multiplayer lobby, team selection, loading, game HUD, and exit confirmation. The current theme uses dark graphite panels, off-white text, amber actions, and blue/red team information. Menu and loading backdrops are dimmed; arena lighting during gameplay is unchanged. The Unity product name is Toys Death Match.

The dark-theme update was visually reviewed in Play mode across the lobby, team selection, loading screen, HUD, exit confirmation and results. Room validation, ready/deploy controls, live HUD values and exit/cancel interactions were rechecked locally. Current previews use the Assets/Screenshots/Toys-Dark-*.png filenames.

Display typography is Barlow Condensed SemiBold; body text is Barlow; data labels use Share Tech Mono. The font sources and OFL licenses are in Assets/UI/ToysDeathMatch/Fonts.

## Validation

- Unity compilation completed without errors.
- Required names verified on separate Create Room and Join Room actions, including whitespace and invisible-character input.
- Named room creation, slot selection, ready state, live team counts, host deployment availability, and leaving a room verified in a Photon offline room.
- Live name, numeric health, health bar, team scores, timer, and target score verified in Play mode.
- Escape opens confirmation and releases the cursor; movement and firing are blocked while the dialog is open. Frames continue advancing. Cancel restores controls; confirming match exit returns to Lobby.
- Menu and team selection visually inspected at 1920x1080 and 1440x1080. Visible controls remain within the viewport.
- Nested slot typography and backgrounds checked after closing and reopening the scene; overrides persist.
- Existing red/blue player checks passed again: walk, sprint, jump, fire, death, owner respawn, remote respawn, and rejection of snapshots from an earlier life.
- Online end-to-end play was not verified; the Photon service produced intermittent connection timeouts during the session. Interaction tests used offline mode.

## Previews

- Assets/Screenshots/Toys-Lobby.png
- Assets/Screenshots/Toys-TeamSelection.png
- Assets/Screenshots/Toys-Loading.png
- Assets/Screenshots/Toys-HUD.png
- Assets/Screenshots/Toys-ExitConfirmation.png
- Assets/Screenshots/Toys-Lobby-4x3.png
- Assets/Screenshots/Toys-TeamSelection-4x3.png

## Editing

The shared HUD is in Assets/Resources/PlayerWithGFX.prefab; the red variant inherits it. Menu, team selection, and persistent loading UI are in Assets/Scenes/Lobby.unity.

The current builder is AgentScripts/ToysUI.cs, invoked through Unity Pipeline with ToysUI.Prepare, ToysUI.Lobby, ToysUI.Loading, and ToysUI.HUD. ToysUI.FinalizeAssets validates references and sets the product name. VerifyToysUI.cs contains the UI interaction exercises. Earlier ModernUI.cs builders predate this design and should not be reapplied.
