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
    /// Used to display defensive focuses and militia ship's status.
    /// </summary>
    public class MilitiaShipDescriptionAppender : IGameEntityDescriptionAppender
    {
        public void AddToDescriptionBuffer(GameEntity_Squad RelatedEntityOrNull, GameEntityTypeData RelatedEntityTypeData, ArcenDoubleCharacterBuffer Buffer)
        {
            // Make sure we are getting an entity.
            if (RelatedEntityOrNull == null)
                return;
            // Load our militia data
            CivilianMilitia militiaData = RelatedEntityOrNull.GetCivilianMilitiaExt();
            CivilianCargo cargoData = RelatedEntityOrNull.GetCivilianCargoExt();

            CivilianFaction factionData = RelatedEntityOrNull.PlanetFaction.Faction.GetCivilianFactionExt();

            if (factionData == null )
                return;

            // Inform them about any focus the ship may have.
            GameEntity_Squad centerpiece = World_AIW2.Instance.GetEntityByID_Squad(militiaData.Centerpiece);
            if (centerpiece != null && centerpiece.PrimaryKeyID != RelatedEntityOrNull.PrimaryKeyID)
                Buffer.Add(" This structure is producing ships for " + centerpiece.FleetMembership.Fleet.GetName() + " on the planet " + centerpiece.Planet.Name + ".");
            else
            {
                Planet targetPlanet = World_AIW2.Instance.GetPlanetByIndex(militiaData.PlanetFocus);
                if (targetPlanet != null)
                    Buffer.Add($" This ship's planetary focus is {targetPlanet.Name}");
                else
                    Buffer.Add(" This ship is currently waiting for a protection request.");
            }

            if (militiaData.Ships.Count > 0)
            {
                for (int x = 0; x < (int)CivilianResource.Length; x++)
                {
                    GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRowByName(militiaData.ShipTypeData[x], false, null);

                    if (entityData == null)
                        continue;

                    int count = militiaData.GetShipCount(entityData.InternalName);
                    Buffer.Add($"\n{entityData.DisplayName}:");
                    Buffer.StartColor(UnityEngine.Color.green);
                    Buffer.Add($" {count}/{militiaData.ShipCapacity[x]}");
                    Buffer.EndColor();
                    Buffer.StartColor(CivilianResourceHexColors.Color[x]);
                    Buffer.Add($" ({(CivilianResource)x})");
                    Buffer.EndColor();

                    int cost;
                    if (RelatedEntityOrNull.TypeData.GetHasTag("BuildsProtectors"))
                        cost = (int)(12000 * SpecialFaction_SKCivilianIndustry.CostIntensityModifier(RelatedEntityOrNull.PlanetFaction.Faction));
                    else
                    {
                        double countCostModifier = 1.0 + (1.0 - ((militiaData.ShipCapacity[x] - count + 1.0) / militiaData.ShipCapacity[x]));
                        int baseCost = entityData.CostForAIToPurchase;
                        cost = (int)(SpecialFaction_SKCivilianIndustry.CostIntensityModifier(RelatedEntityOrNull.PlanetFaction.Faction) * (baseCost * countCostModifier * (militiaData.CostMultiplier / 100.0)));
                    }

                    if (count < militiaData.ShipCapacity[x])
                    {
                        double perc = Math.Min(100, 100.0 * (1.0 * cargoData.Amount[x] / cost));
                        Buffer.Add($" {perc.ToString("0.##")}% (Building)");
                    }
                    else
                    {
                        double perc = Math.Min(100, 100.0 * (1.0 * cargoData.Amount[x] / cargoData.Capacity[x]));
                        Buffer.Add($" {perc.ToString("0.##")}% (Stockpiling)");
                    }
                    Buffer.EndColor();
                }
            }

            // Add in an empty line to stop any other gunk (such as the fleet display) from messing up our given information.
            Buffer.Add("\n");
            return;
        }
    }
}
