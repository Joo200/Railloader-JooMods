using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SignalsEverywhere.Rr2026;

// From Tetz's SignalsEverywhere Fixes (on Legacy it patches this in). The CTC panel is one vertical scroll view
// holding a horizontal scroll view per section. Unity turns a vertical mouse wheel over a horizontal-only scroll view
// into sideways scrolling and doesn't pass it on, so over a section (most of the panel) the wheel moved that section
// sideways and the panel couldn't be scrolled down. After each rebuild of the panel (WindowBase.Rebuild), every
// section gets a handler that sends the vertical wheel to the panel and keeps sideways scrolling for Shift + wheel
// and sideways swipes.
internal static class PanelScrolling
{
    internal static void Apply(Component panel)
    {
        foreach (ScrollRect scroll in panel.GetComponentsInChildren<ScrollRect>(true))
        {
            if (scroll.horizontal && !scroll.vertical && scroll.GetComponent<SectionWheel>() == null)
            {
                SectionWheel wheel = scroll.gameObject.AddComponent<SectionWheel>();
                wheel.Own = scroll;
                wheel.Sensitivity = scroll.scrollSensitivity;
                scroll.scrollSensitivity = 0f;   // its own wheel handling now does nothing; SectionWheel decides
            }
        }
    }
}

// On a section's horizontal scroll view: the wheel scrolls the panel; Shift + wheel, or a sideways swipe, scrolls the
// section.
internal class SectionWheel : MonoBehaviour, IScrollHandler
{
    private const float SidewaysFactor = 4f;
    private static bool logged;

    internal ScrollRect Own;
    internal float Sensitivity = 1f;

    public void OnScroll(PointerEventData eventData)
    {
        if (Own == null)
        {
            return;
        }
        Vector2 delta = eventData.scrollDelta;
        Keyboard keyboard = Keyboard.current;
        bool shift = keyboard != null && keyboard.shiftKey.isPressed;
        ScrollRect panel = Panel();
        if (!logged)
        {
            logged = true;
            Serilog.Log.ForContext<SectionWheel>().Information($"CTC panel scrolling: wheel delta {delta}, section sensitivity {Sensitivity}, panel sensitivity {(panel != null ? panel.scrollSensitivity : 0f)}.");
        }
        if (shift || Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            // Let the section's own scroll view do it, just for this event. Its own sensitivity moves a few pixels a
            // notch, far too slow across a wide section: use four times the panel's vertical speed.
            float panelSpeed = panel != null ? panel.scrollSensitivity : Sensitivity;
            Own.scrollSensitivity = Mathf.Max(Sensitivity, panelSpeed * SidewaysFactor);
            Own.OnScroll(eventData);
            Own.scrollSensitivity = 0f;
            return;
        }
        if (panel != null)
        {
            panel.OnScroll(eventData);
        }
    }

    private ScrollRect Panel()
    {
        Transform parent = Own.transform.parent;
        foreach (ScrollRect scroll in parent != null ? parent.GetComponentsInParent<ScrollRect>() : new ScrollRect[0])
        {
            if (scroll != Own && scroll.vertical)
            {
                return scroll;
            }
        }
        return null;
    }
}
