using UnityEngine;

namespace SignalsEverywhere.Panel;

public static class PanelPrefs
{
    public static bool Highlight
    {
        get => PlayerPrefs.GetInt("SignalsEverywhere.CTCPanel.Highlight", 1) == 1;
        set => PlayerPrefs.SetInt("SignalsEverywhere.CTCPanel.Highlight", value ? 1 : 0);
    }

    public static bool BellSound
    {
        get => PlayerPrefs.GetInt("SignalsEverywhere.CTCPanel.Bell", 1) == 1;
        set => PlayerPrefs.SetInt("SignalsEverywhere.CTCPanel.Bell", value ? 1 : 0);
    }
    public static bool ConfigSound
    {
        get => PlayerPrefs.GetInt("SignalsEverywhere.CTCPanel.ConfigSound", 1) == 1;
        set => PlayerPrefs.SetInt("SignalsEverywhere.CTCPanel.ConfigSound", value ? 1 : 0);
    }

    public static float SoundLevel
    {
        get => PlayerPrefs.GetFloat("SignalsEverywhere.CTCPanel.Sound", 0.8f);
        set => PlayerPrefs.SetFloat("SignalsEverywhere.CTCPanel.Sound", value);
    }
    
    
}