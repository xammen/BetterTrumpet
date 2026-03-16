using System;
using Windows.ApplicationModel;

namespace EarTrumpet.Interop.Helpers
{
    class PackageHelper
    {
        public static Version GetVersion(bool isPackaged)
        {
            if (isPackaged)
            {
                var packageVer = Package.Current.Id.Version;
                return new Version(packageVer.Major, packageVer.Minor, packageVer.Build, packageVer.Revision);
            }
            else
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            }
        }

        public static string GetFamilyName(bool isPackaged)
        {
            return isPackaged ? Package.Current.Id.FamilyName : null;
        }

        public static bool CheckHasIdentity()
        {
#if VSDEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                return false;
            }
#endif

            try
            {
                return Package.Current.Id != null;
            }
            catch (InvalidOperationException)
            {
                // Expected in non-packaged mode (portable / classic installer).
                // Not an error — just means we're not running as MSIX.
                System.Diagnostics.Trace.WriteLine("PackageHelper: No package identity (portable/installer mode)");
                return false;
            }
        }

        public static bool HasDevIdentity()
        {
#if VSDEBUG
            return true;
#else
            bool result = false;
            try
            {
                result = Package.Current.DisplayName.EndsWith("(dev)");
            }
            catch
            {
            }
            return result;
#endif
        }
    }
}
