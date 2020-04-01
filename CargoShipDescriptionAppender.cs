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
    /// Used to display stored cargo and the cargo ship's status.
    /// </summary>
    public class CargoShipDescriptionAppender : IGameEntityDescriptionAppender
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
                if (tempData.factionData.CargoShips.Contains(RelatedEntityOrNull.PrimaryKeyID))
                    factionData = tempData.factionData;
            }

            // Load our cargo data.
            CivilianCargo cargoData = RelatedEntityOrNull.GetCivilianCargoExt();
            // Load our status data.
            CivilianStatus shipStatus = RelatedEntityOrNull.GetCivilianStatusExt();

            // Inform them about what the ship is doing.
            Buffer.Add("\nThis ship is currently " + shipStatus.Status.ToString());
            // If currently pathing or enroute, continue to explain towards where
            if (shipStatus.Status == CivilianShipStatus.Enroute)
                Buffer.Add(" towards " + World_AIW2.Instance.GetEntityByID_Squad(shipStatus.Destination).GetQualifiedName() + " on planet " + World_AIW2.Instance.GetEntityByID_Squad(shipStatus.Destination).Planet.Name);
            if (shipStatus.Status == CivilianShipStatus.Pathing)
                Buffer.Add(" towards " + World_AIW2.Instance.GetEntityByID_Squad(shipStatus.Origin).Planet.Name);
            // Inform them about what the ship has on it.
            for (int x = 0; x < cargoData.Amount.Length; x++)
                if (cargoData.Amount[x] > 0)
                {
                    Buffer.StartColor(CivilianResourceHexColors.Color[x]);
                    Buffer.Add($"\n{cargoData.Amount[x]}/{cargoData.Capacity[x]} {((CivilianResource)x).ToString()}");
                    Buffer.EndColor();
                }
            // Add in an empty line to stop any other gunk (such as the fleet display) from messing up our given information.
            Buffer.Add("\n");
            return;
        }
    }
}
