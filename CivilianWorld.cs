using Arcen.AIW2.Core;
using Arcen.Universal;
using SKCivilianIndustry.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKCivilianIndustry
{
    /// <summary>
    /// World storage class. Everything can be found from here.
    /// </summary>
    public class CivilianWorld
    {
        /// <summary>
        /// Version of this class.
        /// </summary>
        public int Version;

        /// <summary>
        /// Indicates whether resources have been already generated.
        /// </summary>
        public bool GeneratedResources = false;

        // Following two functions are used for saving, and loading data.
        public CivilianWorld() { }

        /// <summary>
        /// Used to save data to buffer.
        /// </summary>
        public void SerializeTo(ArcenSerializationBuffer Buffer)
        {
            Buffer.AddInt32( ReadStyle.NonNeg, 1 );
            Buffer.AddItem( GeneratedResources );
        }

        /// <summary>
        /// Used to load our data.
        /// </summary>
        /// <remarks>
        /// Make sure that loading order is the same as the saving order.
        /// </remarks>
        /// <param name="Buffer"></param>
        public CivilianWorld(ArcenDeserializationBuffer Buffer)
        {
            Version = Buffer.ReadInt32(ReadStyle.NonNeg );
            this.GeneratedResources = Buffer.ReadBool();
        }
    }
}
