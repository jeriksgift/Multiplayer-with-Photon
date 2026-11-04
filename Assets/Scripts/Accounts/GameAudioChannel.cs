using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class GameAudioChannel : MonoBehaviour
{
    public enum Channel { Music, Sfx }
    public Channel channel = Channel.Sfx;
    [Range(0, 1)] public float baseVolume = 1f;

    private void OnEnable() { Apply(); }
    private void LateUpdate() { Apply(); }
    private void Apply()
    {
        if (!PlayerSettingsService.Instance) return;
        GetComponent<AudioSource>().volume = baseVolume * (channel == Channel.Music
            ? PlayerSettingsService.Instance.Music : PlayerSettingsService.Instance.Sfx);
    }
}
