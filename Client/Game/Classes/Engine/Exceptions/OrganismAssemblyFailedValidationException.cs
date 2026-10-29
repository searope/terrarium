//------------------------------------------------------------------------------
//      Copyright (c) Microsoft Corporation.  All rights reserved.                                                              
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using OrganismBase;

namespace Terrarium.Game
{
    /// <summary>
    ///  Exception thrown after an assembly validation has been run
    ///  using the AsmCheck component.  This exception is then used
    ///  to display an error message to the user about which code
    ///  in the assembly needs to be fixed.
    /// </summary>
    [Serializable]
    public class OrganismAssemblyFailedValidationException : GameEngineException
    {
        /// <summary>
        ///  Creates a default assembly validation failure.  No special
        ///  failure information is supplied.
        /// </summary>
        public OrganismAssemblyFailedValidationException()
            : this("Assembly failed validation process.  Please consult rules for allowed constructs.")
        {
        }

        /// <summary>
        ///  Creates an assembly validation failure given a special
        ///  message to supply to the user.
        /// </summary>
        /// <param name="message">The message to supply to the user.</param>
        public OrganismAssemblyFailedValidationException(string message) : base(message)
        {
        }

        /// <summary>
        ///  Creates the exception listing the problems found in the assembly.
        /// </summary>
        /// <param name="problems">What the assembly does that creatures aren't allowed to do.</param>
        public OrganismAssemblyFailedValidationException(IList<string> problems)
            : base("Assembly failed validation process.  Please fix the following:\n\n" +
                   string.Join("\n", problems))
        {
        }

    }
}