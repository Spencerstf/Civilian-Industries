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
        /// <summary>
        /// Version of this class.
        /// </summary>
        public int Version;

        // We have three arrays here.
        // One for current amount, one for capacity, and one for per second change.
        public int[] Amount { get; } = new int[(int)CivilianResource.Length];
        public int[] Capacity { get; } = new int[(int)CivilianResource.Length];
        /// <remarks>
        /// Positive is generation, negative is drain.
        /// </remarks>
        public int[] PerSecond { get; } = new int[(int)CivilianResource.Length];

        // Following three functions are used for initializing, saving, and loading data.
        // Initialization function.
        // Default values. Called on creation, NOT on load.
        public CivilianCargo()
        {
            // Values are set to the default for ships. Stations will manually initialize theirs.
            for (int x = 0; x < this.Amount.Length; x++)
            {
                this.Amount[x] = 0;
                this.Capacity[x] = 100;
                this.PerSecond[x] = 0;
            }
        }

        /// <summary>
        /// Used to save our data.
        /// </summary>
        /// <param name="Buffer"></param>
        public void SerializeTo(ArcenSerializationBuffer Buffer)
        {
            Buffer.AddInt32( ReadStyle.NonNeg, 1 );
            // Arrays
            // Get the number of items in the list, and store that as well.
            // This is so you know how many items you'll have to load later.
            // As we have one entry for each resource, we'll only have to get the count once.
            int count = this.Amount.Length;
            Buffer.AddInt32( ReadStyle.NonNeg, count );
            for (int x = 0; x < count; x++)
            {
                Buffer.AddInt32( ReadStyle.NonNeg, this.Amount[x] );
                Buffer.AddInt32( ReadStyle.NonNeg, this.Capacity[x] );
                Buffer.AddInt32( ReadStyle.Signed, this.PerSecond[x] );
            }
        }

        /// <summary>
        /// Used to load our data from the buffer.
        /// </summary>
        /// <remarks>
        /// Make sure that laoding order is the same as the saving order.</remarks>
        /// <param name="Buffer"></param>
        public CivilianCargo( ArcenDeserializationBuffer Buffer )
        {
            this.Version = Buffer.ReadInt32( ReadStyle.NonNeg );
            // Lists require a special touch to load.
            // We'll have saved the number of items stored up above to be used here to determine the number of items to load.
            // ADDITIONALLY we'll need to recreate our arrays beforehand, as loading does not call the Initialization function.
            // Can't add values to an array that doesn't exist, after all.
            // Its more important to be accurate than it is to be update safe here, so we'll always use our stored value to figure out the number of resources.
            int savedCount = Buffer.ReadInt32( ReadStyle.NonNeg );
            int resourceTypeCount = (int)CivilianResource.Length;
            for ( int x = 0; x < resourceTypeCount; x++ )
            {
                if ( x >= savedCount )
                {
                    this.Amount[x] = 0;
                    this.Capacity[x] = 100;
                    this.PerSecond[x] = 0;
                }
                else
                {
                    this.Amount[x] = Buffer.ReadInt32( ReadStyle.NonNeg );
                    this.Capacity[x] = Buffer.ReadInt32( ReadStyle.NonNeg );
                    this.PerSecond[x] = Buffer.ReadInt32( ReadStyle.Signed );
                }
            }
        }
    }
}
