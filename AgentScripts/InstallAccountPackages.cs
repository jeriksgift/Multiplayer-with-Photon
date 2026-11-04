using System;
using System.Threading.Tasks;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

public static class InstallAccountPackages
{
    public static async Task<string> Install()
    {
        var request = Client.AddAndRemove(new[] {
            "com.unity.services.authentication", "com.unity.services.cloudsave",
            "com.unity.services.friends", "com.unity.services.cloudcode"
        });
        while (!request.IsCompleted) await Task.Delay(100);
        if (request.Status != StatusCode.Success) throw new Exception(request.Error.message);
        return "Account service packages installed.";
    }
}
