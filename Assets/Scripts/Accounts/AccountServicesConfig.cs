using UnityEngine;

[CreateAssetMenu(menuName = "Toys/Account Services Config")]
public sealed class AccountServicesConfig : ScriptableObject
{
    public string environment = "production";
    public bool googlePlayConfigured;
    [Tooltip("Public Web OAuth client ID. Configure the same ID in Google Play Games Android Setup. Never store a client secret here.")]
    public string googlePlayWebClientId = "YOUR_WEB_CLIENT_ID.apps.googleusercontent.com";
}
