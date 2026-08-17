using System;
using System.Runtime.InteropServices;

namespace EarTrumpet.Interop
{
    static class Combase
    {
        // .NET 5+ removed built-in HSTRING / IInspectable P/Invoke marshallers.
        // Passing string + UnmanagedType.HString throws MarshalDirectiveException
        // and silently breaks per-app device switching (GitHub #37).
        [DllImport("combase.dll")]
        private static extern int RoGetActivationFactory(
            IntPtr activatableClassId,
            [In] ref Guid iid,
            out IntPtr factory);

        [DllImport("combase.dll", PreserveSig = false)]
        public static extern void WindowsCreateString(
            [MarshalAs(UnmanagedType.LPWStr)] string src,
            [In] uint length,
            [Out] out IntPtr hstring);

        [DllImport("combase.dll")]
        public static extern int WindowsDeleteString(IntPtr hstring);

        [DllImport("combase.dll")]
        public static extern IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out uint length);

        public static object GetActivationFactory(string activatableClassId, Guid iid)
        {
            if (string.IsNullOrEmpty(activatableClassId))
            {
                throw new ArgumentNullException(nameof(activatableClassId));
            }

            WindowsCreateString(activatableClassId, (uint)activatableClassId.Length, out var classId);
            try
            {
                var hr = RoGetActivationFactory(classId, ref iid, out var factoryPtr);
                Marshal.ThrowExceptionForHR(hr);
                if (factoryPtr == IntPtr.Zero)
                {
                    throw new InvalidOperationException("RoGetActivationFactory returned a null factory.");
                }

                try
                {
                    return Marshal.GetObjectForIUnknown(factoryPtr);
                }
                finally
                {
                    Marshal.Release(factoryPtr);
                }
            }
            finally
            {
                WindowsDeleteString(classId);
            }
        }

        public static string HStringToString(IntPtr hstring)
        {
            if (hstring == IntPtr.Zero)
            {
                return null;
            }

            var buffer = WindowsGetStringRawBuffer(hstring, out var length);
            if (buffer == IntPtr.Zero)
            {
                return string.Empty;
            }

            return Marshal.PtrToStringUni(buffer, (int)length);
        }
    }
}
