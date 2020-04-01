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
            Buffer.AddItem(1);
            Buffer.AddItem((int)this.Resource);
        }
        /// <summary>
        /// Used to load data. Make sure the loading order is the same as the saving order.
        /// </summary>
        public CivilianPlanet(ArcenDeserializationBuffer Buffer)
        {
            this.Version = Buffer.ReadInt32();
            this.Resource = (CivilianResource)Buffer.ReadInt32();
        }
    }
}
