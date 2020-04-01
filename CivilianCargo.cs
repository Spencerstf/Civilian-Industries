using Arcen.Universal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKCivilianIndustry
{
    /// <summary>
    /// Used on any entity which has resources.
    /// </summary>
    public class CivilianCargo
    {
        // Version of this class.
        public int Version;

        // We have three arrays here.
        // One for current amount, one for capacity, and one for per second change.
        public int[] Amount;
        public int[] Capacity;
        public int[] PerSecond; // Positive is generation, negative is drain.

        // Following three functions are used for initializing, saving, and loading data.
        // Initialization function.
        // Default values. Called on creation, NOT on load.
        public CivilianCargo()
        {
            // Values are set to the default for ships. Stations will manually initialize theirs.
            this.Amount = new int[(int)CivilianResource.Length];
            this.Capacity = new int[(int)CivilianResource.Length];
            this.PerSecond = new int[(int)CivilianResource.Length];
            for (int x = 0; x < this.Amount.Length; x++)
            {
                this.Amount[x] = 0;
                this.Capacity[x] = 100;
                this.PerSecond[x] = 0;
            }
        }
        // Saving our data.
        public void SerializeTo(ArcenSerializationBuffer Buffer)
        {
            Buffer.AddItem(1);
            // Arrays
            // Get the number of items in the list, and store that as well.
            // This is so you know how many items you'll have to load later.
            // As we have one entry for each resource, we'll only have to get the count once.
            int count = this.Amount.Length;
            Buffer.AddItem(count);
            for (int x = 0; x < count; x++)
            {
                Buffer.AddItem(this.Amount[x]);
                Buffer.AddItem(this.Capacity[x]);
                Buffer.AddItem(this.PerSecond[x]);
            }
        }
        // Loading our data. Make sure the loading order is the same as the saving order.
        public CivilianCargo(ArcenDeserializationBuffer Buffer)
        {
            this.Version = Buffer.ReadInt32();
            // Lists require a special touch to load.
            // We'll have saved the number of items stored up above to be used here to determine the number of items to load.
            // ADDITIONALLY we'll need to recreate our arrays beforehand, as loading does not call the Initialization function.
            // Can't add values to an array that doesn't exist, after all.
            // Its more important to be accurate than it is to be update safe here, so we'll always use our stored value to figure out the number of resources.
            int savedCount = Buffer.ReadInt32();
            int realCount = (int)CivilianResource.Length;
            this.Amount = new int[realCount];
            this.Capacity = new int[realCount];
            this.PerSecond = new int[realCount];
            for (int x = 0; x < realCount; x++)
            {
                if (x >= savedCount)
                {
                    this.Amount[x] = 0;
                    this.Capacity[x] = 100;
                    this.PerSecond[x] = 0;
                }
                this.Amount[x] = Buffer.ReadInt32();
                this.Capacity[x] = Buffer.ReadInt32();
                this.PerSecond[x] = Buffer.ReadInt32();
            }
        }
    }
}
