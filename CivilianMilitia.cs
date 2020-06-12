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

        /// <summary>
        /// The centerpiece of the fleet.
        /// </summary>
        public int Centerpiece;

        /// <summary>
        /// Status of the fleet.
        /// </summary>
        public CivilianMilitiaStatus Status;

        /// <summary>
        /// The planet that this fleet is focused on.
        /// It will only interact to hostile forces on or adjacent to this.
        /// </summary>
        public short PlanetFocus;

        /// <summary>
        /// Wormhole that this fleet has been asigned to. If -1 then it will instead find an unclaimed mine on the planet.
        /// </summary>
        public int EntityFocus;

        // GameEntityTypeData that this militia builds, a list of every ship of that type under their control, and their capacity.
        public Dictionary<int, string> ShipTypeData = new Dictionary<int, string>();
        public Dictionary<int, List<int>> Ships = new Dictionary<int, List<int>>();
        public Dictionary<int, int> ShipCapacity = new Dictionary<int, int>();

        /// <summary>
        /// Count the number of ships of a certain type that this militia controls.
        /// </summary>
        /// <param name="entityTypeDataInternalName" >Internal Name of the Entity to count.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Used to save our data to buffer.
        /// </summary>
        /// <param name="Buffer"></param>
        public void SerializeTo(ArcenSerializationBuffer Buffer)
        {
            Buffer.AddInt32( ReadStyle.NonNeg, 2 );
            Buffer.AddInt32( ReadStyle.Signed, this.Centerpiece );
            Buffer.AddByte( ReadStyleByte.Normal, (byte)this.Status );
            Buffer.AddInt16( ReadStyle.Signed, this.PlanetFocus );
            Buffer.AddInt32( ReadStyle.Signed, this.EntityFocus );
            int count = (int)CivilianResource.Length;
            Buffer.AddInt32( ReadStyle.NonNeg, count );
            for (int x = 0; x < count; x++)
            {
                Buffer.AddItem(this.ShipTypeData[x]);
                int subCount = this.Ships[x].Count;
                Buffer.AddInt32( ReadStyle.NonNeg, subCount );
                for ( int y = 0; y < subCount; y++ )
                    Buffer.AddInt32( ReadStyle.NonNeg, this.Ships[x][y] );
                Buffer.AddInt32( ReadStyle.NonNeg, this.ShipCapacity[x] );
            }
            Buffer.AddInt32( ReadStyle.NonNeg, this.CostMultiplier );
            Buffer.AddInt32( ReadStyle.NonNeg, this.CapMultiplier );   
        }

        /// <summary>
        /// Used to load our data from buffer.
        /// </summary>
        /// <param name="Buffer"></param>
        public CivilianMilitia(ArcenDeserializationBuffer Buffer)
        {
            this.Version = Buffer.ReadInt32(ReadStyle.NonNeg );
            this.Centerpiece = Buffer.ReadInt32(ReadStyle.Signed);
            this.Status = (CivilianMilitiaStatus)Buffer.ReadByte( ReadStyleByte.Normal );
            if ( this.Version < 2 )
                this.PlanetFocus = (short)Buffer.ReadInt32( ReadStyle.Signed );
            else
                this.PlanetFocus = Buffer.ReadInt16( ReadStyle.Signed );
            this.EntityFocus = Buffer.ReadInt32(ReadStyle.Signed);
            int count = Buffer.ReadInt32(ReadStyle.NonNeg );
            for (int x = 0; x < count; x++)
            {
                this.ShipTypeData.Add(x, Buffer.ReadString());
                this.Ships[x] = new List<int>();
                int subCount = Buffer.ReadInt32(ReadStyle.NonNeg );
                for (int y = 0; y < subCount; y++)
                    this.Ships[x].Add(Buffer.ReadInt32(ReadStyle.NonNeg ) );
                this.ShipCapacity[x] = Buffer.ReadInt32(ReadStyle.NonNeg );
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
            this.CostMultiplier = Buffer.ReadInt32(ReadStyle.NonNeg );
            this.CapMultiplier = Buffer.ReadInt32(ReadStyle.NonNeg );
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
