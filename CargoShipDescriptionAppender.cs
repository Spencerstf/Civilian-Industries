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
    /// Used to display stored cargo and the cargo ship's status.
    /// </summary>
    public class CargoShipDescriptionAppender : IGameEntityDescriptionAppender
    {
        public void AddToDescriptionBuffer(GameEntity_Squad RelatedEntityOrNull, GameEntityTypeData RelatedEntityTypeData, ArcenDoubleCharacterBuffer Buffer)
        {
            // Make sure we are getting an entity.
            if (RelatedEntityOrNull == null)
                return;

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
