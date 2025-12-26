using System.Collections.Generic;
using Audio;
using HarmonyLib;
using Track.Signals;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SignalsEverywhere.Panel;

public static class SoundHelper
{
    private static AccessTools.FieldRef<CTCPanelController, List<AudioClip>> CodeSounds = 
        AccessTools.FieldRefAccess<CTCPanelController, List<AudioClip>>("codeButtonAudioClips");
    
    public static void PlayKlick()
    {
        if (PanelPrefs.ConfigSound == false) return;
        if (CTCPanelController.Shared == null) return;
        
        var controller = CTCPanelController.Shared;
        PlayRandomSound(CodeSounds(controller), "CTCPanelCode");
    }

    private static void PlayRandomSound(List<AudioClip> clips, string name)
    {
        if (clips.Count <= 0) return;
        int num = Random.Range(0, clips.Count);
        var clip = clips[num];
        
        Camera main = Camera.main;
        if (main == null) return;

        Transform transform = TrainController.Shared.transform;
        Vector3 offset = transform.InverseTransformPoint(main.transform.position);
        IAudioSource source = VirtualAudioSourcePool.Checkout(name, clip, false, AudioController.Group.CTC, 5, transform, AudioDistance.Local, offset);
        source.volume = PanelPrefs.SoundLevel;
        source.spatialBlend = 0.0f;
        source.minDistance = 1f;
        source.maxDistance = 7f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.Play();
        VirtualAudioSourcePool.ReturnAfterFinished(source);
    }
}