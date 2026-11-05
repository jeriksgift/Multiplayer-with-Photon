using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public static class VerifyAuthenticationRepair
{
    public static async Task<object> Inspect()
    {
        int before = Time.frameCount;
        await Task.Delay(500);
        var session = AccountSession.Instance;
        var ui = Object.FindAnyObjectByType<AuthenticationScreen>();
        return new {
            scene = SceneManager.GetActiveScene().name,
            framesAdvanced = Time.frameCount - before,
            initialized = UnityServices.State.ToString(),
            signedIn = UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn,
            ready = session.IsReady, profile = session.Profile, publicId = session.PublicPlayerId,
            status = ui ? ui.GetComponent<UIDocument>().rootVisualElement.Q<Label>("AuthenticationStatus").text : null
        };
    }

    public static async Task<object> Restore()
    {
        await AccountSession.Instance.RestoreAsync();
        return await Inspect();
    }
}
