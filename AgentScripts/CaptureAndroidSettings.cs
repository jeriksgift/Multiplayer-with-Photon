var settings = UnityEngine.Object.FindAnyObjectByType<LobbySettingsScreen>();
settings.Open();
UnityEngine.ScreenCapture.CaptureScreenshot("Temp/android-settings-native.png");
return "Queued settings capture";
