using Arcen.Universal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKCivilianIndustry
{
    public class CivilianPlanet
    {
        /// <summary>
        /// Version of the class.
        /// </summary>
        public int Version;

        /// <summary>
        /// What resource this planet has.
        /// </summary>
        public CivilianResource Resource = CivilianResource.Length;

        public CivilianPlanet() { }

        /// <summary>
        /// Used to save data
        /// </summary>
        public void SerializeTo(ArcenSerializationBuffer Buffer)
        {
            Buffer.AddInt32( ReadStyle.NonNeg, this.Version );
            Buffer.AddByte( ReadStyleByte.Normal, (byte)this.Resource );
        }
        /// <summary>
        /// Used to load data. Make sure the loading order is the same as the saving order.
        /// </summary>
        public CivilianPlanet(ArcenDeserializationBuffer Buffer)
        {
            this.Version = Buffer.ReadInt32( ReadStyle.NonNeg );
            this.Resource = (CivilianResource)Buffer.ReadByte( ReadStyleByte.Normal );
        }
    }
}
