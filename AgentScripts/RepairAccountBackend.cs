using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using Unity.Services.Core.Editor;
using Unity.Services.Core.Editor.Environments;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

public static class RepairAccountBackend
{
    public static async Task<object> Inspect()
    {
        await EnvironmentsApi.Instance.RefreshAsync();
        var environments = EnvironmentsApi.Instance.Environments;
        return new { projectId = CloudProjectSettings.projectId, environments,
            scripts = await Request("GET", await BaseUrl()),
            registry = await Request("GET", await RegistryUrl(), allowMissing: true) };
    }

    // Only provisions the missing login dependency. Never resets an issued-ID counter
    // or overwrites a remote script. Re-running against a repaired backend is read-only.
    public static async Task<object> Repair()
    {
        string scriptsUrl = await BaseUrl();
        var scripts = await Request("GET", scriptsUrl);
        bool exists = scripts["results"].Any(s => (string)s["name"] == "PlayerIdDirectory");
        var registry = await Request("GET", await RegistryUrl(), allowMissing: true);
        bool hasAllocator = registry?["results"]?.Any(i => (string)i["key"] == "allocator") == true;
        if (!hasAllocator)
        {
            if (exists) throw new InvalidOperationException("Directory already exists but allocator is missing. Restore the allocator from a backup; do not reset issued IDs.");
            await Request("POST", await RegistryUrl(), new JObject {
                ["key"] = "allocator", ["value"] = new JObject { ["next"] = 1000000000L, ["pending"] = new JObject() }
            });
        }
        if (!exists)
        {
            var parameters = new JArray {
                new JObject { ["name"] = "action", ["type"] = "STRING", ["required"] = true },
                new JObject { ["name"] = "publicId", ["type"] = "STRING", ["required"] = false },
                new JObject { ["name"] = "playerIds", ["type"] = "STRING", ["required"] = false }
            };
            await Request("POST", scriptsUrl, new JObject {
                ["name"] = "PlayerIdDirectory", ["type"] = "API", ["language"] = "JS",
                ["code"] = File.ReadAllText("Assets/CloudCode/PlayerIdDirectory.js"), ["params"] = parameters
            });
            await Request("POST", scriptsUrl + "/PlayerIdDirectory/publish");
        }
        return await Inspect();
    }

    private static async Task<string> BaseUrl()
    {
        await EnvironmentsApi.Instance.RefreshAsync();
        var environment = EnvironmentsApi.Instance.Environments.First(e => e.Name == "production");
        if (CloudProjectSettings.projectId != "2aabea99-63bd-4a65-a55e-23bbbb652b6e")
            throw new InvalidOperationException("Unexpected project.");
        return "https://services.api.unity.com/cloud-code/v1/projects/" + CloudProjectSettings.projectId +
            "/environments/" + environment.Id + "/scripts";
    }

    private static async Task<string> RegistryUrl()
    {
        var scripts = await BaseUrl();
        return scripts.Replace("/cloud-code/v1/projects/", "/cloud-save/v1/data/projects/")
            .Replace("/scripts", "/custom/toys-player-id-registry/private/items");
    }

    private static async Task<JToken> Request(string method, string url, JObject body = null, bool allowMissing = false)
    {
        string token = await new AccessTokens().GetServicesGatewayTokenAsync();
        using (var request = new UnityWebRequest(url, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + token);
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body.ToString()));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            var pending = request.SendWebRequest();
            while (!pending.isDone) await Task.Delay(50);
            if (allowMissing && request.responseCode == 404) return null;
            if (request.responseCode < 200 || request.responseCode > 299)
                throw new InvalidOperationException(method + " " + request.responseCode + ": " + request.downloadHandler.text);
            return string.IsNullOrEmpty(request.downloadHandler.text) ? null : JToken.Parse(request.downloadHandler.text);
        }
    }
}
