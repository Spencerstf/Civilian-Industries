using Arcen.AIW2.Core;
using Arcen.Universal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKCivilianIndustry
{
    /// <summary>
    /// Used on militia fleets. Tells us what their focus is.
    /// </summary>
    public class CivilianMilitia
    {
        public int Version;

        // The centerpiece of the flee.t
        public int Centerpiece;

        // The status of the fleet.
        public CivilianMilitiaStatus Status;

        // The planet that this fleet's focused on.
        // It will only interact to hostile forces on or adjacent to this.
        public int PlanetFocus;

        // Wormhole that this fleet has been assigned to. If -1, it will instead find an unclaimed mine on the planet.
        public int EntityFocus;

        // GameEntityTypeData that this militia builds, a list of every ship of that type under their control, and their capacity.
        public Dictionary<int, string> ShipTypeData = new Dictionary<int, string>();
        public Dictionary<int, List<int>> Ships = new Dictionary<int, List<int>>();
        public Dictionary<int, int> ShipCapacity = new Dictionary<int, int>();

        public int GetShipCount(string entityTypeDataInternalName)
        {
            int index = -1;
            for (int x = 0; x < ShipTypeData.Count; x++)
                if (ShipTypeData[x] == entityTypeDataInternalName)
                {
                    index = x;
                    break;
                }
            if (index == -1)
                return 0;
            int shipCount = 0;
            for (int x = 0; x < Ships[index].Count; x++)
            {
                GameEntity_Squad squad = World_AIW2.Instance.GetEntityByID_Squad(Ships[index][x]);
                if (squad == null)
                    continue;
                shipCount++;
                shipCount += squad.ExtraStackedSquadsInThis;
            }
            return shipCount;
        }

        // Multipliers for various things.
        public int CostMultiplier;
        public int CapMultiplier;

        // Following three functions are used for initializing, saving, and loading data.
        // Initialization function.
        // Default values. Called on creation, NOT on load.
        public CivilianMilitia()
        {
            this.Centerpiece = -1;
            this.Status = CivilianMilitiaStatus.Idle;
            this.PlanetFocus = -1;
            this.EntityFocus = -1;
            for (int x = 0; x < (int)CivilianResource.Length; x++)
            {
                this.ShipTypeData.Add(x, "none");
                this.Ships.Add(x, new List<int>());
                this.ShipCapacity.Add(x, 0);
            }
            this.CostMultiplier = 100; // 100%
            this.CapMultiplier = 100; // 100%
        }
        // Saving our data.
        public void SerializeTo(ArcenSerializationBuffer Buffer)
        {
            Buffer.AddItem(1);
            Buffer.AddItem(this.Centerpiece);
            Buffer.AddItem((int)this.Status);
            Buffer.AddItem(this.PlanetFocus);
            Buffer.AddItem(this.EntityFocus);
            int count = (int)CivilianResource.Length;
            Buffer.AddItem(count);
            for (int x = 0; x < count; x++)
            {
                Buffer.AddItem(this.ShipTypeData[x]);
                int subCount = this.Ships[x].Count;
                Buffer.AddItem(subCount);
                for (int y = 0; y < subCount; y++)
                    Buffer.AddItem(this.Ships[x][y]);
                Buffer.AddItem(this.ShipCapacity[x]);
            }
            Buffer.AddItem(this.CostMultiplier);
            Buffer.AddItem(this.CapMultiplier);
        }
        // Loading our data. Make sure the loading order is the same as the saving order.
        public CivilianMilitia(ArcenDeserializationBuffer Buffer)
        {
            this.Version = Buffer.ReadInt32();
            this.Centerpiece = Buffer.ReadInt32();
            this.Status = (CivilianMilitiaStatus)Buffer.ReadInt32();
            this.PlanetFocus = Buffer.ReadInt32();
            this.EntityFocus = Buffer.ReadInt32();
            int count = Buffer.ReadInt32();
            for (int x = 0; x < count; x++)
            {
                this.ShipTypeData.Add(x, Buffer.ReadString());
                this.Ships[x] = new List<int>();
                int subCount = Buffer.ReadInt32();
                for (int y = 0; y < subCount; y++)
                    this.Ships[x].Add(Buffer.ReadInt32());
                this.ShipCapacity[x] = Buffer.ReadInt32();
            }
            if (this.ShipTypeData.Count < (int)CivilianResource.Length)
            {
                for (int x = count; x < (int)CivilianResource.Length; x++)
                {
                    this.ShipTypeData.Add(x, "none");
                    this.Ships.Add(x, new List<int>());
                    this.ShipCapacity.Add(x, 0);
                }
            }
            this.CostMultiplier = Buffer.ReadInt32();
            this.CapMultiplier = Buffer.ReadInt32();
        }

        public GameEntity_Squad getMine()
        {
            return World_AIW2.Instance.GetEntityByID_Squad(this.EntityFocus);
        }

        public GameEntity_Other getWormhole()
        {
            return World_AIW2.Instance.GetEntityByID_Other(this.EntityFocus);
        }
    }
}
