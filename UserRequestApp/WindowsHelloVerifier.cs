//
// Copyright © 2010-2025, Sinclair Community College
// Licensed under the GNU General Public License, version 3.
// See the LICENSE file in the project root for full license information.
//

namespace SinclairCC.MakeMeAdmin
{
    using System;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading.Tasks;
    using Windows.Foundation;
    using Windows.Security.Credentials.UI;

    /// <summary>
    /// The application-level meaning of a Windows Hello verification result.
    /// </summary>
    internal enum WindowsHelloResult
    {
        Verified,
        DeviceNotPresent,
        NotConfiguredForUser,
        DisabledByPolicy,
        DeviceBusy,
        RetriesExhausted,
        Canceled,
        Unknown
    }

    /// <summary>
    /// A mapped Windows Hello result that retains the native value for diagnostics.
    /// </summary>
    internal struct WindowsHelloVerification
    {
        internal WindowsHelloVerification(WindowsHelloResult result, int nativeValue)
        {
            this.Result = result;
            this.NativeValue = nativeValue;
        }

        internal WindowsHelloResult Result { get; private set; }

        internal int NativeValue { get; private set; }

        internal bool IsVerified
        {
            get { return this.Result == WindowsHelloResult.Verified; }
        }

        internal bool IsCanceled
        {
            get { return this.Result == WindowsHelloResult.Canceled; }
        }

        internal bool IsUnavailable
        {
            get
            {
                return this.Result == WindowsHelloResult.DeviceNotPresent ||
                       this.Result == WindowsHelloResult.NotConfiguredForUser ||
                       this.Result == WindowsHelloResult.DisabledByPolicy ||
                       this.Result == WindowsHelloResult.DeviceBusy;
            }
        }
    }

    /// <summary>
    /// Requests Windows Hello verification through the desktop interop API so the
    /// native prompt is owned by the supplied Win32 window.
    /// </summary>
    internal sealed class WindowsHelloVerifier
    {
        internal async Task<WindowsHelloVerification> VerifyAsync(IntPtr ownerWindow, string message)
        {
            if (ownerWindow == IntPtr.Zero)
            {
                throw new ArgumentException("A valid owner window is required.", "ownerWindow");
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("A verification message is required.", "message");
            }

            object activationFactory = WindowsRuntimeMarshal.GetActivationFactory(typeof(UserConsentVerifier));
            IUserConsentVerifierInterop verifierInterop = (IUserConsentVerifierInterop)activationFactory;
            Guid operationInterface = typeof(IAsyncOperation<UserConsentVerificationResult>).GUID;
            IAsyncOperation<UserConsentVerificationResult> operation =
                verifierInterop.RequestVerificationForWindowAsync(
                ownerWindow,
                message,
                ref operationInterface);
            UserConsentVerificationResult nativeResult = await operation.AsTask();
            return MapResult((int)nativeResult);
        }

        internal static WindowsHelloVerification MapResult(int nativeValue)
        {
            WindowsHelloResult result;

            switch (nativeValue)
            {
                case 0:
                    result = WindowsHelloResult.Verified;
                    break;
                case 1:
                    result = WindowsHelloResult.DeviceNotPresent;
                    break;
                case 2:
                    result = WindowsHelloResult.NotConfiguredForUser;
                    break;
                case 3:
                    result = WindowsHelloResult.DisabledByPolicy;
                    break;
                case 4:
                    result = WindowsHelloResult.DeviceBusy;
                    break;
                case 5:
                    result = WindowsHelloResult.RetriesExhausted;
                    break;
                case 6:
                    result = WindowsHelloResult.Canceled;
                    break;
                default:
                    result = WindowsHelloResult.Unknown;
                    break;
            }

            return new WindowsHelloVerification(result, nativeValue);
        }

        [ComImport]
        [Guid("39E050C3-4E74-441A-8DC0-B81104DF949C")]
        [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
        private interface IUserConsentVerifierInterop
        {
            IAsyncOperation<UserConsentVerificationResult> RequestVerificationForWindowAsync(
                IntPtr appWindow,
                [MarshalAs(UnmanagedType.HString)] string message,
                [In] ref Guid riid);
        }
    }
}
