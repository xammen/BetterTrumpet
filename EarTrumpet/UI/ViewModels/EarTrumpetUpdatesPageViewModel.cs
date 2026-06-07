using System;

namespace EarTrumpet.UI.ViewModels
{
    // Updates page. Subclasses the About view model so it inherits the update
    // channel / check / install plumbing (and so the existing code-behind handlers,
    // which match on EarTrumpetAboutPageViewModel, keep working). Split out of the
    // former monolithic About page so the update controls have a dedicated home.
    class EarTrumpetUpdatesPageViewModel : EarTrumpetAboutPageViewModel
    {
        public EarTrumpetUpdatesPageViewModel(Action openDiagnostics, AppSettings settings)
            : base(openDiagnostics, settings)
        {
            Glyph = "\xE896"; // Download / update icon
            Title = Properties.Resources.UpdatesSettingsPageText;
            Subtitle = Properties.Resources.UpdatesSettingsPageSubtitle;
        }
    }
}
