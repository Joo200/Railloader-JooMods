using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Track;
using Track.Signals;
using UI.Builder;
using UnityEngine.UI;

namespace SignalsEverywhere.Rr2026;

// Members Railroader 2026.1 renamed or dropped, added back as C# 14 extension members so the code that uses them reads
// as it does for Legacy.
internal static class Compat2026
{
    extension(TrainController trainController)
    {
        public Graph graph => trainController.Graph;
    }

    // Interlockings and predicates hold switch node ids now (track nodes are plain objects, looked up in the graph).
    extension(CTCInterlocking.SwitchSet set)
    {
        public List<TrackNode> switchNodes =>
            set.switchNodeIds?.Select(id => Graph.Shared.GetNode(id)).Where(n => n != null).ToList() ?? new List<TrackNode>();
    }

    extension(CTCPredicateSignal.Predicate predicate)
    {
        public TrackNode? switchNode
        {
            get => string.IsNullOrEmpty(predicate.switchNodeId) ? null : Graph.Shared.GetNode(predicate.switchNodeId);
            set => predicate.switchNodeId = value?.id;
        }
    }

    extension(ref UIPanelBuilder builder)
    {
        public float Spacing
        {
            get => Traverse.Create(builder).Field<UnityEngine.RectTransform>("_container").Value.GetComponent<HorizontalOrVerticalLayoutGroup>().spacing;
            set => builder.ConfigureLayout(spacing: value);
        }
    }
}
