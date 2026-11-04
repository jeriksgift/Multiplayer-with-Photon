using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public sealed class AuthenticationScreen : MonoBehaviour
{
    private VisualElement root;
    private Label status;
    private IVisualElementScheduledItem clock;
    private bool busy;
    private bool started;
    private int generation;

    private void OnEnable()
    {
        generation++;
        root = GetComponent<UIDocument>().rootVisualElement;
        status = root.Q<Label>("AuthenticationStatus");
        root.Q<Button>("GooglePlayButton").clicked += Google;
        root.Q<Button>("GuestButton").clicked += Guest;
        root.Q<Button>("CompleteProfileButton").clicked += CompleteProfile;
        root.Q<Button>("RetryLoginButton").clicked += Retry;
        root.Q<Button>("ResumeDeleteButton").clicked += ResumeDelete;
        root.Q<Button>("ExitGameButton").clicked += Exit;
        root.Q<TextField>("ResumeDeleteConfirmation").RegisterValueChangedCallback(UpdateDeleteButton);
        root.Q<Button>("ResumeDeleteButton").SetEnabled(false);
        UpdateClock();
        clock = root.schedule.Execute(UpdateClock).Every(1000);
        if (started) Retry();
    }

    private void Start() { started = true; Retry(); }
    private void OnDisable()
    {
        generation++;
        clock?.Pause();
        if (root == null) return;
        root.Q<Button>("GooglePlayButton").clicked -= Google;
        root.Q<Button>("GuestButton").clicked -= Guest;
        root.Q<Button>("CompleteProfileButton").clicked -= CompleteProfile;
        root.Q<Button>("RetryLoginButton").clicked -= Retry;
        root.Q<Button>("ResumeDeleteButton").clicked -= ResumeDelete;
        root.Q<Button>("ExitGameButton").clicked -= Exit;
        root.Q<TextField>("ResumeDeleteConfirmation").UnregisterValueChangedCallback(UpdateDeleteButton);
    }

    private void UpdateClock() { root.Q<Label>("DateTimeLabel").text = LobbyScreen.FormatLocalTime(DateTime.Now); }
    private void Exit() { if (!busy) Application.Quit(); }
    private void Google() { Run(() => AccountSession.Instance.SignInGoogleAsync(), "Opening Google Play…"); }
    private void Guest() { Run(() => AccountSession.Instance.SignInGuestAsync(), "Signing in…"); }
    private void Retry() { Run(async () => { await AccountSession.Instance.RestoreAsync(); }, "Checking your account…"); }
    private void CompleteProfile()
    {
        Run(() => AccountSession.Instance.CompleteProfileAsync(root.Q<TextField>("ProfileNickname").value,
            root.Q<IntegerField>("ProfileAge").value), "Saving your profile…");
    }
    private void ResumeDelete()
    {
        Run(() => AccountSession.Instance.DeleteAccountAsync(root.Q<TextField>("ResumeDeleteConfirmation").value), "Deleting your account and data…");
    }

    private void UpdateDeleteButton(ChangeEvent<string> evt)
    {
        root.Q<Button>("ResumeDeleteButton").SetEnabled(!busy && evt.newValue == "CONFIRM");
    }

    private async void Run(Func<Task> operation, string message)
    {
        if (busy) return;
        int current = generation;
        busy = true;
        SetEnabled(false);
        status.text = message;
        Show("RetryLoginButton", false);
        try
        {
            await operation();
            if (!this || current != generation) return;
            status.text = string.Empty;
            var session = AccountSession.Instance;
            if (session.IsReady) { session.EnterLobby(); return; }
            bool signedIn = UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;
            Show("SignInPage", !signedIn);
            Show("ProfilePage", signedIn && !session.DeletionPending);
            Show("ResumeDeletionPage", session.DeletionPending);
            if (signedIn && !session.DeletionPending) root.Q<TextField>("ProfileNickname").Focus();
        }
        catch (Exception ex)
        {
            if (!this || current != generation) return;
            status.text = FriendlyError(ex);
            Show("RetryLoginButton", true);
        }
        finally
        {
            if (this && current == generation)
            {
                busy = false;
                SetEnabled(true);
                root.Q<Button>("ResumeDeleteButton").SetEnabled(root.Q<TextField>("ResumeDeleteConfirmation").value == "CONFIRM");
            }
        }
    }

    public static string FriendlyError(Exception ex)
    {
        if (ex is Unity.Services.CloudCode.CloudCodeException cloud && cloud.Reason == Unity.Services.CloudCode.CloudCodeExceptionReason.NotFound)
            return "Account setup is currently unavailable. Please try again later.";
        if (ex is AuthenticationException auth && auth.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            return "This Google Play account is already linked to another player. Your guest account has been kept.";
        if (ex is ArgumentException || ex is InvalidOperationException || ex is TimeoutException) return ex.Message;
        if (ex is Unity.Services.Friends.Exceptions.FriendsServiceException friends && friends.StatusCode == System.Net.HttpStatusCode.NotFound)
            return "Player or friend request not found. Check the player ID and refresh.";
        if (ex is RequestFailedException service)
            return "Could not reach the account service (" + service.ErrorCode + "). Check your connection and try again.";
        return "Something went wrong. Please try again.";
    }

    private void SetEnabled(bool value)
    {
        foreach (var button in root.Query<Button>().ToList()) button.SetEnabled(value);
        root.Q<TextField>("ProfileNickname").SetEnabled(value);
        root.Q<IntegerField>("ProfileAge").SetEnabled(value);
        root.Q<TextField>("ResumeDeleteConfirmation").SetEnabled(value);
    }
    private void Show(string name, bool visible) { root.Q(name).EnableInClassList("toys-hidden", !visible); }
}
