# Authentication, friends, and settings setup

The Unity implementation is installed. `Assets/Scenes/Login.unity` is the first enabled build scene. New players choose Guest or Google Play, complete their nickname and age (minimum 16), and enter Lobby. A cached UGS session restores automatically. Starting Lobby directly in the Editor also routes through Login when needed.

The project is already linked to UGS project `2aabea99-63bd-4a65-a55e-23bbbb652b6e`. The local config is `Assets/Resources/AccountServicesConfig.asset`, with environment `production`. Use the same project and environment for all services and Cloud Code scripts.

## Authentication repair verified 2026-11-05

The login error `Account setup is currently unavailable` was caused by a missing `PlayerIdDirectory` Cloud Code script, not by anonymous authentication. Production only had `CompletePlayerProfile` published. The existing `PlayerIdDirectory.js` was deployed and published as version 1 with its three parameter definitions, and the missing private `toys-player-id-registry/allocator` item was initialized. Existing counters must never be reset.

Live Editor verification restored the cached account, preserved its completed profile, assigned public ID `1000000000`, and automatically entered Lobby with `AccountSession.IsReady == true`. A second Play session restored the same account and public ID successfully. All 15 account/ID Cloud Code tests passed. This is a backend repair, so existing builds using this project/environment can use Retry Login without a rebuild.

`AgentScripts/RepairAccountBackend.cs` provides read-only `Inspect` and guarded `Repair` entry points through `unity command script run`. It uses the Editor's service authentication without printing or storing credentials, targets this production project explicitly, and refuses to initialize a missing allocator if a directory script already exists. `AgentScripts/VerifyAuthenticationRepair.cs` checks the running Login/Lobby session and confirms frames advance.

Google Play still has placeholder configuration and requires the provider/plugin/OAuth setup below. `DeletePlayerData` is also not deployed; complete that setup before testing account deletion. A separate Photon connection/TLS failure was observed after the account successfully reached Lobby; authentication does not resolve that network issue.

## 1. Enable Unity Gaming Services

1. Open your project in the [Unity Dashboard](https://cloud.unity.com/).
2. Enable Authentication, Cloud Save, Friends, and Cloud Code. Anonymous sign-in uses Unity Authentication; no Google credentials are needed for Guest.
3. In Friends, complete the service setup so both player accounts can send and receive requests. See [Unity's Friends setup](https://docs.unity.com/en-us/friends/guides/get-started).
4. Keep `AccountServicesConfig.environment` set to the environment in which you publish the scripts below. Select that environment in the Dashboard.

Installed packages: Authentication 3.8.1, Cloud Save 3.4.2, Friends 1.3.0, Cloud Code 2.10.4, with dependencies resolved by Unity Package Manager.

## 2. Publish the Cloud Code scripts

In Dashboard > Cloud Code, create **JavaScript scripts** with these exact names, paste the corresponding file contents, define the parameters below, and publish all three scripts into the selected environment.

| Script | Source | Required parameters |
| --- | --- | --- |
| `CompletePlayerProfile` | `Assets/CloudCode/CompletePlayerProfile.js` | `nickname`: String; `age`: Numeric |
| `DeletePlayerData` | `Assets/CloudCode/DeletePlayerData.js` | `confirmation`: String |
| `PlayerIdDirectory` | `Assets/CloudCode/PlayerIdDirectory.js` | `action`: String (required); `publicId`: String (optional); `playerIds`: String (optional) |

The Dashboard does not automatically parse `module.exports.params`; enter the parameter definitions in its parameters editor. The files also contain inline parameter declarations for future CLI/Editor deployment. See [Cloud Code script parameters](https://docs.unity.com/en-us/cloud-code/3.0.0/scripts/how-to-guides/script-structure).

### Initialize 10-digit player IDs once

In the same **production** environment, open Cloud Save > Game Data / Custom Data. Create a custom data entity named **`toys-player-id-registry`**. In its **Private** access class, add key **`allocator`** with this JSON object value (an object, not quoted text):

```json
{"next":1000000000,"pending":{}}
```

Create this item only if it does not already exist. Never reset its counter after IDs have been assigned. The server uses the returned Cloud Save write lock to reserve each number atomically; resetting it could reissue an existing number. The ID range is 1000000000–9999999999. This implementation relies on [Cloud Save write-lock conflict checks](https://docs.unity.com/en-us/cloud-save/concepts/write-locks).

Publish **PlayerIdDirectory** and republish the updated **DeletePlayerData** script. Existing accounts receive their number automatically on their next successful sign-in; nickname and age are preserved. Guest-to-Google linking retains the same number. There is no random-number or local-only fallback when the registry is missing.

The public 10-digit number is displayed and copied in Accounts and entered when adding a friend. UGS's internal identifier remains the authentication/Photon/Friends key; the server resolves between these identifiers. Friend-list rows display the 10-digit number when that account has migrated. Deletion removes both directory mappings and any pending reservation. Issued numbers are never reused.

`CompletePlayerProfile` validates the minimum age and nickname on the server, then saves `toys_profile` to **Protected player data**. This access class already permits owner reads and server writes, so no extra client-write grant is needed. Nicknames have 2–16 characters and no whitespace, markup, or control characters; UGS player names do not permit whitespace. The age is not published to other players. The UGS player name is synchronized for the Friends list; Unity adds its own numeric display-name suffix. The lobby has only a room-name field; matches use the saved profile nickname. The ID to share is the **10-digit player ID**, displayed in Accounts and Friends.

`DeletePlayerData` requires exact `CONFIRM`, deletes all the authenticated player's relationships (including pending requests and blocks), Cloud Save files, and Default/Public/Protected player data. It takes the player identity from the trusted request context; it never accepts another player's ID. The client deletes Authentication **after** that cleanup succeeds. This order matters because [DeleteAccountAsync alone does not delete data in other services](https://docs.unity.com/en-us/authentication/delete-accounts).

Interrupted deletion can be retried. A local pending marker prevents entry to Lobby and offers a finish-deletion page until cleanup and account deletion succeed. This confirmation is an intentional-action check, not email verification. If you later add data to Economy, Leaderboards, Analytics, or another backend, extend the deletion endpoint for those services as well.

Until these scripts are published, Guest sign-in can authenticate, but profile completion and deletion will show a service error instead of pretending to succeed.

## 3. Create the Google Play Games configuration

Follow the [Google Play Games Console setup](https://developer.android.com/games/pgs/console/setup) and [Unity plugin setup](https://developer.android.com/games/pgs/unity/unity-start).

1. Choose the final Android application/package ID in Unity Player Settings, and create or select the corresponding application in Play Console.
2. In Play Console > Play Games Services > Setup and management > Configuration, create the Play Games project and link its Google Cloud project. Complete the OAuth consent configuration required by Google.
3. Create an **Android OAuth credential** for the exact application ID and signing certificate SHA-1. Add credentials for the certificates used by your debug/local build and your Play-distributed build as applicable. With Play App Signing, use the **app signing** certificate for builds installed from Play, which differs from the upload certificate.
4. Create a **Game server / Web application OAuth credential**. Keep its Web client ID and client secret for the UGS provider configuration. The Web client ID is different from the Android client ID.
5. Add your Google accounts as Play Games testers, and use an internal testing track when testing a Play-distributed build. Play Games testing access and Play track testing access must both be configured for your testers.

## 4. Import Google Play Games and fill the placeholders

1. Download and import the [official Google Play Games plugin for Unity](https://github.com/playgameservices/play-games-plugin-for-unity/releases). Use a current release that supports Play Games Services v2; Unity's UGS integration requires plugin v11.01 or later.
2. Open Window > Google Play Games > Setup > Android Setup. Paste the Android resources supplied by your Play Games configuration and enter the **Web application client ID**. Complete setup and Android dependency resolution.
3. In Unity Dashboard Authentication > Identity Providers, add **Google Play Games** and enter the same Web client ID and Web client secret. Save the provider. The secret belongs in the Dashboard, never in a Unity asset or game build. See [Unity's Google Play integration](https://docs.unity.com/en-us/authentication/platform-signin/google-play-games).
4. In Unity's Android Player Settings > Scripting Define Symbols, add `TOYS_GOOGLE_PLAY_GAMES` **after importing the plugin**. This enables the native bridge in `Assets/Scripts/Accounts/GooglePlaySignIn.cs`.
5. Select `Assets/Resources/AccountServicesConfig.asset`: replace `YOUR_WEB_CLIENT_ID.apps.googleusercontent.com` with the real Web client ID, then enable `Google Play Configured`.
6. Build for Android and test on a physical device signed into a tester account. Google Play login is unavailable in the Unity Editor and desktop builds; Guest is available there.

The Google button now requests a one-time server authorization code and exchanges it with UGS. Accounts > Link Google Play uses the link API, retaining the guest player's ID, profile, and friendships. If that Google identity belongs to another UGS account, linking fails with a message and retains the guest account; it never force-links or overwrites the other player.

## 5. Sound and graphics

Master controls `AudioListener.volume`. Music and SFX control their respective source volumes. Add `GameAudioChannel` to a music AudioSource, select **Music**, and set its Base Volume to the source's intended volume. The same component can explicitly mark SFX. Existing unmarked AudioSources use the SFX slider and retain their captured base volume. There were no game audio-source scripts to route when this integration was added; assign Music when adding music playback. For dynamically animated source volumes, animate `GameAudioChannel.baseVolume` rather than `AudioSource.volume`.

All three volume values and graphics choice persist locally across restarts. They are saved when Settings closes or the application pauses/exits. Controls is deliberately empty.

Low, Normal, High, and Ultra are real Unity quality levels with separate URP assets under `Assets/Settings/PlayerQuality`. They change render scale, MSAA, shadow resolution/distance/cascades, and LOD bias. Tune the assets against your target devices. Existing Mobile/PC quality levels were retained.

## 6. End-to-end checks after service configuration

1. Start Login with a fresh installation. Choose Guest; verify age 15 is rejected, then complete a valid nickname and age 16. Confirm Lobby opens with only the room-name field. Check the saved nickname and 10-digit player ID in Settings > Accounts.
2. Restart. Confirm it restores directly to Lobby. A network failure must offer retry, without generating a new guest identity.
3. On Android, link Google Play from the guest account. Confirm the player ID is unchanged. Use Google Play on a second fresh device to restore the same account/profile.
4. Use two distinct accounts/devices. Copy one ID and send a friend request from the other. Verify the sender sees a pending request and the recipient sees Accept/Decline. Accept to create the friendship. Also check decline, cancel request, removal, and requests arriving while the panel is open.
5. Change each volume and graphics option, close/reopen Settings, and restart to verify persistence.
6. With a disposable test account, verify `confirm` leaves deletion disabled; type exact `CONFIRM` and submit. Check UGS Player Management, Cloud Save, and the other account's Friends list to confirm removal. Restart and confirm Login appears. A deleted Google account can subsequently create a new game account with a new UGS ID.

## Validation already performed locally

- Unity C# compilation passed.
- Play mode rendered the login/profile forms and Accounts, Sound, Graphics, and deletion panels. Frames were observed advancing.
- Starting Lobby directly without a session switched to Login and left Photon disconnected (`PeerCreated`).
- UI callbacks verified empty Controls, all three slider values, all four actual Unity quality selections, exact-case confirmation, and cancel. No live deletion was invoked.
- Seven Node tests cover server-side age/name validation, owner identity, profile retry behavior, confirmation, paginated cleanup, failure handling, and missing-data retries. Run: `node --test AgentScripts/Tests/accounts-cloud-code.test.cjs`.
- Eight additional Node tests cover the numeric ID range, concurrent allocation, stable IDs across devices, interrupted writes, missing registry, directory lookup, exhaustion, and deletion cleanup. Run: `node --test AgentScripts/Tests/player-id-directory.test.cjs`.
- `AgentScripts/VerifyRoomOnlyLobby.cs` checks the imported room-only lobby, numeric friend-entry length, and C# 10-digit validation.
- Live Google Play sign-in, cross-device restore, friend exchange, and full remote deletion require the Dashboard and Google setup above and have not been exercised against configured production services.

Editor setup helpers are retained in `AgentScripts/InstallAccountPackages.cs` and `AgentScripts/SetupAccounts.cs`; UI checks are in `AgentScripts/VerifyAccountsUi.cs`. They are outside Assets and do not ship. The pre-edit open Lobby scene was backed up in `AgentScripts/Backups/Accounts/Lobby-before-accounts.unity`.
