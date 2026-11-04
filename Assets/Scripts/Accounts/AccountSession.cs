using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Photon.Pun;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models.Data.Player;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
[UnityEngine.Scripting.Preserve]
public sealed class ToysPlayerProfile
{
    public string nickname;
    public int age;
    public int version;
    public bool IsComplete => version == 1 && age >= 16 && age <= 120 &&
        !string.IsNullOrWhiteSpace(nickname) && nickname.Trim().Length >= 2 && nickname.Length <= 16;
}

[Serializable]
[UnityEngine.Scripting.Preserve]
public sealed class PlayerDataDeletionResult
{
    public bool cleaned;
}

[Serializable]
[UnityEngine.Scripting.Preserve]
public sealed class PlayerIdDirectoryResult
{
    public string publicId;
    public string playerId;
    public Dictionary<string, string> ids;
}

public sealed class AccountSession : MonoBehaviour
{
    public const string LoginScene = "Login";
    public const string LobbyScene = "Lobby";
    public const string ProfileKey = "toys_profile";
    public static AccountSession Instance { get; private set; }
    public ToysPlayerProfile Profile { get; private set; }
    public bool GoogleLinked { get; private set; }
    public string PublicPlayerId { get; private set; }
    public bool IsReady => UnityServices.State == ServicesInitializationState.Initialized &&
        AuthenticationService.Instance.IsAuthorized && Profile != null && Profile.IsComplete && IsPublicPlayerId(PublicPlayerId) && !DeletionPending;
    public string PlayerId => UnityServices.State == ServicesInitializationState.Initialized &&
        AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : string.Empty;
    public bool DeletionPending => !string.IsNullOrEmpty(PlayerId) && PlayerPrefs.GetInt("toys.delete." + PlayerId, 0) == 1;
    public AccountServicesConfig Config { get; private set; }
    private Task initialization;
    private bool routing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() { Instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance) return;
        new GameObject("Account Session").AddComponent<AccountSession>();
    }

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Config = Resources.Load<AccountServicesConfig>("AccountServicesConfig");
        gameObject.AddComponent<PlayerSettingsService>();
    }

    public Task InitializeAsync()
    {
        if (initialization == null || initialization.IsFaulted) initialization = InitializeCoreAsync();
        return initialization;
    }

    private async Task InitializeCoreAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            var options = new InitializationOptions();
            options.SetEnvironmentName(Config ? Config.environment : "production");
            await UnityServices.InitializeAsync(options);
        }
    }

    public async Task<bool> RestoreAsync()
    {
        await InitializeAsync();
        if (AuthenticationService.Instance.IsSignedIn && !AuthenticationService.Instance.IsAuthorized)
            AuthenticationService.Instance.SignOut();
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            if (!AuthenticationService.Instance.SessionTokenExists) return false;
            try { await AuthenticationService.Instance.SignInAnonymouslyAsync(); }
            catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.InvalidSessionToken)
            {
                AuthenticationService.Instance.SignOut(true);
                Profile = null;
                GoogleLinked = false;
                return false;
            }
        }
        await LoadProfileAsync();
        return true;
    }

    public async Task SignInGuestAsync()
    {
        await InitializeAsync();
        if (AuthenticationService.Instance.IsSignedIn && !AuthenticationService.Instance.IsAuthorized)
            AuthenticationService.Instance.SignOut();
        if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        await LoadProfileAsync();
    }

    public async Task SignInGoogleAsync()
    {
        await InitializeAsync();
        string code = await GooglePlaySignIn.GetAuthorizationCodeAsync(Config);
        if (AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(code);
        else
            await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(code);
        await LoadProfileAsync();
    }

    public async Task LinkGoogleAsync()
    {
        if (!IsReady) throw new InvalidOperationException("Please sign in again before linking your account.");
        if (GoogleLinked) return;
        string code = await GooglePlaySignIn.GetAuthorizationCodeAsync(Config);
        await AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(code);
        var info = await AuthenticationService.Instance.GetPlayerInfoAsync();
        GoogleLinked = !string.IsNullOrEmpty(info.GetGooglePlayGamesId());
    }

    private async Task LoadProfileAsync()
    {
        Profile = null;
        PublicPlayerId = null;
        var info = await AuthenticationService.Instance.GetPlayerInfoAsync();
        GoogleLinked = !string.IsNullOrEmpty(info.GetGooglePlayGamesId());
        var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { ProfileKey },
            new LoadOptions(new ProtectedReadAccessClassOptions()));
        if (data.TryGetValue(ProfileKey, out var item))
        {
            var profile = item.Value.GetAs<ToysPlayerProfile>();
            if (profile != null && profile.IsComplete) Profile = profile;
        }
        if (Profile != null && !DeletionPending)
        {
            await EnsurePublicPlayerIdAsync();
            await SynchronizePlayerNameAsync(Profile.nickname);
            PhotonNetwork.NickName = Profile.nickname;
        }
    }

    public async Task CompleteProfileAsync(string nickname, int age)
    {
        nickname = (nickname ?? string.Empty).Trim();
        if (nickname.Length < 2 || nickname.Length > 16 || ContainsInvalidNameCharacter(nickname))
            throw new ArgumentException("Nickname must have 2–16 characters, with no spaces, markup, or control characters.");
        if (age < 16 || age > 120) throw new ArgumentException("You must be at least 16. Enter an age from 16 to 120.");
        if (DeletionPending) throw new InvalidOperationException("Account deletion has already started.");
        var profile = await CloudCodeService.Instance.CallEndpointAsync<ToysPlayerProfile>("CompletePlayerProfile",
            new Dictionary<string, object> { { "nickname", nickname }, { "age", age } });
        if (profile == null || !profile.IsComplete) throw new InvalidOperationException("The profile could not be saved. Please try again.");
        await EnsurePublicPlayerIdAsync();
        await SynchronizePlayerNameAsync(profile.nickname);
        Profile = profile;
        PhotonNetwork.NickName = Profile.nickname;
    }

    private static bool ContainsInvalidNameCharacter(string value)
    {
        foreach (char c in value) if (char.IsWhiteSpace(c) || char.IsControl(c) || c == '<' || c == '>') return true;
        return false;
    }

    public static bool IsPublicPlayerId(string value)
    {
        if (value == null || value.Length != 10 || value[0] == '0') return false;
        foreach (char c in value) if (c < '0' || c > '9') return false;
        return true;
    }

    private async Task EnsurePublicPlayerIdAsync()
    {
        var result = await CloudCodeService.Instance.CallEndpointAsync<PlayerIdDirectoryResult>("PlayerIdDirectory",
            new Dictionary<string, object> { { "action", "get" } });
        if (result == null || !IsPublicPlayerId(result.publicId) || result.playerId != PlayerId)
            throw new InvalidOperationException("Your player ID could not be loaded. Please try again.");
        PublicPlayerId = result.publicId;
    }

    public async Task<string> ResolvePublicPlayerIdAsync(string publicId)
    {
        if (!IsPublicPlayerId(publicId)) throw new ArgumentException("Enter a 10-digit player ID.");
        var result = await CloudCodeService.Instance.CallEndpointAsync<PlayerIdDirectoryResult>("PlayerIdDirectory",
            new Dictionary<string, object> { { "action", "resolve" }, { "publicId", publicId } });
        if (result == null || result.publicId != publicId || string.IsNullOrEmpty(result.playerId))
            throw new InvalidOperationException("Player ID was not found. Check the number and try again.");
        return result.playerId;
    }

    public async Task<Dictionary<string, string>> LookupPublicPlayerIdsAsync(IEnumerable<string> playerIds)
    {
        var result = await CloudCodeService.Instance.CallEndpointAsync<PlayerIdDirectoryResult>("PlayerIdDirectory",
            new Dictionary<string, object> { { "action", "lookup" }, { "playerIds", string.Join(",", playerIds) } });
        return result?.ids ?? new Dictionary<string, string>();
    }

    private static async Task SynchronizePlayerNameAsync(string nickname)
    {
        string current = await AuthenticationService.Instance.GetPlayerNameAsync(false);
        if (current != nickname && (current == null || !current.StartsWith(nickname + "#", StringComparison.Ordinal)))
            await AuthenticationService.Instance.UpdatePlayerNameAsync(nickname);
    }

    public void EnterLobby()
    {
        if (!IsReady) throw new InvalidOperationException("Finish signing in and complete your profile first.");
        SceneManager.LoadScene(LobbyScene);
    }

    public void RequireLogin()
    {
        if (!routing) StartCoroutine(RouteToLogin());
    }

    private IEnumerator RouteToLogin()
    {
        routing = true;
        yield return null;
        SceneManager.LoadScene(LoginScene);
        routing = false;
    }

    public async Task DeleteAccountAsync(string confirmation)
    {
        if (confirmation != "CONFIRM") throw new ArgumentException("Type CONFIRM exactly to permanently delete your account.");
        await InitializeAsync();
        if (!AuthenticationService.Instance.IsAuthorized) throw new InvalidOperationException("Please reconnect and sign in before deleting your account.");
        string id = PlayerId;
        PlayerPrefs.SetInt("toys.delete." + id, 1);
        PlayerPrefs.Save();
        var result = await CloudCodeService.Instance.CallEndpointAsync<PlayerDataDeletionResult>("DeletePlayerData",
            new Dictionary<string, object> { { "confirmation", confirmation } });
        if (result == null || !result.cleaned)
            throw new InvalidOperationException("Player data cleanup is not finished. Please confirm deletion again to retry.");
        await AuthenticationService.Instance.DeleteAccountAsync();
        if (AuthenticationService.Instance.IsSignedIn) AuthenticationService.Instance.SignOut(true);
        else AuthenticationService.Instance.ClearSessionToken();
        Profile = null;
        GoogleLinked = false;
        PublicPlayerId = null;
        PlayerPrefs.DeleteKey("toys.delete." + id);
        PlayerSettingsService.Instance.ResetPreferences();
        PlayerPrefs.Save();
        if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
        SceneManager.LoadScene(LoginScene);
    }
}
