using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace TomodachiDrawer.UI.Avalonia;

// This is a mitigation for this issue which popped up in Sentry as well as issue #119
// I could not for the life of me find an issue on the subject, so I had to make one: https://github.com/AvaloniaUI/Avalonia/issues/21491
// TL;DR: Avalonia tries to inform the Narrator service (or similar accessibility tools which read the window) when theres a change,
// but the value of the NumericUpDown is a decimal, and Avalonia's ComMarshaller doesn't have a case for that and falls to an argument exception.
// This doesn't happen for those without a narrator enabled.
// This mitigates it by just making a stupid automation peer that doesn't do the update.
public class SafeNumericUpDown : NumericUpDown
{
    protected override Type StyleKeyOverride => typeof(NumericUpDown);

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);
}
