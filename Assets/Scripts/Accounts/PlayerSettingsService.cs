using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerSettingsService : MonoBehaviour
{
    public static PlayerSettingsService Instance { get; private set; }
    public static readonly string[] GraphicsOptions = { "Low", "Normal", "High", "Ultra" };
    public float Master { get; private set; }
    public float Music { get; private set; }
    public float Sfx { get; private set; }
    public int Graphics { get; private set; }
    private readonly Dictionary<AudioSource, float> unmanagedSources = new Dictionary<AudioSource, float>();
    private float nextScan;

    private void Awake()
    {
        Instance = this;
        Master = PlayerPrefs.GetFloat("toys.master", 1f);
        Music = PlayerPrefs.GetFloat("toys.music", 1f);
        Sfx = PlayerPrefs.GetFloat("toys.sfx", 1f);
        Graphics = Mathf.Clamp(PlayerPrefs.GetInt("toys.graphics", 1), 0, 3);
        ApplyGraphics();
        AudioListener.volume = Master;
    }

    public void SetVolumes(float master, float music, float sfx)
    {
        Master = Mathf.Clamp01(master); Music = Mathf.Clamp01(music); Sfx = Mathf.Clamp01(sfx);
        PlayerPrefs.SetFloat("toys.master", Master); PlayerPrefs.SetFloat("toys.music", Music); PlayerPrefs.SetFloat("toys.sfx", Sfx);
        AudioListener.volume = Master;
        ApplySfx();
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + .25f;
        foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            if (!source.GetComponent<GameAudioChannel>() && !unmanagedSources.ContainsKey(source)) unmanagedSources.Add(source, source.volume);
        ApplySfx();
    }

    private void ApplySfx()
    {
        var expired = new List<AudioSource>();
        foreach (var pair in unmanagedSources)
        {
            if (!pair.Key) expired.Add(pair.Key);
            else if (!pair.Key.GetComponent<GameAudioChannel>()) pair.Key.volume = pair.Value * Sfx;
        }
        foreach (var source in expired) unmanagedSources.Remove(source);
    }

    public void SetGraphics(int value)
    {
        Graphics = Mathf.Clamp(value, 0, 3);
        PlayerPrefs.SetInt("toys.graphics", Graphics);
        ApplyGraphics();
    }

    private void ApplyGraphics()
    {
        int index = System.Array.IndexOf(QualitySettings.names, GraphicsOptions[Graphics]);
        if (index >= 0) QualitySettings.SetQualityLevel(index, true);
    }

    public void ResetPreferences()
    {
        foreach (string key in new[] { "toys.master", "toys.music", "toys.sfx", "toys.graphics" }) PlayerPrefs.DeleteKey(key);
        Master = Music = Sfx = 1;
        Graphics = 1;
        AudioListener.volume = 1;
        ApplySfx(); ApplyGraphics();
    }

    private void OnApplicationPause(bool paused) { if (paused) PlayerPrefs.Save(); }
    private void OnApplicationQuit() { PlayerPrefs.Save(); }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
