//------------------------------------------------------------------------------
//      Copyright (c) Microsoft Corporation.  All rights reserved.                                                             
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using OrganismBase;
using Terrarium.Game;
using Terrarium.Persistence;
using Terrarium.Tools;

namespace Terrarium.Hosting
{
    // Each creature gets wrapped by an OrganismWrapper so that we have
    // a place to put timing and other status information
    internal class OrganismWrapper : ISaveRecord
    {
        private readonly Organism _org;
        private bool _active = true;

        public OrganismWrapper(Organism org)
        {
            _org = org;
        }

        /// <summary>
        ///  Rebuilds a wrapper and its organism from the values GetSaveValues saved.
        /// </summary>
        /// <param name="values">The saved values.</param>
        internal static OrganismWrapper FromSaveValues(IDictionary<string, object> values)
        {
            return new OrganismWrapper(values);
        }

        private OrganismWrapper(IDictionary<string, object> values)
        {
            try
            {
                var t = Type.GetType((string) values["OrganismType"]);
                _org = (Organism) Activator.CreateInstance(t);
            }
            catch (Exception e)
            {
                ErrorLog.LogHandledException(e);

                // Something failed when we tried to deserialize.  This usually means that the
                // animal has been blacklisted and thus their assembly has been nulled out.
                // Replace with an instance of TerrariumOrganism so we don't abort the whole deserialization process.
                // TerrariumOrganism will simply throw an exception when we try to give it a time slice.
                _org = (Organism) Activator.CreateInstance(typeof (TerrariumOrganism));
                return;
            }

            // Give the animal a chance to deserialize itself
            (_org).InternalOrganismDeserialize(new MemoryStream((byte[]) values["OrganismInfo"]));
            if (_org is Animal)
            {
                ((Animal) _org).InternalAnimalDeserialize(new MemoryStream((byte[]) values["AnimalInfo"]));
                _org.SerializedStream = new MemoryStream((byte[]) values["UserInfo"]);
            }
            else
            {
                ((Plant) _org).InternalPlantDeserialize(new MemoryStream((byte[]) values["PlantInfo"]));
                _org.SerializedStream = new MemoryStream((byte[]) values["UserInfo"]);
            }
        }

        public Organism Organism
        {
            get { return _org; }
        }

        /// <summary>
        /// ticks: 100 nanosec intervals
        /// </summary>
        public Int64 Overage { get; set; }

        /// <summary>
        /// ticks: 100 nanosec intervals
        /// </summary>
        public Int64 TotalTime { get; set; }

        public Int64 LastTime { get; set; }

        public Int64 TotalActivations { get; set; }

        public bool Active
        {
            get { return _active; }

            set { _active = value; }
        }

        #region ISaveRecord Members

        void ISaveRecord.GetSaveValues(IDictionary<string, object> info)
        {
            if (_org == null) return;
            info["OrganismType"] = _org.GetType().AssemblyQualifiedName;

            var m = new MemoryStream();
            (_org).InternalOrganismSerialize(m);
            info["OrganismInfo"] = m.ToArray();

            // Let the animal contribute to serialization.  We don't use BinarySerialization
            // here because we don't want animals to be able to put bogus items in their
            // serialization stream that we will deserialize on their behalf later and 
            // cause security problems.
            if (_org is Animal)
            {
                m = new MemoryStream();
                ((Animal) _org).InternalAnimalSerialize(m);
                info["AnimalInfo"] = m.ToArray();

                m = new MemoryStream();

                try
                {
                    ((Animal) _org).SerializeAnimal(m);
                }
                catch (Exception e)
                {
                    ErrorLog.LogHandledException(e);
                    if (GameEngine.Current != null)
                    {
                        GameEngine.Current.OnEngineStateChanged(
                            new EngineStateChangedEventArgs(EngineStateChangeType.Other,
                                                            "Animal Serialization Failure.",
                                                            _org.GetType().Assembly.GetName().Name +
                                                            "was frost bitten while being Cryogenically Frozen (aka Serialized)."));
                    }
                    m = new MemoryStream();
                }
                info["UserInfo"] = m.ToArray();
            }
            else
            {
                m = new MemoryStream();
                ((Plant) _org).InternalPlantSerialize(m);
                info["PlantInfo"] = m.ToArray();

                m = new MemoryStream();
                try
                {
                    ((Plant) _org).SerializePlant(m);
                }
                catch (Exception e)
                {
                    ErrorLog.LogHandledException(e);
                    if (GameEngine.Current != null)
                    {
                        GameEngine.Current.OnEngineStateChanged(
                            new EngineStateChangedEventArgs(EngineStateChangeType.Other,
                                                            "Plant Serialization Failure.",
                                                            _org.GetType().Assembly.GetName().Name +
                                                            "was frost bitten while being Cryogenically Frozen (aka Serialized)."));
                    }
                    m = new MemoryStream();
                }

                info["UserInfo"] = m.ToArray();
            }
        }

        #endregion
    }
}