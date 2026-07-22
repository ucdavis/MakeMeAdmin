//
// Copyright © 2010-2025, Sinclair Community College
// Licensed under the GNU General Public License, version 3.
// See the LICENSE file in the project root for full license information.
//

namespace SinclairCC.MakeMeAdmin
{
    using System;
    using System.Security.Principal;
    using System.Threading.Tasks;

    internal enum AuthenticationOutcome
    {
        Succeeded,
        Canceled,
        Unavailable,
        Failed
    }

    internal struct AuthenticationResult
    {
        internal AuthenticationResult(AuthenticationOutcome outcome)
        {
            this.Outcome = outcome;
        }

        internal AuthenticationOutcome Outcome { get; private set; }

        internal bool Succeeded
        {
            get { return this.Outcome == AuthenticationOutcome.Succeeded; }
        }
    }

    /// <summary>
    /// Coordinates configured authentication methods without granting rights itself.
    /// </summary>
    internal sealed class AuthenticationManager
    {
        private readonly Func<IntPtr, string, Task<WindowsHelloVerification>> verifyWindowsHello;
        private readonly Func<IntPtr, WindowsIdentity, bool> verifyPassword;

        internal AuthenticationManager()
            : this(
                  (ownerWindow, message) => new WindowsHelloVerifier().VerifyAsync(ownerWindow, message),
                  (ownerWindow, identity) => new PasswordAuthenticator().AuthenticateCurrentUser(ownerWindow, identity))
        {
        }

        internal AuthenticationManager(
            Func<IntPtr, string, Task<WindowsHelloVerification>> verifyWindowsHello,
            Func<IntPtr, WindowsIdentity, bool> verifyPassword)
        {
            this.verifyWindowsHello = verifyWindowsHello ?? throw new ArgumentNullException("verifyWindowsHello");
            this.verifyPassword = verifyPassword ?? throw new ArgumentNullException("verifyPassword");
        }

        internal async Task<AuthenticationResult> AuthenticateCurrentUserAsync(
            AuthenticationMode mode,
            IntPtr ownerWindow,
            WindowsIdentity currentIdentity,
            string windowsHelloMessage)
        {
            if (!Enum.IsDefined(typeof(AuthenticationMode), mode))
            {
                throw new InvalidOperationException(
                    string.Format("Authentication Mode contains unsupported value {0}.", (int)mode));
            }

            switch (mode)
            {
                case AuthenticationMode.None:
                    return new AuthenticationResult(AuthenticationOutcome.Succeeded);

                case AuthenticationMode.Password:
                    return this.AuthenticateWithPassword(ownerWindow, currentIdentity);

                case AuthenticationMode.WindowsHello:
                    return await this.AuthenticateWithWindowsHelloAsync(
                        ownerWindow,
                        currentIdentity,
                        windowsHelloMessage,
                        false);

                case AuthenticationMode.WindowsHelloWithPasswordFallback:
                    return await this.AuthenticateWithWindowsHelloAsync(
                        ownerWindow,
                        currentIdentity,
                        windowsHelloMessage,
                        true);

                default:
                    throw new InvalidOperationException("The authentication mode is not supported.");
            }
        }

        private AuthenticationResult AuthenticateWithPassword(
            IntPtr ownerWindow,
            WindowsIdentity currentIdentity)
        {
            if (currentIdentity == null)
            {
                throw new ArgumentNullException("currentIdentity");
            }

            bool authenticated = this.verifyPassword(ownerWindow, currentIdentity);
            return new AuthenticationResult(
                authenticated ? AuthenticationOutcome.Succeeded : AuthenticationOutcome.Canceled);
        }

        private async Task<AuthenticationResult> AuthenticateWithWindowsHelloAsync(
            IntPtr ownerWindow,
            WindowsIdentity currentIdentity,
            string windowsHelloMessage,
            bool passwordFallbackEnabled)
        {
            WindowsHelloVerification verification =
                await this.verifyWindowsHello(ownerWindow, windowsHelloMessage);

            if (verification.IsVerified)
            {
                return new AuthenticationResult(AuthenticationOutcome.Succeeded);
            }

            if (verification.IsCanceled)
            {
                return new AuthenticationResult(AuthenticationOutcome.Canceled);
            }

            if (verification.IsUnavailable)
            {
                if (passwordFallbackEnabled)
                {
                    return this.AuthenticateWithPassword(ownerWindow, currentIdentity);
                }

                return new AuthenticationResult(AuthenticationOutcome.Unavailable);
            }

            return new AuthenticationResult(AuthenticationOutcome.Failed);
        }
    }
}
