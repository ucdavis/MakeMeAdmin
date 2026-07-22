//
// Copyright © 2010-2025, Sinclair Community College
// Licensed under the GNU General Public License, version 3.
// See the LICENSE file in the project root for full license information.
//

namespace SinclairCC.MakeMeAdmin
{
    /// <summary>
    /// Defines how a local user must verify their identity before requesting
    /// administrator rights.
    /// </summary>
    public enum AuthenticationMode : int
    {
        None = 0,
        Password = 1,
        WindowsHello = 2,
        WindowsHelloWithPasswordFallback = 3
    }
}
