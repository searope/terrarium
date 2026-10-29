//------------------------------------------------------------------------------
//      Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------------------------

using System;
using OrganismBase;

namespace Terrarium.Persistence
{
    /// <summary>
    ///  Thrown when a file was saved by an older version of Terrarium, whose
    ///  BinaryFormatter save format can no longer be read.
    /// </summary>
    [Serializable]
    public class OldSaveFileException : GameEngineException
    {
        /// <summary>
        ///  Creates the exception for the given file.
        /// </summary>
        /// <param name="path">The file that couldn't be read.</param>
        public OldSaveFileException(string path)
            : base("'" + path + "' was saved by an older version of Terrarium and can't be opened.")
        {
        }
    }
}
