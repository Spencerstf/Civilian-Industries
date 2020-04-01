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
    /// World storage class. Everything can be found from here.
    /// </summary>
    public class CivilianWorld
    {
        // Version of this class.
        public int Version;

        // Faction indexes with an active civilian industry.
        public List<int> Factions = new List<int>();

        /// <summary>
        /// Indicates whether resources have been already generated.
        /// </summary>
        public bool GeneratedResources = false;

        // Helper function(s).
        // Get the faction that the sent index is for.
        public (bool valid, Faction faction, CivilianFaction factionData) getFactionInfo(int index)
        {
            Faction faction = World_AIW2.Instance.GetFactionByIndex(this.Factions[index]);
            if (faction == null)
            {
                ArcenDebugging.SingleLineQuickDebug("Civilian Industries - Failed to find faction for sent index.");
                return (false, null, null);
            }
            CivilianFaction factionData = faction.GetCivilianFactionExt();
            if (factionData == null)
            {
                ArcenDebugging.SingleLineQuickDebug("Civilian Industries - Failed to load faction data for found faction: " + faction.GetDisplayName());
                return (false, faction, null);
            }
            return (true, faction, factionData);
        }

        // Following two functions are used for saving, and loading data.
        public CivilianWorld() { }

        // Saving our data.
        public void SerializeTo(ArcenSerializationBuffer Buffer)
        {
            Buffer.AddItem(1);
            // Lists require a special touch to save.
            // Get the number of items in the list, and store that as well.
            // This is so you know how many items you'll have to load later.
            int count = this.Factions.Count;
            Buffer.AddItem(count);
            for (int x = 0; x < count; x++)
                Buffer.AddItem(this.Factions[x]);
            Buffer.AddItem(GeneratedResources);
        }
        // Loading our data. Make sure the loading order is the same as the saving order.
        public CivilianWorld(ArcenDeserializationBuffer Buffer)
        {
            Version = Buffer.ReadInt32();
            // Lists require a special touch to load.
            // We'll have saved the number of items stored up above to be used here to determine the number of items to load.
            // ADDITIONALLY we'll need to recreate a blank list beforehand, as loading does not call the Initialization function.
            // Can't add values to a list that doesn't exist, after all.
            this.Factions = new List<int>();
            int count = Buffer.ReadInt32();
            for (int x = 0; x < count; x++)
                this.Factions.Add(Buffer.ReadInt32());
            this.GeneratedResources = Buffer.ReadBool();
        }
    }
}
