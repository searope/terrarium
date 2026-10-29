//------------------------------------------------------------------------------
//      Copyright (c) Microsoft Corporation.  All rights reserved.                                                               
//------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Reflection;
using System.Security;

namespace Terrarium.Hosting
{
    /// <summary>
    ///  Encapsulates a series of functions that can be used to
    ///  query and modify security.
    /// </summary>
    public class SecurityUtils
    {
        /// <summary>
        ///  Find out if a managed debugger is attached.
        /// </summary>
        public static bool IsDebuggerAttached
        {
            get { return Debugger.IsAttached; }
        }

        /// <summary>
        ///  Determine if the given assembly has a terrarium key.
        /// </summary>
        /// <param name="asmName">The assembly to check.</param>
        /// <returns>True if the assembly has the Terrarium key, false otherwise.</returns>
        public static bool AssemblyHasTerrariumKey(AssemblyName asmName)
        {
            var terrKey = Assembly.GetExecutingAssembly().GetName().GetPublicKeyToken();
            var checkKey = asmName.GetPublicKeyToken();

            if (terrKey == null)
            {
                throw new SecurityException("Terrarium must be signed");
            }

            // bugs don't have to be signed, so it's OK it is has no key
            if ((checkKey == null) || (checkKey.Length != terrKey.Length))
            {
                return false;
            }

            if (checkKey.Length == terrKey.Length)
            {
                for (var i = 0; i < terrKey.Length; i++)
                {
                    if (checkKey[i] != terrKey[i])
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}