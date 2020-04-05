using Arcen.Universal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKCivilianIndustry
{
    /// <summary>
    /// Used on mobile ships. Tells us what they're currently doing.
    /// </summary>
    public class CivilianStatus
    {
        public int Version { get; private set; }

        /// <summary>
        /// The index of requesting station.
        /// If - 1 the it is being sent from the grand station.
        /// </summary>
        public int Origin { get; set; } = -1;

        /// <summary>
        /// The index of the ship's destination station, if any.
        /// </summary>
        public int Destination { get; set; } = -1;

        /// <summary>
        /// The amount of time left before departing from a loading job.
        /// Usually 120 seconds. Value here is interpreted as seconds.
        /// </summary>
        public int LoadTimer { get; set; } = 0;

        public CivilianStatus()
        {

        }

        /// <summary>
        /// Used to save data to the buffer.
        /// </summary>
        /// <param name="Buffer"></param>
        public void SerializeTo(ArcenSerializationBuffer Buffer)
        {
            Buffer.AddItem(2);
            Buffer.AddItem(this.Origin);
            Buffer.AddItem(this.Destination);
            Buffer.AddItem(this.LoadTimer);
        }
        /// <summary>
        /// Loading our data. Make sure the loading order is the same as the saving order.
        /// </summary>
        public CivilianStatus(ArcenDeserializationBuffer Buffer)
        {
            this.Version = Buffer.ReadInt32();
            if ( this.Version < 2 )
                Buffer.ReadInt32();
            this.Origin = Buffer.ReadInt32();
            this.Destination = Buffer.ReadInt32();
            this.LoadTimer = Buffer.ReadInt32();
        }
    }
}
