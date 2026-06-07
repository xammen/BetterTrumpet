using System;

namespace EarTrumpet.UI.ViewModels
{
    // Privacy & data page. Subclasses the About view model so it inherits the
    // telemetry toggle and settings export/import commands (and so the existing
    // code-behind handlers, which match on EarTrumpetAboutPageViewModel, keep
    // working). Split out of the former monolithic About page.
    class EarTrumpetPrivacyPageViewModel : EarTrumpetAboutPageViewModel
    {
        public EarTrumpetPrivacyPageViewModel(Action openDiagnostics, AppSettings settings)
            : base(openDiagnostics, settings)
        {
            Glyph = "\xE72E"; // Lock / privacy icon
            Title = Properties.Resources.PrivacySettingsPageText;
            Subtitle = Properties.Resources.PrivacySettingsPageSubtitle;
        }
    }
}
