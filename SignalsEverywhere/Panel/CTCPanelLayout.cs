using System.Collections.Generic;
using System.Linq;

namespace SignalsEverywhere.Panel;

public class CTCPanelLayout
{
    public enum LayoutType
    {
        Label,
        Light,
        Track,
        SwitchLeftTop,
        SwitchRightTop,
        SwitchRightBottom,
        SwitchLeftBottom,
        TrackRise,
        TrackFall
        
    }

    public class SchematicElement
    {
        public float X;
        public int Y;
        public LayoutType Type;
        public string? Id;
        public string? Block;
        public string? SwitchLabel;
        public float? LabelOffsetX;
        public float? LabelOffsetY;
        public bool ShowTrack = true;
        public string Color = "white";
        
        public InterlockControlElement? Interlock;
        public CrossoverControlElement? Crossover;
    }
    
    public class InterlockControlElement
    {
        public string Interlock;
        
        public List<string>? VanillaSwitchKnobIds;
        public string? VanillaDirKnobId;
        
        public List<int> KnobOrder = new();
        public List<string> SwitchLabels = new();

        public string? SignalLabel = "";

        public string SwitchKnobId(int num) => VanillaSwitchKnobIds?[num] ?? $"{Interlock}-{num}";
        public string DirKnobId() => VanillaDirKnobId ?? $"{Interlock}-D";
    }
    
    public class CrossoverControlElement
    {
        public string Crossover;
        
        public List<int> SwitchKnobOrder = new();
        public List<int> SignalKnobOrder = new();
        public List<string> SwitchLabels = new();
        public List<string> SignalLabels = new();

        public string SwitchKnobId(int num) => $"{Crossover}-{num}";
        public string SignalKnobId(string groupId) => $"{Crossover}-{groupId}";
    }
    
    public Dictionary<string, List<SchematicElement>> panel { get; set; }

    public void Squash()
    {
        foreach (var elements in panel.Values)
        {
            var usedXs = elements.Select(e => e.X).Distinct().OrderBy(x => x).ToArray();
            var map = new Dictionary<float, float>();
            for (int i = 0; i < usedXs.Length; i++) 
                map[usedXs[i]] = i;
            foreach (var e in elements)
            {
                e.X = map[e.X] + 1;
            }
        }
    }
}