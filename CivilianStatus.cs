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
        public int Version;

        // The ship's current status.
        public CivilianShipStatus Status;

        // The index of the requesting station.
        // If -1, its being sent from the grand station.
        public int Origin;

        // The index of the ship's destination station, if any.
        public int Destination;

        // The amount of time left before departing from a loading job.
        // Usually 2 minutes.
        public int LoadTimer;

        // Following three functions are used for initializing, saving, and loading data.
        // Initialization function.
        // Default values. Called on creation, NOT on load.
        public CivilianStatus()
        {
            this.Status = CivilianShipStatus.Idle;
            this.Origin = -1;
            this.Destination = -1;
            this.LoadTimer = 0;
        }
        // Saving our data.
        public void SerializeTo(ArcenSerializationBuffer Buffer)
        {
            Buffer.AddItem(1);
            Buffer.AddItem((int)this.Status);
            Buffer.AddItem(this.Origin);
            Buffer.AddItem(this.Destination);
            Buffer.AddItem(this.LoadTimer);
        }
        // Loading our data. Make sure the loading order is the same as the saving order.
        public CivilianStatus(ArcenDeserializationBuffer Buffer)
        {
            this.Version = Buffer.ReadInt32();
            this.Status = (CivilianShipStatus)Buffer.ReadInt32();
            this.Origin = Buffer.ReadInt32();
            this.Destination = Buffer.ReadInt32();
            this.LoadTimer = Buffer.ReadInt32();
        }
    }
}
