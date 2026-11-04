using System;
using System.Threading.Tasks;

public static class GooglePlaySignIn
{
    public static async Task<string> GetAuthorizationCodeAsync(AccountServicesConfig config)
    {
        if (config == null || !config.googlePlayConfigured ||
            string.IsNullOrWhiteSpace(config.googlePlayWebClientId) || config.googlePlayWebClientId.StartsWith("YOUR_"))
            throw new InvalidOperationException("Google Play sign-in is not configured yet. Please use Guest for now.");
#if UNITY_ANDROID && !UNITY_EDITOR && TOYS_GOOGLE_PLAY_GAMES
        GooglePlayGames.PlayGamesPlatform.Activate();
        var result = new TaskCompletionSource<string>();
        GooglePlayGames.PlayGamesPlatform.Instance.ManuallyAuthenticate(status =>
        {
            if (status != GooglePlayGames.BasicApi.SignInStatus.Success)
            {
                result.TrySetException(new InvalidOperationException("Google Play sign-in was cancelled or unavailable. Please try again."));
                return;
            }
            GooglePlayGames.PlayGamesPlatform.Instance.RequestServerSideAccess(true, code =>
            {
                if (string.IsNullOrWhiteSpace(code))
                    result.TrySetException(new InvalidOperationException("Google Play did not return an authorization code. Please try again."));
                else result.TrySetResult(code);
            });
        });
        if (await Task.WhenAny(result.Task, Task.Delay(90000)) != result.Task)
            throw new TimeoutException("Google Play sign-in timed out. Please try again.");
        return await result.Task;
#else
        await Task.CompletedTask;
        throw new InvalidOperationException("Google Play sign-in requires an Android build with the Google Play Games plugin. Guest login is available here.");
#endif
    }
}
