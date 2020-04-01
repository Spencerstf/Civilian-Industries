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
    /// Used to display faction-related info to the player.
    /// </summary>
    /// <remarks>
    /// Description classes.
    /// Grand stations.
    /// </remarks>
    public class GrandStationDescriptionAppender : IGameEntityDescriptionAppender
    {
        public void AddToDescriptionBuffer(GameEntity_Squad RelatedEntityOrNull, GameEntityTypeData RelatedEntityTypeData, ArcenDoubleCharacterBuffer Buffer)
        {
            // Make sure we are getting an entity.
            if (RelatedEntityOrNull == null)
                return;
            // Need to find our faction data to display information.
            // Look through our world data, first, to find which faction controls our starting station, and load its faction data.
            CivilianWorld worldData = World.Instance.GetCivilianWorldExt();
            CivilianFaction factionData = null;
            // Look through our saved factions to find which one has our starting station
            for (int x = 0; x < worldData.Factions.Count; x++)
            {
                var tempData = worldData.getFactionInfo(x);
                if (tempData.factionData.GrandStation == RelatedEntityOrNull.PrimaryKeyID)
                {
                    factionData = tempData.factionData;
                }
            }

            // If we found our faction data, inform them about build requests in the faction
            if (factionData != null)
            {
                int baseCost = factionData.GetResourceCost(RelatedEntityOrNull.PlanetFaction.Faction);
                int cargoCost = (int)(baseCost + (baseCost * (factionData.CargoShips.Count / 10.0)));
                int percForCargo = (int)Math.Min(100, 100.0 * factionData.BuildCounter / cargoCost);
                int militiaCost = (int)(baseCost + (baseCost * (factionData.MilitiaLeaders.Count / 10.0)));
                int percForMilitia = (int)Math.Min(100, 100.0 * factionData.MilitiaCounter / militiaCost);
                Buffer.Add($"\n{percForCargo}% to next Cargo Ship.");
                Buffer.Add($"\n{percForMilitia}% to next Militia Construction Ship.");
            }

            // Add in an empty line to stop any other gunk (such as the fleet display) from messing up our given information.
            Buffer.Add("\n");
            return;
        }
    }
}
