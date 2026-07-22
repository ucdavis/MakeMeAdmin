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
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Security.Principal;
    using System.Text;

    internal static class CredentialNativeMethods
    {
        private const int ErrorSuccess = 0;
        private const int ErrorCancelled = 1223;
        private const int ErrorInvalidData = 13;
        private const int Logon32ProviderDefault = 0;
        private const int Logon32LogonNetwork = 3;

        private const int CredUiMaxUserNameLength = 513;
        private const int CredUiMaxDomainLength = 337;
        private const int CredUiMaxPasswordLength = 256;

        [Flags]
        private enum CredPackFlags
        {
            None = 0,
            GenericCredentials = 0x4
        }

        [Flags]
        private enum CredUiWindowsFlags
        {
            None = 0,
            Generic = 0x1,
            EnumerateCurrentUser = 0x200
        }

        internal const int PasswordPromptFlags = (int)CredUiWindowsFlags.Generic;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CredUiInfo
        {
            internal int cbSize;
            internal IntPtr hwndParent;
            internal string pszMessageText;
            internal string pszCaptionText;
            internal IntPtr hbmBanner;
        }

        [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredPackAuthenticationBuffer(CredPackFlags flags,
                                                                string userName,
                                                                string password,
                                                                IntPtr packedCredentials,
                                                                ref int packedCredentialsSize);

        [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredUnPackAuthenticationBuffer(CredPackFlags flags,
                                                                  IntPtr authBuffer,
                                                                  uint authBufferSize,
                                                                  StringBuilder userName,
                                                                  ref int maxUserName,
                                                                  StringBuilder domainName,
                                                                  ref int maxDomainName,
                                                                  StringBuilder password,
                                                                  ref int maxPassword);

        [DllImport("credui.dll", CharSet = CharSet.Unicode)]
        private static extern int CredUIPromptForWindowsCredentials(ref CredUiInfo credentialUiInfo,
                                                                    int authenticationError,
                                                                    ref uint authenticationPackage,
                                                                    IntPtr inputAuthBuffer,
                                                                    int inputAuthBufferSize,
                                                                    out IntPtr outputAuthBuffer,
                                                                    out uint outputAuthBufferSize,
                                                                    ref bool saveCredentials,
                                                                    CredUiWindowsFlags flags);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LogonUser(string userName,
                                             string domain,
                                             IntPtr password,
                                             int logonType,
                                             int logonProvider,
                                             ref IntPtr token);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        /// <summary>
        /// Displays a conventional username/password dialog that does not enumerate
        /// Windows Hello credential-provider tiles.
        /// </summary>
        internal static PasswordPromptResult PromptForPassword(IntPtr parentWindow, string userName, int errorCode)
        {
            CredUiInfo credentialUiInfo = new CredUiInfo
            {
                hwndParent = parentWindow,
                pszCaptionText = Properties.Resources.CredentialsPromptCaption,
                pszMessageText = Properties.Resources.CredentialsPromptMessage,
                cbSize = Marshal.SizeOf(typeof(CredUiInfo))
            };

            IntPtr inputCredentialBuffer = IntPtr.Zero;
            int inputCredentialSize = 0;
            IntPtr outputCredentialBuffer = IntPtr.Zero;
            uint outputCredentialSize = 0;
            uint authenticationPackage = 0;
            bool saveCredentials = false;

            try
            {
                CreateInputBuffer(userName, out inputCredentialBuffer, out inputCredentialSize);

                int promptResult = CredUIPromptForWindowsCredentials(ref credentialUiInfo,
                                                                      errorCode,
                                                                      ref authenticationPackage,
                                                                      inputCredentialBuffer,
                                                                      inputCredentialSize,
                                                                      out outputCredentialBuffer,
                                                                      out outputCredentialSize,
                                                                      ref saveCredentials,
                                                                      (CredUiWindowsFlags)PasswordPromptFlags);

                if (promptResult == ErrorCancelled)
                {
                    return PasswordPromptResult.Canceled();
                }

                if (promptResult != ErrorSuccess)
                {
                    return PasswordPromptResult.Error(promptResult);
                }

                return PasswordPromptResult.FromCredentials(UnpackCredentials(outputCredentialBuffer, outputCredentialSize));
            }
            finally
            {
                ZeroAndFreeBuffer(inputCredentialBuffer, inputCredentialSize);
                ZeroAndFreeBuffer(outputCredentialBuffer, outputCredentialSize);
            }
        }

        /// <summary>
        /// Validates the password and verifies the resulting token belongs to the
        /// current caller by comparing SIDs rather than account-name text.
        /// </summary>
        internal static PasswordValidationResult ValidateCredentials(PasswordCredentials credentials, SecurityIdentifier expectedUserSid)
        {
            if ((credentials == null) || (expectedUserSid == null))
            {
                return PasswordValidationResult.Error(ErrorInvalidData);
            }

            string userName = credentials.UserName;
            string domain = credentials.Domain;

            if (string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(userName))
            {
                int slashIndex = userName.IndexOf('\\');
                if (slashIndex >= 0)
                {
                    domain = userName.Substring(0, slashIndex);
                    userName = userName.Substring(slashIndex + 1);
                }
            }

            IntPtr passwordPointer = IntPtr.Zero;
            IntPtr tokenHandle = IntPtr.Zero;

            try
            {
                passwordPointer = Marshal.SecureStringToGlobalAllocUnicode(credentials.Password);

                if (!LogonUser(userName,
                               string.IsNullOrEmpty(domain) ? null : domain,
                               passwordPointer,
                               Logon32LogonNetwork,
                               Logon32ProviderDefault,
                               ref tokenHandle))
                {
                    return PasswordValidationResult.InvalidCredentials(Marshal.GetLastWin32Error());
                }

                using (WindowsIdentity validatedIdentity = new WindowsIdentity(tokenHandle))
                {
                    if (IdentityMatches(validatedIdentity.User, expectedUserSid))
                    {
                        return PasswordValidationResult.Succeeded();
                    }
                }

                return PasswordValidationResult.DifferentUser();
            }
            finally
            {
                if (passwordPointer != IntPtr.Zero)
                {
                    Marshal.ZeroFreeGlobalAllocUnicode(passwordPointer);
                }

                if (tokenHandle != IntPtr.Zero)
                {
                    CloseHandle(tokenHandle);
                }
            }
        }

        internal static bool IdentityMatches(SecurityIdentifier validatedUserSid, SecurityIdentifier expectedUserSid)
        {
            return (validatedUserSid != null) &&
                   (expectedUserSid != null) &&
                   validatedUserSid.Equals(expectedUserSid);
        }

        private static PasswordCredentials UnpackCredentials(IntPtr credentialBuffer, uint credentialBufferSize)
        {
            StringBuilder userName = new StringBuilder(CredUiMaxUserNameLength);
            StringBuilder domain = new StringBuilder(CredUiMaxDomainLength);
            StringBuilder password = new StringBuilder(CredUiMaxPasswordLength);
            int userNameLength = userName.Capacity;
            int domainLength = domain.Capacity;
            int passwordLength = password.Capacity;
            SecureString securePassword = null;

            try
            {
                if (!CredUnPackAuthenticationBuffer(CredPackFlags.None,
                                                    credentialBuffer,
                                                    credentialBufferSize,
                                                    userName,
                                                    ref userNameLength,
                                                    domain,
                                                    ref domainLength,
                                                    password,
                                                    ref passwordLength))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                securePassword = new SecureString();
                for (int index = 0; index < password.Length; index++)
                {
                    securePassword.AppendChar(password[index]);
                }

                securePassword.MakeReadOnly();
                PasswordCredentials credentials = new PasswordCredentials(userName.ToString(), domain.ToString(), securePassword);
                securePassword = null;
                return credentials;
            }
            finally
            {
                ClearStringBuilder(password);
                securePassword?.Dispose();
            }
        }

        private static void CreateInputBuffer(string userName, out IntPtr inputCredentialBuffer, out int inputCredentialSize)
        {
            inputCredentialBuffer = IntPtr.Zero;
            inputCredentialSize = 0;

            if (string.IsNullOrEmpty(userName))
            {
                return;
            }

            CredPackAuthenticationBuffer(CredPackFlags.GenericCredentials,
                                         userName,
                                         string.Empty,
                                         IntPtr.Zero,
                                         ref inputCredentialSize);

            if (inputCredentialSize <= 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            int allocatedCredentialSize = inputCredentialSize;
            inputCredentialBuffer = Marshal.AllocCoTaskMem(allocatedCredentialSize);
            int packedCredentialSize = allocatedCredentialSize;

            if (!CredPackAuthenticationBuffer(CredPackFlags.GenericCredentials,
                                              userName,
                                              string.Empty,
                                              inputCredentialBuffer,
                                              ref packedCredentialSize))
            {
                inputCredentialSize = allocatedCredentialSize;
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            inputCredentialSize = allocatedCredentialSize;
        }

        private static void ZeroAndFreeBuffer(IntPtr buffer, long bufferSize)
        {
            if (buffer == IntPtr.Zero)
            {
                return;
            }

            try
            {
                if (bufferSize > 0)
                {
                    ZeroBuffer(buffer, checked((int)bufferSize));
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(buffer);
            }
        }

        /// <summary>
        /// Overwrites an unmanaged buffer without depending on an operating-system
        /// entry point that may not be exported on every supported Windows build.
        /// </summary>
        internal static void ZeroBuffer(IntPtr buffer, int bufferSize)
        {
            if (buffer == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (bufferSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            for (int index = 0; index < bufferSize; index++)
            {
                Marshal.WriteByte(buffer, index, 0);
            }
        }

        private static void ClearStringBuilder(StringBuilder value)
        {
            if (value == null)
            {
                return;
            }

            for (int index = 0; index < value.Length; index++)
            {
                value[index] = '\0';
            }

            value.Clear();
        }
    }
}
