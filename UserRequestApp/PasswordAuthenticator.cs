// 
// Copyright © 2010-2025, Sinclair Community College
// Licensed under the GNU General Public License, version 3.
// See the LICENSE file in the project root for full license information.
//
// This file is part of Make Me Admin.
//
// Make Me Admin is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, version 3.
//
// Make Me Admin is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Make Me Admin. If not, see <http://www.gnu.org/licenses/>.
//

namespace SinclairCC.MakeMeAdmin
{
    using System;
    using System.ComponentModel;
    using System.Security;
    using System.Security.Principal;

    /// <summary>
    /// Coordinates password-only authentication for the current Windows user.
    /// </summary>
    internal sealed class PasswordAuthenticator
    {
        private const int ErrorInvalidData = 13;
        private const int ErrorLogonFailure = 1326;

        private readonly Func<IntPtr, string, int, PasswordPromptResult> promptForPassword;
        private readonly Func<PasswordCredentials, SecurityIdentifier, PasswordValidationResult> validateCredentials;

        internal PasswordAuthenticator()
            : this(CredentialNativeMethods.PromptForPassword, CredentialNativeMethods.ValidateCredentials)
        {
        }

        internal PasswordAuthenticator(Func<IntPtr, string, int, PasswordPromptResult> promptForPassword,
                                       Func<PasswordCredentials, SecurityIdentifier, PasswordValidationResult> validateCredentials)
        {
            this.promptForPassword = promptForPassword ?? throw new ArgumentNullException(nameof(promptForPassword));
            this.validateCredentials = validateCredentials ?? throw new ArgumentNullException(nameof(validateCredentials));
        }

        /// <summary>
        /// Prompts until the current user's password succeeds or the user cancels.
        /// </summary>
        internal bool AuthenticateCurrentUser(IntPtr parentWindow, WindowsIdentity currentIdentity)
        {
            if ((currentIdentity == null) || (currentIdentity.User == null))
            {
                throw new ArgumentException("The current Windows identity is unavailable.", nameof(currentIdentity));
            }

            int authenticationError = 0;

            while (true)
            {
                PasswordPromptResult promptResult = this.promptForPassword(parentWindow, currentIdentity.Name, authenticationError);

                if (promptResult.Status == PasswordPromptStatus.Canceled)
                {
                    return false;
                }

                if (promptResult.Status == PasswordPromptStatus.Error)
                {
                    throw new Win32Exception(promptResult.ErrorCode);
                }

                if (promptResult.Credentials == null)
                {
                    throw new InvalidOperationException("The credential prompt returned no credentials.");
                }

                PasswordValidationResult validationResult;
                using (promptResult.Credentials)
                {
                    validationResult = this.validateCredentials(promptResult.Credentials, currentIdentity.User);
                }

                switch (validationResult.Status)
                {
                    case PasswordValidationStatus.Succeeded:
                        return true;

                    case PasswordValidationStatus.InvalidCredentials:
                        authenticationError = validationResult.ErrorCode == 0 ? ErrorLogonFailure : validationResult.ErrorCode;
                        break;

                    case PasswordValidationStatus.DifferentUser:
                        authenticationError = ErrorLogonFailure;
                        break;

                    default:
                        throw new Win32Exception(validationResult.ErrorCode == 0 ? ErrorInvalidData : validationResult.ErrorCode);
                }
            }
        }
    }

    internal enum PasswordPromptStatus
    {
        CredentialsProvided,
        Canceled,
        Error
    }

    internal sealed class PasswordPromptResult
    {
        private PasswordPromptResult(PasswordPromptStatus status, PasswordCredentials credentials, int errorCode)
        {
            this.Status = status;
            this.Credentials = credentials;
            this.ErrorCode = errorCode;
        }

        internal PasswordPromptStatus Status { get; }

        internal PasswordCredentials Credentials { get; }

        internal int ErrorCode { get; }

        internal static PasswordPromptResult FromCredentials(PasswordCredentials credentials)
        {
            if (credentials == null)
            {
                throw new ArgumentNullException(nameof(credentials));
            }

            return new PasswordPromptResult(PasswordPromptStatus.CredentialsProvided, credentials, 0);
        }

        internal static PasswordPromptResult Canceled()
        {
            return new PasswordPromptResult(PasswordPromptStatus.Canceled, null, 0);
        }

        internal static PasswordPromptResult Error(int errorCode)
        {
            return new PasswordPromptResult(PasswordPromptStatus.Error, null, errorCode);
        }
    }

    internal enum PasswordValidationStatus
    {
        Error,
        Succeeded,
        InvalidCredentials,
        DifferentUser
    }

    internal struct PasswordValidationResult
    {
        private PasswordValidationResult(PasswordValidationStatus status, int errorCode)
        {
            this.Status = status;
            this.ErrorCode = errorCode;
        }

        internal PasswordValidationStatus Status { get; }

        internal int ErrorCode { get; }

        internal static PasswordValidationResult Succeeded()
        {
            return new PasswordValidationResult(PasswordValidationStatus.Succeeded, 0);
        }

        internal static PasswordValidationResult InvalidCredentials(int errorCode)
        {
            return new PasswordValidationResult(PasswordValidationStatus.InvalidCredentials, errorCode);
        }

        internal static PasswordValidationResult DifferentUser()
        {
            return new PasswordValidationResult(PasswordValidationStatus.DifferentUser, 0);
        }

        internal static PasswordValidationResult Error(int errorCode)
        {
            return new PasswordValidationResult(PasswordValidationStatus.Error, errorCode);
        }
    }

    /// <summary>
    /// Holds a password in a disposable SecureString rather than a managed string.
    /// </summary>
    internal sealed class PasswordCredentials : IDisposable
    {
        private SecureString password;

        internal PasswordCredentials(string userName, string domain, SecureString password)
        {
            this.UserName = userName ?? string.Empty;
            this.Domain = domain ?? string.Empty;
            this.password = password ?? throw new ArgumentNullException(nameof(password));
        }

        internal string UserName { get; }

        internal string Domain { get; }

        internal SecureString Password
        {
            get
            {
                if (this.password == null)
                {
                    throw new ObjectDisposedException(nameof(PasswordCredentials));
                }

                return this.password;
            }
        }

        internal bool IsDisposed => this.password == null;

        public void Dispose()
        {
            if (this.password != null)
            {
                this.password.Dispose();
                this.password = null;
            }
        }
    }
}
