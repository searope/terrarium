//------------------------------------------------------------------------------
//      Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Terrarium.Persistence
{
    /// <summary>
    ///  Implemented by objects that save themselves as a flat set of named
    ///  values instead of their fields.  Values may be strings, numbers,
    ///  booleans or byte arrays.  The serializer is given a factory that
    ///  rebuilds the object from the same values.
    /// </summary>
    internal interface ISaveRecord
    {
        /// <summary>
        ///  Adds the values that describe this object.
        /// </summary>
        /// <param name="values">The values to save.</param>
        void GetSaveValues(IDictionary<string, object> values);
    }
}
