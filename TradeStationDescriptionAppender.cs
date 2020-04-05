﻿using Arcen.AIW2.Core;
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
    /// Used to display stored cargo for trade stations.
    /// </summary>
    public class TradeStationDescriptionAppender : IGameEntityDescriptionAppender
    {
        public void AddToDescriptionBuffer(GameEntity_Squad RelatedEntityOrNull, GameEntityTypeData RelatedEntityTypeData, ArcenDoubleCharacterBuffer Buffer)
        {
            // Make sure we are getting an entity.
            if (RelatedEntityOrNull == null)
                return;
            // Load our cargo data.
            CivilianCargo cargoData = RelatedEntityOrNull.GetCivilianCargoExt();

            // Inform them about what the station has on it.
            for (int x = 0; x < cargoData.Amount.Length; x++)
            {
                // Skip if we're under the minimum tech requirement.
                int unlocked = 0;
                List<TechUpgrade> upgrades = TechUpgradeTable.Instance.Rows;
                for ( int i = 0; i < upgrades.Count; i++ )
                {
                    TechUpgrade upgrade = upgrades[i];
                    if ( upgrade.InternalName == ((CivilianTech)x).ToString() )
                    {
                        unlocked += RelatedEntityOrNull.PlanetFaction.Faction.TechUnlocks[upgrade.RowIndex];
                        unlocked += RelatedEntityOrNull.PlanetFaction.Faction.FreeTechUnlocks[upgrade.RowIndex];
                        unlocked += RelatedEntityOrNull.PlanetFaction.Faction.CalculatedInheritedTechUnlocks[upgrade.RowIndex];
                        break;
                    }
                }
                if ( ((SpecialFaction_SKCivilianIndustry)RelatedEntityOrNull.PlanetFaction.Faction.Implementation).IgnoreResource[x] )
                {
                    if ( cargoData.PerSecond[x] != 0 )
                    {
                        Buffer.StartColor( CivilianResourceHexColors.Color[x] );
                        Buffer.Add( "\n" + ((CivilianResource)x).ToString() );
                        Buffer.EndColor();
                        Buffer.StartColor( UnityEngine.Color.red );
                        Buffer.Add( " is produced here but currently ignored due to tech level." );
                        Buffer.EndColor();
                    }
                }
                else
                {
                    Buffer.StartColor( CivilianResourceHexColors.Color[x] );
                    if ( cargoData.Amount[x] > 0 || cargoData.PerSecond[x] != 0 )
                        Buffer.Add( $"\n{cargoData.Amount[x]}/{cargoData.Capacity[x]} {(CivilianResource)x}" );
                    Buffer.EndColor();
                    // If resource has generation or drain, notify them.
                    if ( cargoData.PerSecond[x] > 0 )
                    {
                        int income = cargoData.PerSecond[x] + RelatedEntityOrNull.CurrentMarkLevel;
                        Buffer.Add( $" +{income} per second" );
                    }
                }
            }

            // Add in an empty line to stop any other gunk (such as the fleet display) from messing up our given information.
            Buffer.Add("\n");
            return;
        }
    }
}
