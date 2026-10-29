//------------------------------------------------------------------------------
//      Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------------------------

using System;
using System.Reflection;
using System.Runtime.Loader;

namespace Terrarium.Hosting
{
    /// <summary>
    ///  Loads creature assemblies.  Each creature cache has one context, so
    ///  a creature assembly is loaded once and its types keep their identity.
    ///  OrganismBase and the game's own assemblies always come from the copy
    ///  the game is running, so creatures and the game share Animal and Plant.
    /// </summary>
    internal sealed class CreatureLoadContext : AssemblyLoadContext
    {
        private readonly Func<AssemblyName, Assembly> _resolve;

        /// <summary>
        ///  Creates a context.
        /// </summary>
        /// <param name="name">A name for debugging.</param>
        /// <param name="resolve">Finds an assembly a creature references, or returns null for the
        /// default context to resolve it.</param>
        /// <param name="isCollectible">True for a context that is unloaded when no longer used.</param>
        public CreatureLoadContext(string name, Func<AssemblyName, Assembly> resolve, bool isCollectible)
            : base(name, isCollectible)
        {
            _resolve = resolve;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            return _resolve != null ? _resolve(assemblyName) : null;
        }
    }
}
