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
            // Load our faction data.
            CivilianFaction factionData = RelatedEntityOrNull.PlanetFaction.Faction.GetCivilianFactionExt();

            // Inform them what the ship is currently doing.
            Buffer.Add( "\nThis ship is currently " );
            // Idle
            if ( factionData.CargoShipsIdle.Contains( RelatedEntityOrNull.PrimaryKeyID ) )
                Buffer.Add( "Idle." );
            // Pathing
            if ( factionData.CargoShipsPathing.Contains( RelatedEntityOrNull.PrimaryKeyID ) )
            {
                Buffer.Add( "Pathing" );
                GameEntity_Squad target = World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Origin );
                if ( target != null )
                    Buffer.Add( " towards " + target.TypeData.DisplayName + " on " + target.Planet.Name );
                Buffer.Add( "." );
            }
            // Loading
            if ( factionData.CargoShipsLoading.Contains( RelatedEntityOrNull.PrimaryKeyID ) )
            {
                Buffer.Add( "Loading resources" );
                GameEntity_Squad target = World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Origin );
                if ( target != null )
                    Buffer.Add( " from " + target.TypeData.DisplayName + " on " + target.Planet.Name );
                Buffer.Add( "." );
                if ( shipStatus.LoadTimer > 0 )
                    Buffer.Add( " It will automatically depart after " + shipStatus.LoadTimer + " seconds" );
                GameEntity_Squad target2 = World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Destination );
                if ( target2 != null )
                    Buffer.Add( " and head towards " + target2.TypeData.DisplayName + " on " + target2.Planet.Name );
                Buffer.Add( "." );
            }
            // Enroute
            if ( factionData.CargoShipsEnroute.Contains( RelatedEntityOrNull.PrimaryKeyID ) )
            {
                Buffer.Add( "Enroute" );
                GameEntity_Squad target = World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Destination );
                if ( target != null )
                    Buffer.Add( " towards " + target.TypeData.DisplayName + " on " + target.Planet.Name );
                Buffer.Add( "." );
            }
            // Unloading
            if ( factionData.CargoShipsUnloading.Contains( RelatedEntityOrNull.PrimaryKeyID ) )
            {
                Buffer.Add( "Unloading resources" );
                GameEntity_Squad target = World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Destination );
                if ( target != null )
                    Buffer.Add( " onto " + target.TypeData.DisplayName + " on " + target.Planet.Name );
                Buffer.Add( "." );
            }
            // Building
            if ( factionData.CargoShipsBuilding.Contains( RelatedEntityOrNull.PrimaryKeyID ) )
            {
                Buffer.Add( "Building forces" );
                GameEntity_Squad target = World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Destination );
                if ( target != null )
                    Buffer.Add( " at " + target.TypeData.DisplayName + " on " + target.Planet.Name );
                Buffer.Add( "." );
            }

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
