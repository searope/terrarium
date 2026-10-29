//------------------------------------------------------------------------------
//      Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using OrganismBase;

namespace Terrarium.Game
{
    /// <summary>
    ///  Carries an organism through a teleporter.  Teleporters send the
    ///  organism back into a random spot of the same world.
    /// </summary>
    internal class TeleportState
    {
        private OrganismState _organismState;

        /// <summary>
        ///  The organism being teleported.
        /// </summary>
        internal Organism Organism { get; set; }

        /// <summary>
        ///  The organism's state when it entered the teleporter.
        /// </summary>
        internal OrganismState OrganismState
        {
            get { return _organismState; }

            set
            {
                Debug.Assert(value.IsImmutable);
                _organismState = value;
            }
        }

        /// <summary>
        ///  The world state the organism left, used to drop organisms that
        ///  arrive after a new game has started.
        /// </summary>
        internal Guid Originator { get; set; }

        /// <summary>
        ///  True when the organism comes back into the world it left.
        /// </summary>
        internal bool TeleportedToSelf { get; set; }
    }
}
