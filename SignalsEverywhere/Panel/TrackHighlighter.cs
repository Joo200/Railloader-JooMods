using UnityEngine;
using UnityEngine.UI;

namespace SignalsEverywhere.Panel;

public class TrackHighlighter : MonoBehaviour
{
    public string? id = null;
    public Schematic? Schematic { get;
        set { field = value;
            if (!_registered && field != null)
            {
                field.OnHighlightRequest += SetColor; _registered = true;
            } 
        }
    }
    public Color HighlightColor { get; set; } = Color.white;

    private bool _registered = false;

    private void OnEnable()
    {
        if (Schematic != null && !_registered)
        {
            _registered = true;
            Schematic.OnHighlightRequest += SetColor;
        }
    }
    
    private void OnDisable()
    {
        if (Schematic != null)
            Schematic.OnHighlightRequest -= SetColor;
        _registered = false;
    }

    private void SetColor(string name, bool val)
    {
        val &= PanelPrefs.Highlight;
        if (name == id)
        {
            GetComponent<Image>().color = val ? HighlightColor : ViewCreator.TrackColor;
        }
    }
}