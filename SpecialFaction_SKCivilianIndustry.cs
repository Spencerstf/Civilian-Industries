using Arcen.AIW2.Core;
using Arcen.AIW2.External;
using Arcen.Universal;
using SKCivilianIndustry.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SKCivilianIndustry
{
    // The main faction class.
    public class SpecialFaction_SKCivilianIndustry : BaseSpecialFaction
    {
        // Information required for our faction.
        // General identifier for our faction.
        protected override string TracingName => "SKCivilianIndustry";

        // Let the game know we're going to want to use the DoLongRangePlanning_OnBackgroundNonSimThread_Subclass function.
        // This function is generally used for things that do not need to always run, such as navigation requests.
        protected override bool EverNeedsToRunLongRangePlanning => true;

        // The following can be set to limit the number of times the background thread can be ran.
        //protected override int MinimumSecondsBetweenLongRangePlannings => 5;

        // When was the last time we sent a journel message? To update the player about civies are doing.
        protected ArcenSparseLookup<Planet, int> LastGameSecondForMessageAboutThisPlanet;
        protected ArcenSparseLookup<Planet, int> LastGameSecondForLastTachyonBurstOnThisPlanet;

        // General data used by the faction. Not required, but makes referencing it much easier.
        public CivilianFaction factionData;

        // Constants and/or game settings.
        protected int MinimumOutpostDeploymentRange;
        protected double MilitiaAttackOverkillPercentage;
        protected int SecondsBetweenMilitiaUpgrades;
        protected bool DefensiveBattlestationForces;
        public int MinTechToProcess;
        public bool[] IgnoreResource;

        // Note: We clear all variables on the faction in the constructor.
        // This is the (current) best way to make sure data is not carried between saves, especially statics.
        public SpecialFaction_SKCivilianIndustry() : base()
        {
            factionData = null;
            LastGameSecondForMessageAboutThisPlanet = new ArcenSparseLookup<Planet, int>();
            LastGameSecondForLastTachyonBurstOnThisPlanet = new ArcenSparseLookup<Planet, int>();
            IgnoreResource = new bool[(int)CivilianResource.Length];
        }

        // Scale ship costs based on intensity. 5 is 100%, with a 10% step up or down based on intensity.
        public static double CostIntensityModifier( Faction faction )
        {
            int intensity = faction.Ex_MinorFactionCommon_GetPrimitives().Intensity;
            return 1.5 - (intensity * 0.1);
        }

        // Set up initial relationships.
        public override void SetStartingFactionRelationships( Faction faction )
        {
            base.SetStartingFactionRelationships( faction );

            // Start by becoming hostile to everybody.
            enemyThisFactionToAll( faction );

            // Than do our intial relationship step.
            UpdateAllegiance( faction );
        }

        // Update relationships.
        protected virtual void UpdateAllegiance( Faction faction )
        {
            switch ( faction.Ex_MinorFactionCommon_GetPrimitives().Allegiance )
            {
                case "AI Team":
                    allyThisFactionToAI( faction );
                    break;
                case "Minor Faction Team Red":
                case "Minor Faction Team Blue":
                case "Minor Faction Team Green":
                    allyThisFactionToMinorFactionTeam( faction, faction.Ex_MinorFactionCommon_GetPrimitives().Allegiance );
                    break;
                default:
                    allyThisFactionToHumans( faction );
                    break;
            }
        }

        // Handle stack splitting logic.
        public override void DoOnStackSplit( GameEntity_Squad originalSquad, GameEntity_Squad newSquad )
        {
            // If we have no world data, uh-oh, we won't be able to find where they're supposed to go.
            // Eeventually, add some sort of fallback logic for militia ships. For now, just skip em.
            if ( factionData != null )
            {
                for ( int y = 0; y < factionData.MilitiaLeaders.Count; y++ )
                {
                    GameEntity_Squad militiaLeader = World_AIW2.Instance.GetEntityByID_Squad( factionData.MilitiaLeaders[y] );
                    if ( militiaLeader == null )
                        continue;
                    CivilianMilitia militiaData = militiaLeader.GetCivilianMilitiaExt();
                    if ( militiaData.Ships != null )
                    {
                        for ( int z = 0; z < militiaData.Ships.Count; z++ )
                        {
                            if ( militiaData.Ships[z].Contains( originalSquad.PrimaryKeyID ) )
                            {
                                militiaData.Ships[z].Add( newSquad.PrimaryKeyID );
                            }
                        }
                    }
                }
            }
        }

        // Handle the creation of the Grand Station.
        public void CreateGrandStation( Faction faction, Faction alignedFaction, ArcenSimContext Context )
        {
            // If human or ai alligned, spawn based on king units.
            if ( alignedFaction.Type == FactionType.Player || alignedFaction.Type == FactionType.AI )
            {
                World_AIW2.Instance.DoForEntities( EntityRollupType.KingUnitsOnly, delegate ( GameEntity_Squad kingEntity )
                {
                    // Make sure its the correct faction.
                    if ( kingEntity.PlanetFaction.Faction.FactionIndex != alignedFaction.FactionIndex )
                        return DelReturn.Continue;

                    // Load in our Grand Station's TypeData.
                    GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "GrandStation" );

                    // Get the total radius of both our grand station and the king unit.
                    // This will be used to find a safe spawning location.
                    int radius = entityData.ForMark[Balance_MarkLevelTable.MaxOrdinal].Radius + kingEntity.TypeData.ForMark[Balance_MarkLevelTable.MaxOrdinal].Radius;

                    // Get the spawning coordinates for our start station.
                    ArcenPoint spawnPoint = ArcenPoint.ZeroZeroPoint;
                    int outerMax = 0;
                    do
                    {
                        outerMax++;
                        spawnPoint = kingEntity.Planet.GetSafePlacementPoint( Context, entityData, kingEntity.WorldLocation, radius, radius * outerMax );
                    } while ( spawnPoint == ArcenPoint.ZeroZeroPoint );

                    // Get the planetary faction to spawn our station in as.
                    PlanetFaction pFaction = kingEntity.Planet.GetPlanetFactionForFaction( faction );

                    // Spawn in the station.
                    GameEntity_Squad grandStation = GameEntity_Squad.CreateNew( pFaction, entityData, entityData.MarkFor( pFaction ), pFaction.FleetUsedAtPlanet, 0, spawnPoint, Context );

                    // Add in our grand station to our faction's data
                    factionData.GrandStation = grandStation.PrimaryKeyID;

                    return DelReturn.Break;
                } );
                return;
            }
            else
            {
                // Not player or ai, see if they have a 'safe' planet for us to spawn on.
                Galaxy galaxy = World_AIW2.Instance.Galaxies[0];
                for ( int x = 0; x < galaxy.Planets.Count; x++ )
                {
                    Planet workingPlanet = galaxy.Planets[x];
                    LongRangePlanningData_PlanetFaction workingData = workingPlanet.LongRangePlanningData.PlanetFactionDataByIndex[alignedFaction.FactionIndex];
                    if ( workingData == null )
                        break;
                    if ( workingData.DataByStance[FactionStance.Self].TotalStrength / 2 > workingData.DataByStance[FactionStance.Hostile].TotalStrength )
                    {
                        // Found a planet that they have majority control over. Spawn around a strong stationary friendly unit.
                        GameEntity_Squad bestEntity = null;
                        bool foundCenterpiece = false, foundStationary = false;
                        PlanetFaction workingPFaction = workingPlanet.GetPlanetFactionForFaction( alignedFaction );
                        workingPFaction.Entities.DoForEntities( delegate ( GameEntity_Squad allignedSquad )
                        {
                            // Default to the first if stationary.
                            if ( bestEntity == null && !allignedSquad.TypeData.IsMobile )
                                bestEntity = allignedSquad;

                            // If found is a centerpiece, pick it.
                            if ( allignedSquad.TypeData.SpecialType == SpecialEntityType.NPCFactionCenterpiece )
                            {
                                if ( !foundCenterpiece )
                                {
                                    bestEntity = allignedSquad;
                                    foundCenterpiece = true;
                                }
                                else if ( allignedSquad.GetStrengthOfSelfAndContents() > bestEntity.GetStrengthOfSelfAndContents() )
                                    bestEntity = allignedSquad;
                            }
                            else if ( !foundCenterpiece )
                            {
                                // No centerpiece, default to strongest, preferring stationary.
                                if ( !allignedSquad.TypeData.IsMobile )
                                {
                                    if ( !foundStationary )
                                    {
                                        bestEntity = allignedSquad;
                                        foundStationary = true;
                                    }
                                    else if ( allignedSquad.GetStrengthOfSelfAndContents() > bestEntity.GetStrengthOfSelfAndContents() )
                                        bestEntity = allignedSquad;
                                }
                            }

                            return DelReturn.Continue;
                        } );

                        if ( bestEntity == null )
                            continue;

                        // Load in our Grand Station's TypeData.
                        GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "GrandStation" );

                        // Get the total radius of both our grand station and the king unit.
                        // This will be used to find a safe spawning location.
                        int radius = entityData.ForMark[Balance_MarkLevelTable.MaxOrdinal].Radius + bestEntity.TypeData.ForMark[Balance_MarkLevelTable.MaxOrdinal].Radius;

                        // Get the spawning coordinates for our start station.
                        ArcenPoint spawnPoint = ArcenPoint.ZeroZeroPoint;
                        int outerMax = 0;
                        do
                        {
                            outerMax++;
                            spawnPoint = bestEntity.Planet.GetSafePlacementPoint( Context, entityData, bestEntity.WorldLocation, radius, radius * outerMax );
                        } while ( spawnPoint == ArcenPoint.ZeroZeroPoint );

                        // Get the planetary faction to spawn our station in as.
                        PlanetFaction pFaction = bestEntity.Planet.GetPlanetFactionForFaction( faction );

                        // Spawn in the station.
                        GameEntity_Squad grandStation = GameEntity_Squad.CreateNew( pFaction, entityData, entityData.MarkFor( pFaction ), pFaction.FleetUsedAtPlanet, 0, spawnPoint, Context );

                        // Add in our grand station to our faction's data
                        factionData.GrandStation = grandStation.PrimaryKeyID;

                        return;
                    }
                }
            }
        }

        // Handle creation of trade stations.
        public void CreateTradeStations( Faction faction, Faction alignedFaction, ArcenSimContext Context )
        {
            // If human or ai alligned, spawn based on command stations.
            if ( alignedFaction.Type == FactionType.Player || alignedFaction.Type == FactionType.AI )
                alignedFaction.Entities.DoForEntities( EntityRollupType.CommandStation, delegate ( GameEntity_Squad commandStation )
                {
                    // Skip if its not currently ready.
                    if ( commandStation.SecondsSpentAsRemains > 0 )
                        return DelReturn.Continue;
                    if ( commandStation.RepairDelaySeconds > 0 )
                        return DelReturn.Continue;
                    if ( commandStation.SelfBuildingMetalRemaining > FInt.Zero )
                        return DelReturn.Continue;
                    if ( factionData.GetTradeStationRebuildTimer( commandStation.Planet ) > 0 )
                        return DelReturn.Continue;

                    // Get the commandStation's planet.
                    Planet planet = commandStation.Planet;
                    if ( planet == null )
                        return DelReturn.Continue;

                    // Skip if we already have a trade station on the planet.
                    for ( int x = 0; x < factionData.TradeStations.Count; x++ )
                    {
                        GameEntity_Squad station = World_AIW2.Instance.GetEntityByID_Squad( factionData.TradeStations[x] );
                        if ( station == null || station.TypeData.InternalName != "TradeStation" )
                            continue;
                        if ( station.Planet.Index == planet.Index )
                            return DelReturn.Continue;
                    }

                    // No trade station found for this planet. Create one.
                    // Load in our trade station's data.
                    GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "TradeStation" );

                    // Get the total radius of both our trade station, and the command station.
                    // This will be used to find a safe spawning location.
                    int radius = entityData.ForMark[Balance_MarkLevelTable.MaxOrdinal].Radius + commandStation.TypeData.ForMark[Balance_MarkLevelTable.MaxOrdinal].Radius;

                    // Get the spawning coordinates for our trade station.
                    ArcenPoint spawnPoint = ArcenPoint.ZeroZeroPoint;
                    int outerMax = 0;
                    do
                    {
                        outerMax++;
                        spawnPoint = planet.GetSafePlacementPoint( Context, entityData, commandStation.WorldLocation, radius, radius * outerMax );
                    } while ( spawnPoint == ArcenPoint.ZeroZeroPoint );

                    // Get the planetary faction to spawn our trade station in as.
                    PlanetFaction pFaction = planet.GetPlanetFactionForFaction( faction );

                    // Spawn in the station's construction point.
                    GameEntity_Squad tradeStation = GameEntity_Squad.CreateNew( pFaction, entityData, entityData.MarkFor( pFaction ), pFaction.FleetUsedAtPlanet, 0, spawnPoint, Context );

                    // Add in our trade station to our faction's data
                    factionData.TradeStations.Add( tradeStation.PrimaryKeyID );

                    // Initialize cargo.
                    CivilianCargo tradeCargo = tradeStation.GetCivilianCargoExt();
                    // Large capacity.
                    for ( int y = 0; y < tradeCargo.Capacity.Length; y++ )
                        tradeCargo.Capacity[y] *= 25;
                    // Give resources based on mine count.
                    int mines = 0;
                    tradeStation.Planet.DoForEntities( EntityRollupType.MetalProducers, delegate ( GameEntity_Squad mineEntity )
                    {
                        if ( mineEntity.TypeData.GetHasTag( "MetalGenerator" ) )
                            mines++;

                        return DelReturn.Continue;
                    } );
                    tradeCargo.PerSecond[(int)planet.GetCivilianPlanetExt().Resource] = (int)(mines * 1.5);

                    // Remove rebuild counter, if applicable.
                    if ( factionData.TradeStationRebuildTimerInSecondsByPlanet.ContainsKey( commandStation.Planet.Index ) )
                        factionData.TradeStationRebuildTimerInSecondsByPlanet.Remove( commandStation.Planet.Index );

                    return DelReturn.Continue;
                } );
            else
            {
                // Not player or ai, see if they have a 'safe' planet for us to spawn on.
                Galaxy galaxy = World_AIW2.Instance.Galaxies[0];
                for ( int x = 0; x < galaxy.Planets.Count; x++ )
                {
                    Planet workingPlanet = galaxy.Planets[x];

                    // Skip if we already have a trade station on the planet.
                    bool alreadyBuilt = false;
                    for ( int y = 0; y < factionData.TradeStations.Count; y++ )
                    {
                        GameEntity_Squad station = World_AIW2.Instance.GetEntityByID_Squad( factionData.TradeStations[y] );
                        if ( station == null )
                            continue;
                        if ( station.Planet.Index == workingPlanet.Index )
                        {
                            alreadyBuilt = true;
                            break;
                        }
                    }

                    if ( alreadyBuilt )
                        continue;

                    LongRangePlanningData_PlanetFaction workingData = workingPlanet.LongRangePlanningData.PlanetFactionDataByIndex[alignedFaction.FactionIndex];
                    if ( workingData == null )
                        break;
                    if ( workingData.DataByStance[FactionStance.Self].TotalStrength / 2 > workingData.DataByStance[FactionStance.Hostile].TotalStrength )
                    {
                        // Found a planet that they have majority control over. Spawn around a stationary friendly unit.
                        GameEntity_Squad bestEntity = null;
                        bool foundCenterpiece = false, foundStationary = false;
                        PlanetFaction workingPFaction = workingPlanet.GetPlanetFactionForFaction( alignedFaction );
                        workingPFaction.Entities.DoForEntities( delegate ( GameEntity_Squad allignedSquad )
                        {
                            // Default to the first stationary.
                            if ( bestEntity == null && !allignedSquad.TypeData.IsMobile )
                                bestEntity = allignedSquad;

                            // If found is a centerpiece, pick it.
                            if ( allignedSquad.TypeData.SpecialType == SpecialEntityType.NPCFactionCenterpiece )
                            {
                                if ( !foundCenterpiece )
                                {
                                    bestEntity = allignedSquad;
                                    foundCenterpiece = true;
                                }
                                else if ( allignedSquad.GetStrengthOfSelfAndContents() > bestEntity.GetStrengthOfSelfAndContents() )
                                    bestEntity = allignedSquad;
                            }
                            else if ( !foundCenterpiece )
                            {
                                // No centerpiece, default to strongest, preferring stationary.
                                if ( !allignedSquad.TypeData.IsMobile )
                                {
                                    if ( !foundStationary )
                                    {
                                        bestEntity = allignedSquad;
                                        foundStationary = true;
                                    }
                                    else if ( allignedSquad.GetStrengthOfSelfAndContents() > bestEntity.GetStrengthOfSelfAndContents() )
                                        bestEntity = allignedSquad;
                                }
                            }

                            return DelReturn.Continue;
                        } );

                        if ( bestEntity == null )
                            continue;

                        // No trade station found for this planet. Create one.
                        // Load in our trade station's data.
                        GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "TradeStation" );

                        // Get the total radius of both our trade station, and the command station.
                        // This will be used to find a safe spawning location.
                        int radius = entityData.ForMark[Balance_MarkLevelTable.MaxOrdinal].Radius + bestEntity.TypeData.ForMark[Balance_MarkLevelTable.MaxOrdinal].Radius;

                        // Get the spawning coordinates for our trade station.
                        ArcenPoint spawnPoint = ArcenPoint.ZeroZeroPoint;
                        int outerMax = 0;
                        do
                        {
                            outerMax++;
                            spawnPoint = workingPlanet.GetSafePlacementPoint( Context, entityData, bestEntity.WorldLocation, radius, radius * outerMax );
                        } while ( spawnPoint == ArcenPoint.ZeroZeroPoint );

                        // Get the planetary faction to spawn our trade station in as.
                        PlanetFaction pFaction = workingPlanet.GetPlanetFactionForFaction( faction );

                        // Spawn in the station's construction point.
                        GameEntity_Squad tradeStation = GameEntity_Squad.CreateNew( pFaction, entityData, entityData.MarkFor( pFaction ), pFaction.FleetUsedAtPlanet, 0, spawnPoint, Context );

                        // Add in our trade station to our faction's data
                        factionData.TradeStations.Add( tradeStation.PrimaryKeyID );

                        // Initialize cargo.
                        CivilianCargo tradeCargo = tradeStation.GetCivilianCargoExt();
                        // Large capacity.
                        for ( int y = 0; y < tradeCargo.Capacity.Length; y++ )
                            tradeCargo.Capacity[y] *= 25;
                        // Give resources based on mine count.
                        int mines = 0;
                        tradeStation.Planet.DoForEntities( EntityRollupType.MetalProducers, delegate ( GameEntity_Squad mineEntity )
                        {
                            if ( mineEntity.TypeData.GetHasTag( "MetalGenerator" ) )
                                mines++;

                            return DelReturn.Continue;
                        } );
                        tradeCargo.PerSecond[(int)workingPlanet.GetCivilianPlanetExt().Resource] = (int)(mines * 1.5);

                        // Remove rebuild counter, if applicable.
                        if ( factionData.TradeStationRebuildTimerInSecondsByPlanet.ContainsKey( bestEntity.Planet.Index ) )
                            factionData.TradeStationRebuildTimerInSecondsByPlanet.Remove( bestEntity.Planet.Index );
                    }
                }
            }
        }

        // Add buildings for the player to build.
        public void AddMilitiaBuildings( Faction faction, Faction alignedFaction, ArcenSimContext Context )
        {
            alignedFaction.Entities.DoForEntities( EntityRollupType.Battlestation, delegate ( GameEntity_Squad battlestation )
            {
                if ( battlestation.TypeData.IsBattlestation ) // Will hopefully fix a weird bug where planets could get battlestation buildings.
                {
                    // Add buildings to the battlestation/citadel's build list.
                    GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "MilitiaHeadquarters" );
                    Fleet.Membership mem = battlestation.FleetMembership.Fleet.GetOrAddMembershipGroupBasedOnSquadType_AssumeNoDuplicates( entityData );
                    mem.ExplicitBaseSquadCap = 1;

                    entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "TradePost" );
                    mem = battlestation.FleetMembership.Fleet.GetOrAddMembershipGroupBasedOnSquadType_AssumeNoDuplicates( entityData );
                    mem.ExplicitBaseSquadCap = 3;

                    entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "MilitiaProtectorShipyards" );
                    mem = battlestation.FleetMembership.Fleet.GetOrAddMembershipGroupBasedOnSquadType_AssumeNoDuplicates( entityData );
                    mem.ExplicitBaseSquadCap = 1;
                }

                return DelReturn.Continue;
            } );
            alignedFaction.DoForControlledPlanets( delegate ( Planet planet )
            {
                // Add buildings to the planet's build list.
                GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "MilitiaBarracks" );

                // Attempt to add to the planet's build list.
                Fleet.Membership mem = planet.GetCommandStationOrNull().FleetMembership.Fleet.GetOrAddMembershipGroupBasedOnSquadType_AssumeNoDuplicates( entityData );

                // Set the building caps.
                mem.ExplicitBaseSquadCap = 1;

                // Remove anything that planets shouldn't get.
                entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "MilitiaHeadquarters" );
                mem = planet.GetCommandStationOrNull().FleetMembership.Fleet.GetOrAddMembershipGroupBasedOnSquadType_AssumeNoDuplicates( entityData );
                mem.ExplicitBaseSquadCap = 0;

                entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "TradePost" );
                mem = planet.GetCommandStationOrNull().FleetMembership.Fleet.GetOrAddMembershipGroupBasedOnSquadType_AssumeNoDuplicates( entityData );
                mem.ExplicitBaseSquadCap = 0;

                entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "MilitiaProtectorShipyards" );
                mem = planet.GetCommandStationOrNull().FleetMembership.Fleet.GetOrAddMembershipGroupBasedOnSquadType_AssumeNoDuplicates( entityData );
                mem.ExplicitBaseSquadCap = 0;

                return DelReturn.Continue;
            } );
        }

        // Look for militia buildings placed by the player, and deal with them.
        public void ScanForMilitiaBuildings( Faction faction, Faction alignedFaction, ArcenSimContext Context )
        {
            alignedFaction.Entities.DoForEntities( EntityRollupType.SpecialTypes, delegate ( GameEntity_Squad entity )
            {
                if ( entity.SecondsSpentAsRemains <= 0 && entity.SelfBuildingMetalRemaining <= 0 )
                {
                    if ( entity.TypeData.GetHasTag( "TradePost" ) && !factionData.TradeStations.Contains( entity.PrimaryKeyID ) )
                    {
                        // Trade Post. Add it to our list and give it resources.
                        factionData.TradeStations.Add( entity.PrimaryKeyID );

                        // Initialize cargo.
                        CivilianCargo tradeCargo = entity.GetCivilianCargoExt();
                        // Large capacity.
                        for ( int y = 0; y < tradeCargo.Capacity.Length; y++ )
                            tradeCargo.Capacity[y] *= 25;
                        // Give resources based on mine count.
                        int mines = 0;
                        entity.Planet.DoForEntities( EntityRollupType.MetalProducers, delegate ( GameEntity_Squad mineEntity )
                        {
                            if ( mineEntity.TypeData.GetHasTag( "MetalGenerator" ) )
                                mines++;

                            return DelReturn.Continue;
                        } );
                        tradeCargo.PerSecond[(int)entity.Planet.GetCivilianPlanetExt().Resource] = mines;

                        entity.SetCivilianCargoExt( tradeCargo );
                    }
                    else if ( entity.TypeData.GetHasTag( "MilitiaHeadquarters" ) && !factionData.MilitiaLeaders.Contains( entity.PrimaryKeyID ) )
                    {
                        if ( entity.FleetMembership.Fleet.Centerpiece != null )
                        {
                            // Miltia Headquarters. Add it to our militia list and set it to patrol logic
                            factionData.MilitiaLeaders.Add( entity.PrimaryKeyID );

                            CivilianMilitia militiaStatus = entity.GetCivilianMilitiaExt();

                            militiaStatus.Centerpiece = entity.FleetMembership.Fleet.Centerpiece.PrimaryKeyID;
                            militiaStatus.CapMultiplier = 300; // 300%
                            militiaStatus.CostMultiplier = 33; // 33%

                            militiaStatus.Status = CivilianMilitiaStatus.Patrolling;
                            militiaStatus.PlanetFocus = entity.Planet.Index;

                            entity.SetCivilianMilitiaExt( militiaStatus );
                        }
                    }
                    else if ( entity.TypeData.GetHasTag( "MilitiaProtectorShipyards" ) && !factionData.MilitiaLeaders.Contains( entity.PrimaryKeyID ) )
                    {
                        if ( entity.FleetMembership.Fleet.Centerpiece != null )
                        {
                            // Militia Protector Shipyards. Add it to our militia list and set it to patrol logic.
                            factionData.MilitiaLeaders.Add( entity.PrimaryKeyID );

                            CivilianMilitia militiaStatus = entity.GetCivilianMilitiaExt();

                            militiaStatus.Centerpiece = entity.FleetMembership.Fleet.Centerpiece.PrimaryKeyID;
                            militiaStatus.Status = CivilianMilitiaStatus.Patrolling;

                            entity.SetCivilianMilitiaExt( militiaStatus );
                        }
                    }
                }
                return DelReturn.Continue;
            } );
        }

        // Handle resource processing.
        public void DoResources( Faction faction, ArcenSimContext Context )
        {
            // For every TradeStation we have defined in our faction data, deal with it.
            for ( int x = 0; x < factionData.TradeStations.Count; x++ )
            {
                // Load the entity, and its cargo data.
                GameEntity_Squad entity = World_AIW2.Instance.GetEntityByID_Squad( factionData.TradeStations[x] );
                if ( entity == null )
                {
                    factionData.TradeStations.RemoveAt( x );
                    x--;
                    continue;
                }
                CivilianCargo entityCargo = entity.GetCivilianCargoExt();

                // Deal with its per second values.
                for ( int y = 0; y < entityCargo.PerSecond.Length; y++ )
                {
                    if ( entityCargo.PerSecond[y] != 0 )
                    {
                        // Update the resource, if able.
                        if ( entityCargo.PerSecond[y] > 0 )
                        {
                            int income = entityCargo.PerSecond[y] + entity.CurrentMarkLevel;
                            entityCargo.Amount[y] = Math.Min( entityCargo.Capacity[y], entityCargo.Amount[y] + income );
                        }
                    }
                }

                // Save its resources.
                entity.SetCivilianCargoExt( entityCargo );
            }
        }

        // Handle the creation of ships.
        public void DoShipSpawns( Faction faction, ArcenSimContext Context )
        {
            // Continue only if starting station is valid.
            if ( factionData.GrandStation == -1 || factionData.GrandStation == -2 )
                return;

            // Load our grand station.
            GameEntity_Squad grandStation = World_AIW2.Instance.GetEntityByID_Squad( factionData.GrandStation );

            // Increment our build counter if needed.
            if ( factionData.FailedCounter.Export > 0 )
                factionData.BuildCounter += factionData.FailedCounter.Import;

            // Build a cargo ship if we have enough requests for them.
            if ( factionData.CargoShips.Count < 10 || factionData.BuildCounter >= (factionData.GetResourceCost( faction ) + (factionData.GetResourceCost( faction ) * (factionData.CargoShips.Count / 10.0))) )
            {
                // Load our cargo ship's data.
                GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "CargoShip" );

                // Get the planet faction to spawn it in as.
                PlanetFaction pFaction = grandStation.Planet.GetPlanetFactionForFaction( faction );

                // Get the spawning coordinates for our cargo ship.
                // We'll simply spawn it right on top of our grand station, and it'll dislocate itself.
                ArcenPoint spawnPoint = grandStation.WorldLocation;

                // For each failed export, spawn a ship.
                for ( int x = 0; x < factionData.FailedCounter.Export; x++ )
                {
                    // Spawn in the ship.
                    GameEntity_Squad entity = GameEntity_Squad.CreateNew( pFaction, entityData, entityData.MarkFor( pFaction ), pFaction.FleetUsedAtPlanet, 0, spawnPoint, Context );

                    // Add the cargo ship to our faction data.
                    factionData.CargoShips.Add( entity.PrimaryKeyID );
                    factionData.ChangeCargoShipStatus( entity, "Idle" );
                }

                // Reset the build counter.
                factionData.BuildCounter = 0;
            }

            // Build mitia ship if we have enough requets for them.
            if ( factionData.MilitiaLeaders.Count < 1 || factionData.MilitiaCounter >= (factionData.GetResourceCost( faction ) + (factionData.GetResourceCost( faction ) * (factionData.MilitiaLeaders.Count / 10.0))) )
            {
                // Load our militia ship's data.
                GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "MilitiaConstructionShip" );

                // Get the planet faction to spawn it in as.
                PlanetFaction pFaction = grandStation.Planet.GetPlanetFactionForFaction( faction );

                // Get the spawning coordinates for our militia ship.
                // We'll simply spawn it right on top of our grand station, and it'll dislocate itself.
                ArcenPoint spawnPoint = grandStation.WorldLocation;

                // Spawn in the ship.
                GameEntity_Squad entity = GameEntity_Squad.CreateNew( pFaction, entityData, entityData.MarkFor( pFaction ), pFaction.FleetUsedAtPlanet, 0, spawnPoint, Context );

                // Add the militia ship to our faction data.
                factionData.MilitiaLeaders.Add( entity.PrimaryKeyID );

                // Reset the build counter.
                factionData.MilitiaCounter = 0;
            }
        }

        // Check for ship arrival.
        public void DoShipArrival( Faction faction, ArcenSimContext Context )
        {
            // Pathing logic, detect arrival at trade station.
            for ( int x = 0; x < factionData.CargoShipsEnroute.Count; x++ )
            {
                GameEntity_Squad cargoShip = World_AIW2.Instance.GetEntityByID_Squad( factionData.CargoShipsEnroute[x] );
                if ( cargoShip == null )
                    continue;

                // Load the cargo ship's status.
                CivilianStatus shipStatus = cargoShip.GetCivilianStatusExt();

                // Heading towards destination station
                // Confirm its destination station still exists.
                GameEntity_Squad destinationStation = World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Destination );

                // If station not found, idle the cargo ship.
                if ( destinationStation == null )
                {
                    factionData.ChangeCargoShipStatus( cargoShip, "Idle" );
                    x--;
                    continue;
                }

                // If ship not at destination planet yet, do nothing.
                if ( cargoShip.Planet.Index != destinationStation.Planet.Index )
                    continue;

                // If ship is close to destination station, start unloading.
                if ( cargoShip.GetDistanceTo_ExpensiveAccurate( destinationStation.WorldLocation, true, true ) < 2000 )
                {
                    if ( factionData.TradeStations.Contains( destinationStation.PrimaryKeyID ) )
                    {
                        factionData.ChangeCargoShipStatus( cargoShip, "Unloading" );
                        shipStatus.LoadTimer = 120;
                    }
                    else if ( factionData.MilitiaLeaders.Contains( destinationStation.PrimaryKeyID ) )
                    {
                        factionData.ChangeCargoShipStatus( cargoShip, "Building" );
                        shipStatus.LoadTimer = 120;
                    }
                    else
                    {
                        factionData.ChangeCargoShipStatus( cargoShip, "Idle" );
                    }
                    cargoShip.SetCivilianStatusExt( shipStatus );
                    x--;
                }
            }
            for ( int x = 0; x < factionData.CargoShipsPathing.Count; x++ )
            {
                GameEntity_Squad cargoShip = World_AIW2.Instance.GetEntityByID_Squad( factionData.CargoShipsPathing[x] );
                if ( cargoShip == null )
                    continue;

                // Load the cargo ship's status.
                CivilianStatus shipStatus = cargoShip.GetCivilianStatusExt();

                // Heading towads origin station.
                // Confirm its origin station still exists.
                GameEntity_Squad originStation = World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Origin );

                // If station not found, idle the cargo ship.
                if ( originStation == null )
                {
                    factionData.ChangeCargoShipStatus( cargoShip, "Idle" );
                    x--;
                    continue;
                }

                // If ship not at origin planet yet, do nothing.
                if ( cargoShip.Planet.Index != originStation.Planet.Index )
                    continue;

                // If ship is close to origin station, start loading.
                if ( cargoShip.GetDistanceTo_ExpensiveAccurate( originStation.WorldLocation, true, true ) < 2000 )
                {
                    factionData.ChangeCargoShipStatus( cargoShip, "Loading" );
                    shipStatus.LoadTimer = 120;
                    x--;
                    cargoShip.SetCivilianStatusExt( shipStatus );
                }
            }
        }

        // Handle resource transferring.
        public void DoResourceTransfer( Faction faction, ArcenSimContext Context )
        {
            // Loop through every cargo ship.
            for ( int x = 0; x < factionData.CargoShipsLoading.Count; x++ )
            {
                // Get the ship.
                GameEntity_Squad cargoShip = World_AIW2.Instance.GetEntityByID_Squad( factionData.CargoShipsLoading[x] );
                if ( cargoShip == null )
                    continue;

                // Load the cargo ship's status.
                CivilianStatus shipStatus = cargoShip.GetCivilianStatusExt();

                // Decrease its wait timer.
                shipStatus.LoadTimer--;

                // Load the cargo ship's cargo.
                CivilianCargo shipCargo = cargoShip.GetCivilianCargoExt();

                // Load the origin station and its cargo.
                GameEntity_Squad originStation = World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Origin );
                // If the station has died, free the cargo ship.
                if ( originStation == null )
                {
                    factionData.ChangeCargoShipStatus( cargoShip, "Idle" );
                    x--;
                    continue;
                }
                CivilianCargo originCargo = originStation.GetCivilianCargoExt();

                // Load the destination station and its cargo.
                GameEntity_Squad destinationStation = World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Destination );
                // If the station has died, free the cargo ship.
                if ( destinationStation == null )
                {
                    factionData.ChangeCargoShipStatus( cargoShip, "Idle" );
                    x--;
                    continue;
                }
                CivilianCargo destinationCargo = destinationStation.GetCivilianCargoExt();

                // Send the resources, if the station has any left.
                for ( int y = 0; y < (int)CivilianResource.Length; y++ )
                {
                    // If its something that our destination produces, take none, and, in fact, give back if we have some.
                    if ( destinationCargo.PerSecond[y] > 0 )
                    {
                        if (shipCargo.Amount[y] > 0 && originCargo.Amount[y] < originCargo.Capacity[y] )
                        {
                            shipCargo.Amount[y]--;
                            originCargo.Amount[y]++;
                        }
                    }
                    else
                    {
                        // When loading up resources, we should try to take as much from the station as we can.
                        if ( originCargo.PerSecond[y] <= 0 )
                        {
                            if ( originCargo.Amount[y] > 0 && shipCargo.Amount[y] < shipCargo.Capacity[y] )
                            {
                                shipCargo.Amount[y]++;
                                originCargo.Amount[y]--;
                            }
                        }
                        // Otherwise, do Loading logic.
                        else
                        {
                            // Stop if there are no resources left to load, if its a resource the station uses, or if the ship is full.
                            if ( originCargo.Amount[y] <= 0 || originCargo.PerSecond[y] < 0 || shipCargo.Amount[y] >= shipCargo.Capacity[y] )
                                continue;

                            // Transfer a single resource per second.
                            originCargo.Amount[y]--;
                            shipCargo.Amount[y]++;
                        }
                    }
                }

                // If load timer hit 0, see if we should head out.
                if ( shipStatus.LoadTimer <= 0 )
                {
                    // If none of our resources are full, stop.
                    bool hasEnough = false;
                    for ( int y = 0; y < (int)CivilianResource.Length; y++ )
                        if ( shipCargo.Amount[y] >= shipCargo.Capacity[y] )
                        {
                            hasEnough = true;
                            break;
                        }
                    if ( hasEnough )
                    {
                        factionData.ChangeCargoShipStatus( cargoShip, "Enroute" );
                    }
                    else
                    {
                        factionData.ChangeCargoShipStatus( cargoShip, "Idle" );
                    }
                    shipStatus.LoadTimer = 0;
                    cargoShip.SetCivilianStatusExt( shipStatus );
                    x--;
                }

                // Save the resources.
                originStation.SetCivilianCargoExt( originCargo );
                cargoShip.SetCivilianCargoExt( shipCargo );
            }
            for ( int x = 0; x < factionData.CargoShipsUnloading.Count; x++ )
            {
                // Get the ship.
                GameEntity_Squad cargoShip = World_AIW2.Instance.GetEntityByID_Squad( factionData.CargoShipsUnloading[x] );
                if ( cargoShip == null )
                    continue;

                // Load the cargo ship's status.
                CivilianStatus shipStatus = cargoShip.GetCivilianStatusExt();

                // Load the cargo ship's cargo.
                CivilianCargo shipCargo = cargoShip.GetCivilianCargoExt();

                // Load the destination station and its cargo.
                GameEntity_Squad destinationStation = World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Destination );
                // If the station has died, free the cargo ship.
                if ( destinationStation == null )
                {
                    factionData.ChangeCargoShipStatus( cargoShip, "Idle" );
                    x--;
                    continue;
                }
                CivilianCargo destinationCargo = destinationStation.GetCivilianCargoExt();

                // Send the resources, if the ship has any left.
                // Check for completion as well here.
                bool isFinished = true;
                for ( int y = 0; y < (int)CivilianResource.Length; y++ )
                {
                    // If the station is producing this resource and has over 25% of it, or doesn't produce it but has over 75% of it, give it to the ship, if it has room.
                    if ( ((destinationCargo.PerSecond[y] > 0 && destinationCargo.Amount[y] > destinationCargo.Capacity[y] * 0.25) ||
                        destinationCargo.Amount[y] > destinationCargo.Capacity[y] * 0.75) && shipCargo.Amount[y] < shipCargo.Capacity[y] )
                    {
                        destinationCargo.Amount[y]--;
                        shipCargo.Amount[y]++;
                    }
                    else
                    {
                        // Otherwise, do ship unloading logic.
                        // If empty, do nothing.
                        if ( shipCargo.Amount[y] <= 0 )
                            continue;

                        // If station is full, do nothing.
                        if ( destinationCargo.Amount[y] >= destinationCargo.Capacity[y] )
                            continue;

                        // Transfer a single resource per second.
                        shipCargo.Amount[y]--;
                        destinationCargo.Amount[y]++;
                        isFinished = false;
                    }
                }

                // Save the resources.
                destinationStation.SetCivilianCargoExt( destinationCargo );
                cargoShip.SetCivilianCargoExt( shipCargo );

                // If ship finished, have it go back to being Idle.
                if ( isFinished )
                {
                    factionData.ChangeCargoShipStatus( cargoShip, "Idle" );
                    x--;
                }
            }
            for ( int x = 0; x < factionData.CargoShipsBuilding.Count; x++ )
            {
                // Get the ship.
                GameEntity_Squad cargoShip = World_AIW2.Instance.GetEntityByID_Squad( factionData.CargoShipsBuilding[x] );
                if ( cargoShip == null )
                    continue;

                // Load the cargo ship's status.
                CivilianStatus shipStatus = cargoShip.GetCivilianStatusExt();

                // Load the cargo ship's cargo.
                CivilianCargo shipCargo = cargoShip.GetCivilianCargoExt();

                // Load the destination station and its cargo.
                GameEntity_Squad destinationStation = World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Destination );
                // If the station has died, free the cargo ship.
                if ( destinationStation == null )
                {
                    factionData.ChangeCargoShipStatus( cargoShip, "Idle" );
                    x--;
                    continue;
                }
                CivilianCargo destinationCargo = destinationStation.GetCivilianCargoExt();

                // Send the resources, if the ship has any left.
                // Check for completion as well here.
                bool isFinished = true;
                for ( int y = 0; y < (int)CivilianResource.Length; y++ )
                {
                    // If empty, do nothing.
                    if ( shipCargo.Amount[y] <= 0 )
                        continue;

                    // Stop if at capacity.
                    if ( destinationCargo.Amount[y] > destinationCargo.Capacity[y] )
                        continue;

                    // Transfer a single resource per second.
                    shipCargo.Amount[y]--;
                    destinationCargo.Amount[y]++;
                    isFinished = false;
                }

                // Save the resources.
                destinationStation.SetCivilianCargoExt( destinationCargo );
                cargoShip.SetCivilianCargoExt( shipCargo );

                // If ship finished, have it go back to being Idle.
                if ( isFinished )
                {
                    factionData.ChangeCargoShipStatus( cargoShip, "Idle" );
                    x--;
                }
            }
        }

        // Handle assigning militia to our ThreatReports.
        public void DoMilitiaAssignment( Faction faction, ArcenSimContext Context )
        {
            Engine_Universal.NewTimingsBeingBuilt.StartRememberingFrame( FramePartTimings.TimingType.MainSimThreadNormal, "DoMilitiaAssignment" );
            // Skip if no threat.
            if ( factionData.ThreatReports == null || factionData.ThreatReports.Count == 0 )
                return;

            // Get a list of free militia leaders.
            List<GameEntity_Squad> freeMilitia = new List<GameEntity_Squad>();

            // Find any free militia leaders and add them to our list.
            for ( int x = 0; x < factionData.MilitiaLeaders.Count; x++ )
            {
                GameEntity_Squad militiaLeader = World_AIW2.Instance.GetEntityByID_Squad( factionData.MilitiaLeaders[x] );
                if ( militiaLeader == null )
                    continue;

                CivilianMilitia militiaStatus = militiaLeader.GetCivilianMilitiaExt();
                if ( militiaStatus == null )
                    continue;

                if ( militiaStatus.Status == CivilianMilitiaStatus.Idle )
                    freeMilitia.Add( militiaLeader );
            }

            // Deal with militia requests.
            for ( int x = 0; x < factionData.ThreatReports.Count; x++ )
            {
                var threatReport = factionData.ThreatReports[x].GetThreat();

                // If we ran out of free militia, update our request.
                if ( freeMilitia.Count == 0 )
                {
                    factionData.MilitiaCounter += factionData.TradeStations.Count;
                    break;
                }

                // Skip if we don't have a post on the planet.
                GameEntity_Squad foundTradePost = null;
                // See if we have a trade station or post on the planet.
                for ( int y = 0; y < factionData.TradeStations.Count; y++ )
                {
                    GameEntity_Squad workingStation = World_AIW2.Instance.GetEntityByID_Squad( factionData.TradeStations[y] );
                    if ( workingStation.Planet.Index != factionData.ThreatReports[x].Planet.Index )
                        continue;

                    if ( workingStation == null || workingStation.SecondsSpentAsRemains > 0 ||
                        workingStation.SelfBuildingMetalRemaining > 0 )
                        continue;

                    foundTradePost = workingStation;
                    break;
                }

                if ( foundTradePost == null )
                    continue;

                // See if any wormholes are still unassigned.
                GameEntity_Other foundWormhole = null;
                factionData.ThreatReports[x].Planet.DoForLinkedNeighbors( delegate ( Planet otherPlanet )
                {
                    // Get its wormhole.
                    GameEntity_Other wormhole = factionData.ThreatReports[x].Planet.GetWormholeTo( otherPlanet );
                    if ( wormhole == null )
                        return DelReturn.Continue;

                    // Skip if too close to the post
                    if ( foundTradePost.WorldLocation.GetDistanceTo( wormhole.WorldLocation, true ) <= MinimumOutpostDeploymentRange * 1.5 )
                        return DelReturn.Continue;

                    // If its not been claimed by another militia, claim it.
                    bool claimed = false;
                    for ( int y = 0; y < factionData.MilitiaLeaders.Count; y++ )
                    {
                        GameEntity_Squad tempSquad = World_AIW2.Instance.GetEntityByID_Squad( factionData.MilitiaLeaders[y] );
                        if ( tempSquad == null )
                            continue;
                        if ( tempSquad.GetCivilianMilitiaExt().EntityFocus == wormhole.PrimaryKeyID )
                            claimed = true;
                    }
                    if ( !claimed )
                    {
                        // If its not a hostile wormhole, assign it, but keep trying to find a hostile one.
                        if ( otherPlanet.GetControllingOrInfluencingFaction().GetIsHostileTowards( faction ) )
                        {
                            foundWormhole = wormhole;
                            return DelReturn.Break;
                        }
                        else
                        {
                            foundWormhole = wormhole;
                        }
                    }
                    return DelReturn.Continue;
                } );


                // If no free wormhole, try to find a free mine.
                GameEntity_Squad foundMine = null;
                factionData.ThreatReports[x].Planet.DoForEntities( EntityRollupType.MetalProducers, delegate ( GameEntity_Squad mineEntity )
                {
                    if ( mineEntity.TypeData.GetHasTag( "MetalGenerator" ) )
                    {
                        bool claimed = false;
                        for ( int y = 0; y < factionData.MilitiaLeaders.Count; y++ )
                        {
                            GameEntity_Squad tempSquad = World_AIW2.Instance.GetEntityByID_Squad( factionData.MilitiaLeaders[y] );
                            if ( tempSquad == null )
                                continue;
                            if ( tempSquad.GetCivilianMilitiaExt().EntityFocus == mineEntity.PrimaryKeyID )
                                claimed = true;
                        }
                        if ( !claimed )
                        {
                            foundMine = mineEntity;
                            return DelReturn.Break;
                        }
                    }

                    return DelReturn.Continue;
                } );

                bool advancedShipyardBuilt = false;
                // If no free mine, see if we already have an advanced technology center on the planet, or queued to be built.
                for ( int y = 0; y < factionData.MilitiaLeaders.Count && !advancedShipyardBuilt; y++ )
                {
                    GameEntity_Squad workingMilitia = World_AIW2.Instance.GetEntityByID_Squad( factionData.MilitiaLeaders[y] );
                    if ( workingMilitia != null &&
                        (workingMilitia.Planet.Index == factionData.ThreatReports[x].Planet.Index && workingMilitia.TypeData.GetHasTag( "AdvancedCivilianShipyard" ))
                        || (workingMilitia.GetCivilianMilitiaExt().Status == CivilianMilitiaStatus.PathingForShipyard &&
                        workingMilitia.GetCivilianMilitiaExt().PlanetFocus == factionData.ThreatReports[x].Planet.Index) )
                        advancedShipyardBuilt = true;
                }

                // Stop if nothing is free.
                if ( foundWormhole == null && foundMine == null && advancedShipyardBuilt )
                    continue;

                // Find the closest militia ship. Default to first in the list.
                GameEntity_Squad militia = freeMilitia[0];
                // If there is at least one more ship, find the closest to our planet, and pick that one.
                if ( freeMilitia.Count > 1 )
                {
                    for ( int y = 1; y < freeMilitia.Count; y++ )
                    {
                        if ( freeMilitia[y].Planet.GetHopsTo( factionData.ThreatReports[x].Planet ) < militia.Planet.GetHopsTo( factionData.ThreatReports[x].Planet ) )
                            militia = freeMilitia[y];
                    }
                }
                // Remove our found militia from our list.
                freeMilitia.Remove( militia );

                // Update the militia's status.
                CivilianMilitia militiaStatus = militia.GetCivilianMilitiaExt();
                militiaStatus.PlanetFocus = factionData.ThreatReports[x].Planet.Index;

                // Assign our mine or wormhole.
                if ( foundWormhole != null )
                {
                    militiaStatus.EntityFocus = foundWormhole.PrimaryKeyID;
                    militiaStatus.Status = CivilianMilitiaStatus.PathingForWormhole;
                }
                else if ( foundMine != null )
                {
                    militiaStatus.EntityFocus = foundMine.PrimaryKeyID;
                    militiaStatus.Status = CivilianMilitiaStatus.PathingForMine;
                }
                else
                {
                    militiaStatus.Status = CivilianMilitiaStatus.PathingForShipyard;
                }

                // Save its status.
                militia.SetCivilianMilitiaExt( militiaStatus );
            }
            Engine_Universal.NewTimingsBeingBuilt.FinishRememberingFrame( FramePartTimings.TimingType.MainSimThreadNormal, "DoMilitiaAssignment" );
        }

        // Handle militia deployment and unit building.
        public void DoMilitiaDeployment( Faction faction, ArcenSimContext Context )
        {
            Engine_Universal.NewTimingsBeingBuilt.StartRememberingFrame( FramePartTimings.TimingType.MainSimThreadNormal, "DoMilitiaDeployment" );
            // Handle once for each militia leader.
            List<int> toRemove = new List<int>();
            List<int> toAdd = new List<int>();
            List<int> processed = new List<int>();
            for ( int x = 0; x < factionData.MilitiaLeaders.Count; x++ )
            {
                // Load its ship and status.
                GameEntity_Squad militiaShip = World_AIW2.Instance.GetEntityByID_Squad( factionData.MilitiaLeaders[x] );
                if ( militiaShip == null || processed.Contains( militiaShip.PrimaryKeyID ) )
                {
                    factionData.MilitiaLeaders.RemoveAt( x );
                    x--;
                    continue;
                }
                CivilianMilitia militiaStatus = militiaShip.GetCivilianMilitiaExt();
                CivilianCargo militiaCargo = militiaShip.GetCivilianCargoExt();
                if ( militiaStatus.Status != CivilianMilitiaStatus.Defending && militiaStatus.Status != CivilianMilitiaStatus.Patrolling )
                {
                    // Load its goal.
                    GameEntity_Squad goalStation = null;
                    // Get its planet.
                    Planet planet = World_AIW2.Instance.GetPlanetByIndex( militiaStatus.PlanetFocus );
                    // If planet not found, and not already deployed, idle the militia ship.
                    if ( planet == null && militiaShip.TypeData.IsMobile )
                    {
                        militiaStatus.Status = CivilianMilitiaStatus.Idle;
                        militiaShip.SetCivilianMilitiaExt( militiaStatus );
                        continue;
                    }
                    // Skip if not at planet yet.
                    if ( militiaShip.Planet.Index != militiaStatus.PlanetFocus )
                        continue;
                    // Get its goal's station.
                    planet.DoForEntities( delegate ( GameEntity_Squad entity )
                    {
                        // If we find its index in our records, thats our goal station.
                        if ( factionData.TradeStations.Contains( entity.PrimaryKeyID ) )
                        {
                            goalStation = entity;
                            return DelReturn.Break;
                        }

                        return DelReturn.Continue;
                    } );
                    // If goal station not found, and not already deployed, idle the militia ship.
                    if ( goalStation == null && militiaShip.TypeData.IsMobile )
                    {
                        militiaStatus.Status = CivilianMilitiaStatus.Idle;
                        militiaShip.SetCivilianMilitiaExt( militiaStatus );
                        continue;
                    }

                    // If pathing, check for arrival.
                    if ( militiaStatus.Status == CivilianMilitiaStatus.PathingForMine )
                    {
                        // If nearby, advance status.
                        if ( militiaShip.GetDistanceTo_ExpensiveAccurate( goalStation.WorldLocation, true, true ) < 500 )
                        {
                            militiaStatus.Status = CivilianMilitiaStatus.EnrouteMine;
                        }
                    }
                    else if ( militiaStatus.Status == CivilianMilitiaStatus.PathingForWormhole )
                    {
                        // If nearby, advance status.
                        if ( militiaShip.GetDistanceTo_ExpensiveAccurate( goalStation.WorldLocation, true, true ) < 500 )
                        {
                            militiaStatus.Status = CivilianMilitiaStatus.EnrouteWormhole;
                        }
                    }
                    else if ( militiaStatus.Status == CivilianMilitiaStatus.PathingForShipyard )
                    {
                        if ( militiaShip.GetDistanceTo_ExpensiveAccurate( goalStation.WorldLocation, true, true ) < 500 )
                        {
                            // Prepare its old id to be removed.
                            toRemove.Add( militiaShip.PrimaryKeyID );

                            // Converting to an Advanced Civilian Shipyard, upgrade the fleet status to a mobile patrol status.
                            militiaStatus.Status = CivilianMilitiaStatus.Patrolling;

                            // Load its station data.
                            GameEntityTypeData outpostData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "AdvancedCivilianShipyard" );

                            // Transform it.
                            GameEntity_Squad newMilitiaShip = militiaShip.TransformInto( Context, outpostData, 1 );

                            // Make sure its not overlapping.
                            newMilitiaShip.SetWorldLocation( newMilitiaShip.Planet.GetSafePlacementPoint( Context, outpostData, newMilitiaShip.WorldLocation, 0, 1000 ) );

                            // Update centerpiece to it.
                            militiaStatus.Centerpiece = newMilitiaShip.PrimaryKeyID;

                            // Move the information to our new ship.
                            newMilitiaShip.SetCivilianMilitiaExt( militiaStatus );

                            // Prepare its new id to be added.
                            toAdd.Add( newMilitiaShip.PrimaryKeyID );
                        }
                    }
                    // If enroute, check for sweet spot.
                    if ( militiaStatus.Status == CivilianMilitiaStatus.EnrouteWormhole )
                    {
                        int stationDist = militiaShip.GetDistanceTo_ExpensiveAccurate( goalStation.WorldLocation, true, true );
                        int wormDist = militiaShip.GetDistanceTo_ExpensiveAccurate( militiaStatus.getWormhole().WorldLocation, true, true );
                        int range = MinimumOutpostDeploymentRange;
                        if ( stationDist > range || wormDist < range )
                        {
                            // Prepare its old id to be removed.
                            toRemove.Add( militiaShip.PrimaryKeyID );

                            // Optimal distance. Transform the ship and update its status.
                            militiaStatus.Status = CivilianMilitiaStatus.Defending;

                            // Load its station data.
                            GameEntityTypeData outpostData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "MilitiaOutpost" );

                            // Transform it.
                            GameEntity_Squad newMilitiaShip = militiaShip.TransformInto( Context, outpostData, 1 );

                            // Update centerpiece to it.
                            militiaStatus.Centerpiece = newMilitiaShip.PrimaryKeyID;

                            // Move the information to our new ship.
                            newMilitiaShip.SetCivilianMilitiaExt( militiaStatus );

                            // Prepare its new id to be added.
                            toAdd.Add( newMilitiaShip.PrimaryKeyID );
                        }
                    }
                    // If enroute, check for sweet spot.
                    else if ( militiaStatus.Status == CivilianMilitiaStatus.EnrouteMine )
                    {
                        int mineDist = militiaShip.GetDistanceTo_ExpensiveAccurate( militiaStatus.getMine().WorldLocation, true, true );
                        int range = 1000;
                        if ( mineDist < range )
                        {
                            // Prepare its old id to be removed.
                            toRemove.Add( militiaShip.PrimaryKeyID );

                            // Converting to a Patrol Post, upgrade the fleet status to a mobile patrol status.
                            militiaStatus.Status = CivilianMilitiaStatus.Patrolling;

                            // Load its station data.
                            GameEntityTypeData outpostData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "MilitiaPatrolPost" );

                            // Transform it.
                            GameEntity_Squad newMilitiaShip = militiaShip.TransformInto( Context, outpostData, 1 );

                            // Make sure its not overlapping.
                            newMilitiaShip.SetWorldLocation( newMilitiaShip.Planet.GetSafePlacementPoint( Context, outpostData, newMilitiaShip.WorldLocation, 0, 1000 ) );

                            // Update centerpiece to it.
                            militiaStatus.Centerpiece = newMilitiaShip.PrimaryKeyID;

                            // Move the information to our new ship.
                            newMilitiaShip.SetCivilianMilitiaExt( militiaStatus );

                            // Prepare its new id to be added.
                            toAdd.Add( newMilitiaShip.PrimaryKeyID );
                        }
                    }
                }
                else if ( militiaStatus.Status == CivilianMilitiaStatus.Defending ) // Do turret spawning.
                {
                    // For each type of unit, process.
                    for ( int y = 0; y < (int)CivilianResource.Length; y++ )
                    {
                        if ( militiaCargo.Amount[y] <= 0 )
                            continue;

                        // Skip if we're under the minimum tech requirement.
                        if ( IgnoreResource[y] )
                            continue;

                        // Get our tag to search for based on resource type.
                        string typeTag = "Civ" + ((CivilianTech)y).ToString() + "Turret";

                        if ( militiaStatus.ShipTypeData[y] == "none" )
                        {
                            // Attempt to find entitydata for our type.
                            if ( GameEntityTypeDataTable.Instance.RowsByTag.GetHasKey( typeTag ) )
                            {
                                GameEntityTypeData typeData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, typeTag );
                                if ( typeData != null )
                                    militiaStatus.ShipTypeData[y] = typeData.InternalName;
                            }
                            else
                            {
                                // No matching tag; get a random turret type.
                                GameEntityTypeData typeData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "CivTurret" );
                                if ( typeData != null )
                                    militiaStatus.ShipTypeData[y] = typeData.InternalName;
                            }

                        }

                        // Clear out any dead or stacked units.
                        for ( int z = 0; z < militiaStatus.Ships[y].Count; z++ )
                        {
                            GameEntity_Squad squad = World_AIW2.Instance.GetEntityByID_Squad( militiaStatus.Ships[y][z] );
                            if ( squad == null )
                            {
                                militiaStatus.Ships[y].RemoveAt( z );
                                z--;
                            }
                        }

                        GameEntityTypeData turretData = GameEntityTypeDataTable.Instance.GetRowByName( militiaStatus.ShipTypeData[y], false, null );
                        int count = militiaStatus.GetShipCount( militiaStatus.ShipTypeData[y] );
                        if ( count < militiaStatus.ShipCapacity[y] )
                        {
                            double countCostModifier = 1.0 + (1.0 - ((militiaStatus.ShipCapacity[y] - count + 1.0) / militiaStatus.ShipCapacity[y]));
                            int baseCost = turretData.CostForAIToPurchase;
                            int cost = (int)(CostIntensityModifier( faction ) * (baseCost * countCostModifier * (militiaStatus.CostMultiplier / 100.0)));

                            if ( militiaCargo.Capacity[y] < cost )
                                militiaCargo.Capacity[y] = (int)(cost * 1.25); // Stockpile some resources.

                            if ( militiaCargo.Amount[y] >= cost )
                            {
                                // Remove cost.
                                militiaCargo.Amount[y] -= cost;
                                // Spawn turret.
                                // Get a focal point directed towards the wormhole.
                                ArcenPoint basePoint = militiaShip.WorldLocation.GetPointAtAngleAndDistance( militiaShip.WorldLocation.GetAngleToDegrees( militiaStatus.getWormhole().WorldLocation ), Math.Min( 5000, militiaShip.GetDistanceTo_ExpensiveAccurate( militiaStatus.getWormhole().WorldLocation, true, true ) / 2 ) );
                                // Get a point around it, as close as possible.
                                ArcenPoint spawnPoint = basePoint.GetRandomPointWithinDistance( Context.RandomToUse, Math.Min( 500, militiaShip.GetDistanceTo_ExpensiveAccurate( militiaStatus.getWormhole().WorldLocation, true, true ) / 4 ), Math.Min( 2500, militiaShip.GetDistanceTo_ExpensiveAccurate( militiaStatus.getWormhole().WorldLocation, true, true ) / 2 ) );

                                // Get the planet faction to spawn it in as.
                                PlanetFaction pFaction = militiaShip.Planet.GetPlanetFactionForFaction( faction );

                                // Spawn in the ship.
                                GameEntity_Squad entity = GameEntity_Squad.CreateNew( pFaction, turretData, faction.GetGlobalMarkLevelForShipLine( turretData ), pFaction.FleetUsedAtPlanet, 0, spawnPoint, Context );

                                // Only let it stack with its own fleet.
                                entity.MinorFactionStackingID = militiaShip.PrimaryKeyID;

                                // Add the turret to our militia's fleet.
                                militiaStatus.Ships[y].Add( entity.PrimaryKeyID );
                            }
                        }
                        else if ( count > militiaStatus.ShipCapacity[y] && militiaStatus.Ships[y].Count > 0 )
                        {
                            GameEntity_Squad squad = World_AIW2.Instance.GetEntityByID_Squad( militiaStatus.Ships[y][0] );
                            if ( squad == null )
                                militiaStatus.Ships[y].RemoveAt( 0 );
                            else
                            {
                                squad.Despawn( Context, true, InstancedRendererDeactivationReason.SelfDestructOnTooHighOfCap );
                                militiaStatus.Ships[y].RemoveAt( 0 );
                            }
                        }
                    }
                    // Save our militia's status.
                    militiaShip.SetCivilianMilitiaExt( militiaStatus );
                    militiaShip.SetCivilianCargoExt( militiaCargo );
                }
                else if ( militiaStatus.Status == CivilianMilitiaStatus.Patrolling ) // If patrolling, do unit spawning.
                {
                    // For each type of unit, get ship count.
                    for ( int y = 0; y < (int)CivilianResource.Length; y++ )
                    {
                        if ( militiaCargo.Amount[y] <= 0 )
                            continue;

                        // Skip if we're under the minimum tech requirement.
                        if ( IgnoreResource[y] )
                            continue;

                        // If we're an advanced shipyard, use alternate logic.
                        bool buildingProtectors = false;
                        if ( militiaShip.TypeData.GetHasTag( "BuildsProtectors" ) )
                            buildingProtectors = true;

                        // Get our tag to search for based on resource type.
                        string typeTag = "Civ" + ((CivilianTech)y).ToString() + "Mobile";
                        if ( buildingProtectors )
                            typeTag = "Civ" + ((CivilianTech)y).ToString() + "Protector";

                        if ( militiaStatus.ShipTypeData[y] == "none" )
                        {
                            // Attempt to find entitydata for our type.
                            if ( GameEntityTypeDataTable.Instance.RowsByTag.GetHasKey( typeTag ) )
                            {
                                GameEntityTypeData typeData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, typeTag );
                                if ( typeData != null )
                                    militiaStatus.ShipTypeData[y] = typeData.InternalName;
                            }
                            else
                            {
                                // No matching tag; get a random turret type.
                                GameEntityTypeData typeData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "CivMobile" );
                                if ( typeData != null )
                                    militiaStatus.ShipTypeData[y] = typeData.InternalName;
                            }

                        }

                        // Clear out any dead or stacked units.
                        for ( int z = 0; z < militiaStatus.Ships[y].Count; z++ )
                        {
                            GameEntity_Squad squad = World_AIW2.Instance.GetEntityByID_Squad( militiaStatus.Ships[y][z] );
                            if ( squad == null )
                            {
                                militiaStatus.Ships[y].RemoveAt( z );
                                z--;
                            }
                        }

                        GameEntityTypeData shipData = GameEntityTypeDataTable.Instance.GetRowByName( militiaStatus.ShipTypeData[y], false, null );

                        int count = militiaStatus.GetShipCount( militiaStatus.ShipTypeData[y] );
                        if ( count < militiaStatus.ShipCapacity[y] )
                        {
                            int cost = 0;
                            if ( buildingProtectors )
                                cost = (int)(12000 * CostIntensityModifier( faction ));
                            else
                            {
                                double countCostModifier = 1.0 + (1.0 - ((militiaStatus.ShipCapacity[y] - count + 1.0) / militiaStatus.ShipCapacity[y]));
                                int baseCost = shipData.CostForAIToPurchase;
                                cost = (int)(CostIntensityModifier( faction ) * (baseCost * countCostModifier * (militiaStatus.CostMultiplier / 100.0)));
                            }

                            if ( militiaCargo.Capacity[y] < cost )
                                militiaCargo.Capacity[y] = (int)(cost * 1.25); // Stockpile some resources.

                            if ( militiaCargo.Amount[y] >= cost )
                            {
                                // Spawn ship.
                                // If we're already at our stacking cap, add it directly to a ship via stack.
                                int shipCount = 0, stackingCap = AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "StackingCutoffNPCs" );
                                militiaShip.Planet.DoForEntities( EntityRollupType.MobileCombatants, delegate ( GameEntity_Squad otherSquad )
                                {
                                    if ( otherSquad.TypeData.RowIndex == shipData.RowIndex )
                                    {
                                        shipCount++;
                                        if ( shipCount >= stackingCap )
                                            return DelReturn.Break;
                                    }

                                    return DelReturn.Continue;
                                } );
                                if ( shipCount >= stackingCap && count > 0 )
                                {
                                    // Get a random ship, and add a stack to it.
                                    bool completed = false;
                                    int attempts = 10;
                                    while ( !completed && attempts > 0 )
                                    {
                                        GameEntity_Squad randomSquad = World_AIW2.Instance.GetEntityByID_Squad( militiaStatus.Ships[y][Context.RandomToUse.Next( militiaStatus.Ships[y].Count )] );
                                        if ( randomSquad != null )
                                        {
                                            randomSquad.AddOrSetExtraStackedSquadsInThis( 1, false );
                                            completed = true;
                                            break;
                                        }
                                        attempts--;
                                    }
                                    if ( completed )
                                        // Remove cost.
                                        militiaCargo.Amount[y] -= cost;
                                }
                                else
                                {
                                    // Remove cost.
                                    militiaCargo.Amount[y] -= cost;

                                    // Get the planet faction to spawn it in as.
                                    PlanetFaction pFaction = militiaShip.Planet.GetPlanetFactionForFaction( faction );

                                    // Spawn in the ship.
                                    GameEntity_Squad entity = GameEntity_Squad.CreateNew( pFaction, shipData, faction.GetGlobalMarkLevelForShipLine( shipData ), pFaction.FleetUsedAtPlanet, 0, militiaShip.WorldLocation, Context );

                                    // Only let it stack with its own fleet.
                                    entity.MinorFactionStackingID = militiaShip.PrimaryKeyID;

                                    // Make it attack nearby hostiles.
                                    entity.Orders.SetBehaviorDirectlyInSim( EntityBehaviorType.Attacker_Full, faction.FactionIndex );

                                    // Add the turret to our militia's fleet.
                                    militiaStatus.Ships[y].Add( entity.PrimaryKeyID );
                                }
                            }
                        }
                        else if ( count > militiaStatus.ShipCapacity[y] && militiaStatus.Ships[y].Count > 0 )
                        {
                            GameEntity_Squad squad = World_AIW2.Instance.GetEntityByID_Squad( militiaStatus.Ships[y][0] );
                            if ( squad == null )
                                militiaStatus.Ships[y].RemoveAt( 0 );
                            else
                            {
                                squad.Despawn( Context, true, InstancedRendererDeactivationReason.SelfDestructOnTooHighOfCap );
                                militiaStatus.Ships[y].RemoveAt( 0 );
                            }
                        }
                    }
                    // Save our militia's status.
                    militiaShip.SetCivilianMilitiaExt( militiaStatus );
                    militiaShip.SetCivilianCargoExt( militiaCargo );
                }
                processed.Add( militiaShip.PrimaryKeyID );
            }
            for ( int x = 0; x < toRemove.Count; x++ )
            {
                factionData.MilitiaLeaders.Remove( toRemove[x] );
                factionData.MilitiaLeaders.Add( toAdd[x] );
            }
            Engine_Universal.NewTimingsBeingBuilt.FinishRememberingFrame( FramePartTimings.TimingType.MainSimThreadNormal, "DoMilitiaDeployment" );
        }

        // AI response warning.
        public void PrepareAIRaid( Faction faction, ArcenSimContext Context )
        {
            // If no trade stations, put off the raid.
            if ( factionData.TradeStations == null || factionData.TradeStations.Count == 0 )
            {
                factionData.NextRaidInThisSeconds = 600;
                return;
            }

            // If we're allied to the ai, stop it.
            if ( faction.Ex_MinorFactionCommon_GetPrimitives().Allegiance == "AI Team" )
            {
                factionData.NextRaidInThisSeconds = 6000000;
                return;
            }

            // Pick a random trade station.
            GameEntity_Squad targetStation = World_AIW2.Instance.GetEntityByID_Squad( factionData.TradeStations[Context.RandomToUse.Next( factionData.TradeStations.Count )] );
            if ( targetStation != null )
            {
                Faction aifaction = BadgerFactionUtilityMethods.GetRandomAIFaction( Context );
                if ( aifaction == null )
                    return;

                // Set up raiding wormholes.
                targetStation.Planet.DoForLinkedNeighborsAndSelf( delegate ( Planet planet )
                {
                    // Toss down 2-4 wormholes.
                    int count = Context.RandomToUse.Next( 2, 5 );

                    for ( int x = 0; x < count; x++ )
                    {
                        AngleDegrees angle = AngleDegrees.Create( Context.RandomToUse.NextFloat( x * (360 / count), (x + 1) * (360 / count) ) );
                        if ( angle == null )
                            continue;
                        GameEntityTypeData wormholeData = GameEntityTypeDataTable.Instance.GetRowByName( "AIRaidingWormhole", false, null );
                        if ( wormholeData == null )
                        {
                            ArcenDebugging.ArcenDebugLogSingleLine( "Unable to find wormhole entitydata.", Verbosity.ShowAsError );
                            continue;
                        }
                        ArcenPoint wormholePoint = Engine_AIW2.Instance.CombatCenter.GetPointAtAngleAndDistance( angle,
                            (ExternalConstants.Instance.DistanceScale_GravwellRadius * FInt.FromParts( 0, 900 )).IntValue );
                        if ( wormholePoint == ArcenPoint.ZeroZeroPoint )
                            continue;
                        GameEntity_Squad newWormhole = GameEntity_Squad.CreateNew( planet.GetPlanetFactionForFaction( aifaction ), wormholeData,
                            0, planet.GetPlanetFactionForFaction( aifaction ).FleetUsedAtPlanet, 0, wormholePoint, Context );

                        factionData.NextRaidWormholes.Add( newWormhole.PrimaryKeyID );
                    }

                    return DelReturn.Continue;
                } );

                World_AIW2.Instance.QueueLogMessageCommand( "The AI is preparing to raid cargo ships on planets near " + targetStation.Planet.Name, JournalEntryImportance.KeepLonger );

                // Start timer.
                factionData.NextRaidInThisSeconds = 119;
            }
        }

        // AI response.
        public void SpawnRaidGroup( GameEntity_Squad target, List<int> availableWormholes, ref List<int> attackedTargets, ref int raidBudget, Faction aiFaction, Faction faction, ArcenSimContext Context )
        {
            // Get a random wormhole thats on the target's planet.
            List<GameEntity_Squad> validWormholes = new List<GameEntity_Squad>();
            for ( int x = 0; x < availableWormholes.Count; x++ )
            {
                GameEntity_Squad wormhole = World_AIW2.Instance.GetEntityByID_Squad( availableWormholes[x] );
                if ( wormhole != null && wormhole.Planet.Index == target.Planet.Index )
                    validWormholes.Add( wormhole );
            }
            if ( validWormholes.Count <= 0 )
                return; // No valid wormholes for this target.
            GameEntity_Squad wormholeToSpawnAt = validWormholes[Context.RandomToUse.Next( validWormholes.Count )];
            if ( target != null && target.Planet == wormholeToSpawnAt.Planet && !attackedTargets.Contains( target.PrimaryKeyID ) )
            {
                int thisBudget = 2500;
                raidBudget -= thisBudget;
                // Spawn random fast ships that the ai is allowed to have.
                string[] shipNames = BadgerFactionUtilityMethods.getEntitesInAIShipGroup( AIShipGroupTable.Instance.GetRowByName( "SneakyStrikecraft", false, null ) ).Split( ',' );
                List<GameEntityTypeData> shipTypes = new List<GameEntityTypeData>();
                for ( int y = 0; y < shipNames.Length; y++ )
                {
                    if ( shipNames[y].Trim() != "" )
                    {
                        GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRowByName( shipNames[y].Trim(), false, null );
                        if ( entityData != null )
                            shipTypes.Add( entityData );
                    }
                }

                List<GameEntity_Squad> spawntRaidShips = new List<GameEntity_Squad>();
                ArcenSparseLookup<GameEntityTypeData, int> raidingShips = new ArcenSparseLookup<GameEntityTypeData, int>();
                while ( thisBudget > 0 )
                {
                    GameEntityTypeData workingType = shipTypes[Context.RandomToUse.Next( shipTypes.Count )];
                    if ( !raidingShips.GetHasKey( workingType ) )
                        raidingShips.AddPair( workingType, 1 );
                    else
                        raidingShips[workingType]++;
                    thisBudget -= workingType.CostForAIToPurchase;
                }
                BaseAIFaction.DeployComposition( Context, aiFaction, null, faction.FactionIndex, raidingShips,
                    ref spawntRaidShips, wormholeToSpawnAt.WorldLocation, wormholeToSpawnAt.Planet );

                for ( int shipCount = 0; shipCount < spawntRaidShips.Count; shipCount++ )
                {
                    spawntRaidShips[shipCount].ExoGalacticAttackTarget = SquadWrapper.Create( target );
                    if ( spawntRaidShips[shipCount].ExoGalacticAttackTarget.GetSquad() == null )
                        throw new Exception( "This is probably too paranoid" );

                    spawntRaidShips[shipCount].ExoGalacticAttackPlanetIdx = target.Planet.Index; //set the planet index so that AI long term planning knows we are in an Exo
                }

                GameCommand speedCommand = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.CreateSpeedGroup_ExoAttack], GameCommandSource.AnythingElse );
                for ( int shipCount = 0; shipCount < spawntRaidShips.Count; shipCount++ )
                    speedCommand.RelatedEntityIDs.Add( spawntRaidShips[shipCount].PrimaryKeyID );
                int exoGroupSpeed = 2200;
                speedCommand.RelatedBool = true;
                speedCommand.RelatedIntegers.Add( exoGroupSpeed );
                World_AIW2.Instance.QueueGameCommand( speedCommand, false );

                attackedTargets.Add( target.PrimaryKeyID );
            }
        }
        public void DoAIRaid( Faction faction, ArcenSimContext Context )
        {
            if ( factionData.NextRaidWormholes.Count == 0 )
            {
                PrepareAIRaid( faction, Context );
                return;
            }
            Faction aiFaction = BadgerFactionUtilityMethods.GetRandomAIFaction( Context );

            // Don't attempt to send multiple fleets after a single target.
            List<int> attackedTargets = new List<int>();

            // Raid strength increases based on the AI's normal wave budget, increased by the number of trade stations we have.
            int timeFactor = 900; // Minimum delay between raid waves.
            int budgetFactor = SpecialFaction_AI.Instance.GetSpecificBudgetAIPurchaseCostGainPerSecond( aiFaction, AIBudgetType.Wave, true, true ).GetNearestIntPreferringHigher();
            int tradeFactor = factionData.TradeStations.Count * 3;
            int raidBudget = (budgetFactor + tradeFactor) * timeFactor;

            // Stop once we're over budget. (Though allow our last wave to exceed it if needed.)
            for ( int x = 0; x < factionData.CargoShips.Count && raidBudget > 0; x++ )
            {
                GameEntity_Squad target = World_AIW2.Instance.GetEntityByID_Squad( factionData.CargoShips[x] );
                SpawnRaidGroup( target, factionData.NextRaidWormholes, ref attackedTargets, ref raidBudget, aiFaction, faction, Context );
            }


            // Let the player know they're about to lose money.
            World_AIW2.Instance.QueueLogMessageCommand( "The AI has begun their raid.", JournalEntryImportance.KeepLonger );

            // Reset raid information.
            factionData.NextRaidInThisSeconds = 1800;
            for ( int x = 0; x < factionData.NextRaidWormholes.Count; x++ )
            {
                GameEntity_Squad wormhole = World_AIW2.Instance.GetEntityByID_Squad( factionData.NextRaidWormholes[x] );
                if ( wormhole != null )
                    wormhole.Despawn( Context, true, InstancedRendererDeactivationReason.IFinishedMyJob );
            }
            factionData.NextRaidWormholes = new List<int>();
        }

        // The following function is called once every second. Consider this our 'main' function of sorts, all of our logic is based on this bad boy calling all our pieces every second.
        public override void DoPerSecondLogic_Stage3Main_OnMainThreadAndPartOfSim( Faction faction, ArcenSimContext Context )
        {
            if ( faction.MustBeAwakenedByPlayer && !faction.HasBeenAwakenedByPlayer )
                return;

            // Update settings.
            if ( faction.Ex_MinorFactionCommon_GetPrimitives().Allegiance == "Player Team" )
            {
                MinimumOutpostDeploymentRange = AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "MinimumOutpostDeploymentRange" );
                MilitiaAttackOverkillPercentage = AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "MilitiaAttackOverkillPercentage" ) / 100.0;
                SecondsBetweenMilitiaUpgrades = AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "SecondsBetweenMilitiaUpgrades" );
                MinTechToProcess = AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "MinTechToProcess" );
                DefensiveBattlestationForces = AIWar2GalaxySettingTable.GetIsBoolSettingEnabledByName_DuringGame( "DefensiveBattlestationForces" );
            } else
            {
                MinimumOutpostDeploymentRange = AIWar2GalaxySettingTable.Instance.GetRowByName( "MinimumOutpostDeploymentRange", false, null ).DefaultIntValue;
                MilitiaAttackOverkillPercentage = AIWar2GalaxySettingTable.Instance.GetRowByName( "MilitiaAttackOverkillPercentage", false, null ).DefaultIntValue / 100.0;
                SecondsBetweenMilitiaUpgrades = AIWar2GalaxySettingTable.Instance.GetRowByName( "SecondsBetweenMilitiaUpgrades", false, null ).DefaultIntValue;
                MinTechToProcess = AIWar2GalaxySettingTable.Instance.GetRowByName( "MinTechToProcess", false, null ).DefaultIntValue;
                DefensiveBattlestationForces = false; // Can't get a default boolean from xml, apparently.
            }

            // Load our global data.
            CivilianWorld worldData = World.Instance.GetCivilianWorldExt();

            // Update mark levels every now and than.
            if ( World_AIW2.Instance.GameSecond % SecondsBetweenMilitiaUpgrades == 0 )
            {
                bool playerAligned = false;
                if ( faction.Ex_MinorFactionCommon_GetPrimitives().Allegiance == "Player Team" )
                {
                    playerAligned = true;
                    faction.InheritsTechUpgradesFromPlayerFactions = true;
                    faction.RecalculateMarkLevelsAndInheritedTechUnlocks();
                }
                int globalAIMark = BadgerFactionUtilityMethods.GetRandomAIFaction( Context ).CurrentGeneralMarkLevel;
                faction.Entities.DoForEntities( delegate ( GameEntity_Squad entity )
                {
                    int requestedMark = faction.GetGlobalMarkLevelForShipLine( entity.TypeData );
                    if ( !playerAligned )
                    {
                        requestedMark = Math.Max( requestedMark, globalAIMark );
                        if ( !entity.TypeData.IsMobile )
                            requestedMark = Math.Max( requestedMark, entity.Planet.MarkLevelForAIOnly.Ordinal );
                    }
                    entity.SetCurrentMarkLevelIfHigherThanCurrent( requestedMark, Context );
                    return DelReturn.Continue;
                } );

                // Update resource ignoring.
                // Figure out what resources we should be ignoring.
                for ( int x = 0; x < IgnoreResource.Length; x++ )
                {
                    List<TechUpgrade> upgrades = TechUpgradeTable.Instance.Rows;
                    for ( int i = 0; i < upgrades.Count; i++ )
                    {
                        TechUpgrade upgrade = upgrades[i];
                        if ( upgrade.InternalName == ((CivilianTech)x).ToString() )
                        {
                            int unlocked = faction.TechUnlocks[upgrade.RowIndex];
                            unlocked += faction.FreeTechUnlocks[upgrade.RowIndex];
                            unlocked += faction.CalculatedInheritedTechUnlocks[upgrade.RowIndex];
                            if ( unlocked < MinTechToProcess )
                                IgnoreResource[x] = true;
                            else
                                IgnoreResource[x] = false;
                            break;
                        }
                    }
                }
            }

            // Update faction relations. Generally a good idea to have this in your DoPerSecondLogic function since other factions can also change their allegiances.
            UpdateAllegiance( faction );

            // If we don't yet have it, create our factionData.
            if ( factionData == null )
            {
                // Load our data.
                factionData = faction.GetCivilianFactionExt();
            }

            // If we have not yet done so, generate resources for planets.
            if ( !worldData.GeneratedResources )
            {
                World_AIW2.Instance.DoForFactions( delegate ( Faction tempFaction )
                {
                    tempFaction.DoForControlledPlanets( delegate ( Planet planet )
                    {
                        CivilianPlanet planetData = planet.GetCivilianPlanetExt();
                        if ( planetData.Resource == CivilianResource.Length )
                            planetData.Resource = (CivilianResource)Context.RandomToUse.Next( (int)CivilianResource.Length );
                        planet.SetCivilianPlanetExt( planetData );
                        return DelReturn.Continue;
                    } );

                    return DelReturn.Continue;
                } );
                worldData.GeneratedResources = true;
            }

            // If not currently active, create the faction's starting station.
            if ( World_AIW2.Instance.GetEntityByID_Squad( factionData.GrandStation ) == null )
                factionData.GrandStation = -1;

            // Increment rebuild timers.
            if ( factionData.GrandStationRebuildTimerInSeconds > 0 )
                factionData.GrandStationRebuildTimerInSeconds--;
            foreach ( int planet in factionData.TradeStationRebuildTimerInSecondsByPlanet.Keys.ToList() )
            {
                if ( factionData.TradeStationRebuildTimerInSecondsByPlanet[planet] > 0 )
                    factionData.TradeStationRebuildTimerInSecondsByPlanet[planet]--;
            }

            // Decloak if needed.
            for ( int y = 0; y < LastGameSecondForMessageAboutThisPlanet.GetPairCount(); y++ )
            {
                Planet planet = LastGameSecondForMessageAboutThisPlanet.GetPairByIndex( y ).Key;
                int lastSecond = LastGameSecondForMessageAboutThisPlanet[planet];

                if ( !LastGameSecondForLastTachyonBurstOnThisPlanet.GetHasKey( planet ) )
                    LastGameSecondForLastTachyonBurstOnThisPlanet.AddPair( planet, lastSecond );

                if ( World_AIW2.Instance.GameSecond - LastGameSecondForLastTachyonBurstOnThisPlanet[planet] >= 30 )
                {
                    var threat = factionData.GetThreat( planet );
                    if ( threat.CloakedHostile > threat.Total * 0.9 )
                        BadgerFactionUtilityMethods.TachyonBlastPlanet( planet, faction );
                    LastGameSecondForLastTachyonBurstOnThisPlanet[planet] = World_AIW2.Instance.GameSecond;
                }
            }

            // For each faction we're friendly to, proccess.
            for ( int x = 0; x < World_AIW2.Instance.Factions.Count; x++ )
            {
                Faction alignedFaction = World_AIW2.Instance.Factions[x];
                if ( !faction.GetIsFriendlyTowards( alignedFaction ) )
                    continue;

                if ( faction.FactionIndex == alignedFaction.FactionIndex )
                    continue; // Skip self

                // Grand Station creation.
                if ( factionData.GrandStation == -1 && factionData.GrandStationRebuildTimerInSeconds == 0 )
                {
                    CreateGrandStation( faction, alignedFaction, Context );
                }

                // If we don't yet have a grand station built for this faction, stop. Without our grand station, we're nothing.
                if ( factionData.GrandStation == -1 )
                    continue;

                // Handle spawning of trade stations.
                CreateTradeStations( faction, alignedFaction, Context );

                // Add buildings for the player to build.
                if ( alignedFaction.Type == FactionType.Player )
                {
                    if ( World_AIW2.Instance.GameSecond % 15 == 0 )
                        AddMilitiaBuildings( faction, alignedFaction, Context );

                    // Scan for any new buildings that the player has placed related to the mod.
                    ScanForMilitiaBuildings( faction, alignedFaction, Context );
                }
            }

            // Handle basic resource generation. (Resources with no requirements, ala Goods or Ore.)
            DoResources( faction, Context );

            // Handle the creation of ships.
            DoShipSpawns( faction, Context );

            // Check for ship arrival.
            DoShipArrival( faction, Context );

            // Handle resource transfering.
            DoResourceTransfer( faction, Context );

            // Handle assigning militia to our ThreatReports.
            DoMilitiaAssignment( faction, Context );

            // Handle militia deployment and unit building.
            DoMilitiaDeployment( faction, Context );

            // Handle AI response. Have some variation on wave timers.
            if ( factionData.NextRaidInThisSeconds > 120 )
                factionData.NextRaidInThisSeconds = Math.Max( 120, factionData.NextRaidInThisSeconds - Context.RandomToUse.Next( 1, 3 ) );
            else if ( factionData.NextRaidInThisSeconds > 0 )
                factionData.NextRaidInThisSeconds -= Context.RandomToUse.Next( 1, 3 );

            // Prepare (and warn the player about) an upcoming raid.
            if ( factionData.NextRaidInThisSeconds == 120 )
                PrepareAIRaid( faction, Context );

            // Raid!
            if ( factionData.NextRaidInThisSeconds <= 0 )
                DoAIRaid( faction, Context );

            // Save our faction data.
            faction.SetCivilianFactionExt( factionData );

            // Save our world data.
            World.Instance.SetCivilianWorldExt( worldData );
        }

        // Calculate threat values every planet.
        public void CalculateThreat( Faction faction, ArcenLongTermIntermittentPlanningContext Context )
        {
            // Empty our dictionary.
            factionData.ThreatReports = new List<ThreatReport>();

            // Get the grand station's planet, to easily figure out when we're processing the home planet.
            GameEntity_Squad grandStation = World_AIW2.Instance.GetEntityByID_Squad( factionData.GrandStation );
            if ( grandStation == null )
                return;
            Planet grandPlanet = grandStation.Planet;
            if ( grandPlanet == null )
                return;

            for ( int x = 0; x < World_AIW2.Instance.Galaxies[0].Planets.Count; x++ )
            {
                Planet planet = World_AIW2.Instance.Galaxies[0].Planets[x];

                // Prepare variables to hold our soon to be detected threat values.
                int friendlyMobileStrength = 0, friendlyGuardStrength = 0, cloakedHostileStrength = 0, nonCloakedHostileStrength = 0, militiaMobileStrength = 0, militiaGuardStrength = 0, waveStrength = 0;
                // Wave detection.
                for ( int j = 0; j < World_AIW2.Instance.AIFactions.Count; j++ )
                {
                    Faction aiFaction = World_AIW2.Instance.AIFactions[j];
                    List<PlannedWave> QueuedWaves = aiFaction.GetWaveList();
                    for ( int k = 0; k < QueuedWaves.Count; k++ )
                    {
                        PlannedWave wave = QueuedWaves[k];

                        if ( wave.targetPlanetIdx != planet.Index )
                            continue;

                        if ( wave.gameTimeInSecondsForLaunchWave - World_AIW2.Instance.GameSecond <= 90 )
                            nonCloakedHostileStrength += wave.CalculateStrengthOfWave( aiFaction ) * 3;

                        else if ( wave.playerBeingAlerted )
                            waveStrength += wave.CalculateStrengthOfWave( aiFaction );
                    }
                }

                // Get hostile strength.
                LongRangePlanningData_PlanetFaction linkedPlanetFactionData = planet.LongRangePlanningData.PlanetFactionDataByIndex[faction.FactionIndex];
                LongRangePlanning_StrengthData_PlanetFaction_Stance hostileStrengthData = linkedPlanetFactionData.DataByStance[FactionStance.Hostile];
                // If on friendly planet, triple the threat.
                if ( factionData.IsPlanetFriendly( faction, planet ) )
                    nonCloakedHostileStrength += hostileStrengthData.TotalStrength * 3;
                else // If on hostile planet, don't factor in stealth.
                {
                    nonCloakedHostileStrength += hostileStrengthData.TotalStrength - hostileStrengthData.CloakedStrength;
                    cloakedHostileStrength += hostileStrengthData.CloakedStrength;
                }

                // Adjacent planet threat matters as well, but not as much as direct threat.
                // We'll only add it if the planet has no friendly forces on it.
                if ( factionData.IsPlanetFriendly( faction, planet ) )
                    planet.DoForLinkedNeighbors( delegate ( Planet linkedPlanet )
                    {
                        if ( factionData.IsPlanetFriendly( faction, linkedPlanet ) )
                            return DelReturn.Continue;

                        linkedPlanetFactionData = linkedPlanet.LongRangePlanningData.PlanetFactionDataByIndex[faction.FactionIndex];
                        LongRangePlanning_StrengthData_PlanetFaction_Stance attackingStrengthData = linkedPlanetFactionData.DataByStance[FactionStance.Friendly];
                        int attackingStrength = attackingStrengthData.TotalStrength;
                        attackingStrengthData = linkedPlanetFactionData.DataByStance[FactionStance.Self];
                        attackingStrength += attackingStrengthData.TotalStrength;

                        if ( attackingStrength < 1000 )
                        {
                            hostileStrengthData = linkedPlanetFactionData.DataByStance[FactionStance.Hostile];
                            nonCloakedHostileStrength += hostileStrengthData.RelativeToHumanTeam_ThreatStrengthVisible;
                            nonCloakedHostileStrength += hostileStrengthData.TotalHunterStrengthVisible;
                        }

                        return DelReturn.Continue;
                    } );

                // If on home plant, double the total threat.
                if ( planet.Index == grandPlanet.Index )
                    nonCloakedHostileStrength *= 2;

                // Get friendly strength on the planet.
                LongRangePlanningData_PlanetFaction planetFactionData = planet.LongRangePlanningData.PlanetFactionDataByIndex[faction.FactionIndex];
                LongRangePlanning_StrengthData_PlanetFaction_Stance friendlyStrengthData = planetFactionData.DataByStance[FactionStance.Friendly];
                friendlyMobileStrength += friendlyStrengthData.MobileStrength;
                friendlyGuardStrength += friendlyStrengthData.TotalStrength - friendlyMobileStrength;

                // Get militia strength on the planet.
                LongRangePlanning_StrengthData_PlanetFaction_Stance militiaStrengthData = planetFactionData.DataByStance[FactionStance.Self];
                militiaMobileStrength = militiaStrengthData.MobileStrength;
                militiaGuardStrength = militiaStrengthData.TotalStrength - militiaMobileStrength;

                // Save our threat value.
                factionData.ThreatReports.Add( new ThreatReport( planet, militiaGuardStrength, militiaMobileStrength, friendlyGuardStrength, friendlyMobileStrength, cloakedHostileStrength, nonCloakedHostileStrength, waveStrength ) );
            }
            // Sort our reports.
            factionData.ThreatReports.Sort();
        }

        // Handle station requests.
        private const int BASE_MIL_URGENCY = 20;
        private const int MIL_URGENCY_REDUCTION_PER_REGULAR = 8;
        private const int MIL_URGENCY_REDUCTION_PER_LARGE = 4;

        private const int BASE_CIV_URGENCY = 5;
        private const int CIV_URGENCY_REDUCTION_PER_REGULAR = 1;
        public void DoTradeRequests( Faction faction, ArcenLongTermIntermittentPlanningContext Context )
        {
            Engine_Universal.NewTimingsBeingBuilt.StartRememberingFrame( FramePartTimings.TimingType.ShortTermBackgroundThreadEntry, "DoTradeRequests" );

            #region Preparation
            // Clear our lists.
            factionData.ImportRequests = new List<TradeRequest>();
            factionData.ExportRequests = new List<TradeRequest>();

            // Preload two lists, one for ships' origin station, and one for ships' destination station.
            ArcenSparseLookup<int, int> AnsweringImport = new ArcenSparseLookup<int, int>();
            ArcenSparseLookup<int, int> AnsweringExport = new ArcenSparseLookup<int, int>();

            ProcessTradingResponse( factionData.CargoShipsPathing, ref AnsweringImport, ref AnsweringExport );
            ProcessTradingResponse( factionData.CargoShipsLoading, ref AnsweringImport, ref AnsweringExport );
            ProcessTradingResponse( factionData.CargoShipsEnroute, ref AnsweringImport );
            ProcessTradingResponse( factionData.CargoShipsUnloading, ref AnsweringImport );
            ProcessTradingResponse( factionData.CargoShipsBuilding, ref AnsweringImport );
            #endregion

            #region Planet Level Trading
            // See if any militia stations don't have a trade in progress.
            for ( int x = 0; x < factionData.MilitiaLeaders.Count; x++ )
            {
                GameEntity_Squad militia = World_AIW2.Instance.GetEntityByID_Squad( factionData.MilitiaLeaders[x] );
                if ( militia == null )
                {
                    factionData.MilitiaLeaders.RemoveAt( x );
                    x--;
                    continue;
                }

                if ( militia.GetCivilianMilitiaExt().Status != CivilianMilitiaStatus.Defending && militia.GetCivilianMilitiaExt().Status != CivilianMilitiaStatus.Patrolling )
                    continue;

                // Don't request resources we're full on, or that we are ignoring.
                List<CivilianResource> toIgnore = new List<CivilianResource>();
                CivilianCargo militiaCargo = militia.GetCivilianCargoExt();
                for ( int y = 0; y < militiaCargo.Amount.Length; y++ )
                    if ( IgnoreResource[y] || (militiaCargo.Amount[y] > 0 && militiaCargo.Amount[y] >= militiaCargo.Capacity[y]) )
                        toIgnore.Add( (CivilianResource)y );

                // Stop if we're full of everything.
                if ( toIgnore.Count == (int)CivilianResource.Length )
                    continue;

                int incoming = AnsweringImport.GetHasKey(militia.PrimaryKeyID) ? AnsweringImport[militia.PrimaryKeyID] : 0;
                int urgency = BASE_MIL_URGENCY;
                if ( militia.TypeData.GetHasTag( "BuildsProtectors" ) ) // Allow more inbound ships for larger projects.
                    urgency -= MIL_URGENCY_REDUCTION_PER_LARGE * incoming;
                else
                    urgency -= MIL_URGENCY_REDUCTION_PER_REGULAR * incoming;

                // Add a request for any resource.
                factionData.ImportRequests.Add( new TradeRequest( CivilianResource.Length, toIgnore, urgency, militia, 0 ) );
            }
            #endregion

            #region Trade Station Imports and Exports

            // Populate our list with trade stations.
            for ( int x = 0; x < factionData.TradeStations.Count; x++ )
            {
                GameEntity_Squad requester = World_AIW2.Instance.GetEntityByID_Squad( factionData.TradeStations[x] );
                if ( requester == null )
                {
                    // Remove invalid ResourcePoints.
                    factionData.TradeStations.RemoveAt( x );
                    x--;
                    continue;
                }
                CivilianCargo requesterCargo = requester.GetCivilianCargoExt();
                if ( requesterCargo == null )
                    continue;

                // Check each type of cargo seperately.
                for ( int y = 0; y < requesterCargo.PerSecond.Length; y++ )
                {
                    // Skip if we don't accept it.
                    if ( requesterCargo.Capacity[y] <= 0 )
                        continue;

                    // Skip if we're supposed to.
                    if ( IgnoreResource[y] )
                        continue;

                    // Resources we generate.
                    if ( requesterCargo.PerSecond[y] > 0 )
                    {
                        // Generates urgency based on how close to full capacity we are.
                        if ( requesterCargo.Amount[y] > 100 )
                        {
                            // Absolute max export cap is the per second generation times 5.
                            // This may cause some shortages, but that fits in with the whole trading theme so is a net positive regardless.
                            int incoming = AnsweringExport.GetHasKey( requester.PrimaryKeyID ) ? AnsweringExport[requester.PrimaryKeyID] : 0;
                            int urgency = ((int)Math.Ceiling( ((500.0 + requesterCargo.Amount[y]) / requesterCargo.Capacity[y]) * (requesterCargo.PerSecond[y] * 5) )) - incoming;

                            if ( urgency > 0 )
                                factionData.ExportRequests.Add( new TradeRequest( (CivilianResource)y, urgency, requester, 5 ) );
                        }
                    }
                    // Resource we store. Simply put out a super tiny order to import/export based on current stores.
                    else if ( requesterCargo.Amount[y] >= requesterCargo.Capacity[y] * 0.5 )
                    {
                        int incoming = AnsweringExport.GetHasKey( requester.PrimaryKeyID ) ? AnsweringExport[requester.PrimaryKeyID] : 0;
                        int urgency = BASE_CIV_URGENCY;
                        urgency -= incoming * CIV_URGENCY_REDUCTION_PER_REGULAR;

                        if ( urgency > 0 )
                            factionData.ExportRequests.Add( new TradeRequest( (CivilianResource)y, 0, requester, 3 ) );
                    }
                    else if ( requesterCargo.Amount[y] < requesterCargo.Capacity[y] * 0.5 )
                    {
                        int incoming = AnsweringImport.GetHasKey( requester.PrimaryKeyID ) ? AnsweringImport[requester.PrimaryKeyID] : 0;
                        int urgency = BASE_CIV_URGENCY;
                        urgency -= incoming * CIV_URGENCY_REDUCTION_PER_REGULAR;

                        factionData.ImportRequests.Add( new TradeRequest( (CivilianResource)y, urgency, requester, 5 ) );
                    }

                }
            }

            #endregion

            // If no import or export requests, stop.
            if ( factionData.ImportRequests.Count == 0 || factionData.ExportRequests.Count == 0 )
            {
                Engine_Universal.NewTimingsBeingBuilt.FinishRememberingFrame( FramePartTimings.TimingType.ShortTermBackgroundThreadEntry, "DoTradeRequests" );
                return;
            }

            // Sort our lists.
            factionData.ImportRequests.Sort();
            factionData.ExportRequests.Sort();

            #region Execute Trade

            // While we have free ships left, assign our requests away.
            for ( int x = 0; x < factionData.ImportRequests.Count && factionData.CargoShipsIdle.Count > 0; x++ )
            {
                // If no free cargo ships, stop.
                if ( factionData.CargoShipsIdle.Count == 0 )
                    break;
                TradeRequest importRequest = factionData.ImportRequests[x];
                // If processed, remove.
                if ( importRequest.Processed == true )
                {
                    factionData.ImportRequests.RemoveAt( x );
                    x--;
                    continue;
                }
                int requestedMaxHops = importRequest.MaxSearchHops;
                GameEntity_Squad requestingEntity = importRequest.Station;
                if ( requestingEntity == null )
                {
                    factionData.ImportRequests.RemoveAt( x );
                    x--;
                    continue;
                }
                // Get a free cargo ship, prefering nearest.
                GameEntity_Squad foundCargoShip = null;
                for ( int y = 0; y < factionData.CargoShipsIdle.Count; y++ )
                {
                    GameEntity_Squad cargoShip = World_AIW2.Instance.GetEntityByID_Squad( factionData.CargoShipsIdle[y] );
                    if ( cargoShip == null )
                    {
                        factionData.RemoveCargoShip( factionData.CargoShipsIdle[y] );
                        y--;
                        continue;
                    }
                    // If few enough hops away for this attempt, assign.
                    if ( foundCargoShip == null || cargoShip.Planet.GetHopsTo( requestingEntity.Planet ) <= foundCargoShip.Planet.GetHopsTo( requestingEntity.Planet ) )
                    {
                        foundCargoShip = cargoShip;
                        continue;
                    }
                }
                if ( foundCargoShip == null )
                    continue;
                // If the cargo ship over 75% of the resource already on it, skip the origin station search, and just have it start heading right towards our requesting station.
                bool hasEnough = false;
                CivilianCargo foundCargo = foundCargoShip.GetCivilianCargoExt();
                for ( int y = 0; y < (int)CivilianResource.Length; y++ )
                    if ( (importRequest.Requested == CivilianResource.Length && !importRequest.Declined.Contains( (CivilianResource)y )) || importRequest.Requested == (CivilianResource)y )
                        if ( foundCargo.Amount[y] > foundCargo.Capacity[y] * 0.75 )
                        {
                            hasEnough = true;
                            break;
                        }

                if ( hasEnough )
                {
                    // Update our cargo ship with its new mission.
                    CivilianStatus cargoShipStatus = foundCargoShip.GetCivilianStatusExt();
                    cargoShipStatus.Origin = -1;    // No origin station required.
                    cargoShipStatus.Destination = requestingEntity.PrimaryKeyID;
                    factionData.ChangeCargoShipStatus( foundCargoShip, "Enroute" );
                    // Save its updated status.
                    foundCargoShip.SetCivilianStatusExt( cargoShipStatus );
                    // Remove the completed entities from processing.
                    importRequest.Processed = true;
                    continue;
                }

                // Find a trade request of the same resource type and opposing Import/Export status thats within our hop limit.
                GameEntity_Squad otherStation = null;
                TradeRequest otherRequest = null;
                for ( int z = 0; z < factionData.ExportRequests.Count; z++ )
                {
                    // Skip if same.
                    if ( x == z )
                        continue;
                    TradeRequest exportRequest = factionData.ExportRequests[z];
                    // If processed, skip.
                    if ( exportRequest.Processed )
                        continue;
                    int otherRequestedMaxHops = exportRequest.MaxSearchHops;

                    if ( (importRequest.Requested == exportRequest.Requested // Matching request.
                        || (importRequest.Requested == CivilianResource.Length && !importRequest.Declined.Contains( exportRequest.Requested ))) // Export has a resource import accepts.
                      && importRequest.Station.Planet.GetHopsTo( exportRequest.Station.Planet ) <= Math.Min( requestedMaxHops, otherRequestedMaxHops ) )
                    {
                        otherStation = exportRequest.Station;
                        otherRequest = exportRequest;
                        break;
                    }
                }
                if ( otherStation != null )
                {
                    // Assign our ship to our new trade route, and remove both requests and the ship from our lists.
                    CivilianStatus cargoShipStatus = foundCargoShip.GetCivilianStatusExt();
                    // Make sure the Origin is the Exporter and the Destination is the Importer.
                    cargoShipStatus.Origin = otherStation.PrimaryKeyID;
                    cargoShipStatus.Destination = requestingEntity.PrimaryKeyID;

                    factionData.ChangeCargoShipStatus( foundCargoShip, "Pathing" );
                    // Save its updated status.
                    foundCargoShip.SetCivilianStatusExt( cargoShipStatus );
                    // Remove the completed entities from processing.
                    importRequest.Processed = true;
                    if ( otherRequest != null )
                        otherRequest.Processed = true;
                }
            }

            // If we've finished due to not having enough trade ships, request more cargo ships.
            if ( factionData.ImportRequests.Count > 0 && factionData.ExportRequests.Count > 0 && factionData.CargoShipsIdle.Count == 0 )
                factionData.FailedCounter = (factionData.ImportRequests.Count, factionData.ExportRequests.Count);
            else
                factionData.FailedCounter = (0, 0);
            #endregion

            Engine_Universal.NewTimingsBeingBuilt.FinishRememberingFrame( FramePartTimings.TimingType.MainSimThreadNormal, "DoTradeRequests" );
        }
        private void ProcessTradingResponse(List<int> ships, ref ArcenSparseLookup<int, int> AnsweringImport, ref ArcenSparseLookup<int, int> AnsweringExport )
        {
            for ( int x = 0; x < ships.Count; x++ )
            {
                GameEntity_Squad ship = World_AIW2.Instance.GetEntityByID_Squad( ships[x] );
                if ( ship == null )
                    continue;
                int target = ship.GetCivilianStatusExt().Origin;
                if ( !AnsweringExport.GetHasKey( target ) )
                    AnsweringExport.AddPair( target, 1 );
                else
                    AnsweringExport[target]++;
            }
            ProcessTradingResponse( ships, ref AnsweringImport );
        }
        private void ProcessTradingResponse( List<int> ships, ref ArcenSparseLookup<int, int> AnsweringImport )
        {
            for ( int x = 0; x < ships.Count; x++ )
            {
                GameEntity_Squad ship = World_AIW2.Instance.GetEntityByID_Squad( ships[x] );
                if ( ship == null )
                    continue;
                int target = ship.GetCivilianStatusExt().Destination;
                if ( !AnsweringImport.GetHasKey( target ) )
                    AnsweringImport.AddPair( target, 1 );
                else
                    AnsweringImport[target]++;
            }
        }

        // Handle movement of cargo ships to their orign and destination points.
        public void DoCargoShipMovement( Faction faction, ArcenLongTermIntermittentPlanningContext Context )
        {
            // Ships going somewhere for dropoff.
            ProcessIncoming( factionData.CargoShipsBuilding, 5000 );
            ProcessIncoming( factionData.CargoShipsEnroute, 2000 );
            ProcessIncoming( factionData.CargoShipsUnloading, 5000 );

            // Ships going somewhere for pickup.
            ProcessOutgoing( factionData.CargoShipsLoading, 5000 );
            ProcessOutgoing( factionData.CargoShipsPathing, 2000 );
        }
        private void ProcessIncoming( List<int> ships, int maxDistance  )
        {
            for ( int x = 0; x < ships.Count; x++ )
            {
                // Load the ship and its status.
                GameEntity_Squad ship = World_AIW2.Instance.GetEntityByID_Squad( ships[x] );
                if ( ship == null )
                    continue;
                CivilianStatus shipStatus = ship.GetCivilianStatusExt();
                if ( shipStatus == null )
                    continue;
                // Enroute movement.
                // ship currently moving towards destination station.
                GameEntity_Squad destinationStation = World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Destination );
                if ( destinationStation == null )
                    continue;
                Planet destinationPlanet = destinationStation.Planet;

                // Check if already on planet.
                if ( ship.Planet.Index == destinationPlanet.Index )
                {
                    if ( destinationStation.GetDistanceTo_ExpensiveAccurate( ship.WorldLocation, true, true ) < maxDistance )
                        continue; // Stop if already close enough.

                    // On planet. Begin pathing towards the station.
                    QueueMovementCommand( ship, destinationStation.WorldLocation );
                }
                else
                {
                    if ( ship.LongRangePlanningData != null && ship.LongRangePlanningData.FinalDestinationIndex != -1 )
                        continue; // Stop if already enroute.

                    // Not on planet yet, prepare wormhole navigation.
                    QueueWormholeCommand( ship, destinationPlanet );
                }
            }
        }
        private void ProcessOutgoing( List<int> ships, int maxDistance )
        {
            for ( int x = 0; x < ships.Count; x++ )
            {
                // Load the ship and its status.
                GameEntity_Squad ship = World_AIW2.Instance.GetEntityByID_Squad( ships[x] );
                if ( ship == null )
                    continue;
                CivilianStatus shipStatus = ship.GetCivilianStatusExt();
                if ( shipStatus == null )
                    continue;
                // Ship currently moving towards origin station.
                GameEntity_Squad originStation = World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Origin );
                if ( originStation == null )
                    continue;
                Planet originPlanet = originStation.Planet;

                // Check if already on planet.
                if ( ship.Planet.Index == originPlanet.Index )
                {
                    if ( originStation.GetDistanceTo_ExpensiveAccurate( ship.WorldLocation, true, true ) < maxDistance )
                        continue; // Stop if already close enough.

                    // On planet. Begin pathing towards the station.
                    QueueMovementCommand( ship, originStation.WorldLocation );
                }
                else
                {
                    if ( ship.LongRangePlanningData != null && ship.LongRangePlanningData.FinalDestinationIndex != -1 )
                        continue; // Stop if already moving.

                    // Not on planet yet, queue a wormhole movement command.
                    QueueWormholeCommand( ship, originPlanet );
                }
            }
        }

        /// <summary>
        /// Handle movement of militia construction ships.

        /// </summary>
        public void DoMilitiaConstructionShipMovement( Faction faction, ArcenLongTermIntermittentPlanningContext Context )
        {
            // Loop through each of our militia ships.
            for ( int x = 0; x < factionData.MilitiaLeaders.Count; x++ )
            {
                // Load the ship and its status.
                GameEntity_Squad ship = World_AIW2.Instance.GetEntityByID_Squad( factionData.MilitiaLeaders[x] );
                if ( ship == null || !ship.TypeData.IsMobile )
                    continue;
                CivilianMilitia shipStatus = ship.GetCivilianMilitiaExt();
                if ( shipStatus == null )
                    continue;
                Planet planet = World_AIW2.Instance.GetPlanetByIndex( shipStatus.PlanetFocus );
                if ( planet == null )
                    continue;

                // Pathing movement.
                if ( shipStatus.Status == CivilianMilitiaStatus.PathingForMine || shipStatus.Status == CivilianMilitiaStatus.PathingForWormhole || shipStatus.Status == CivilianMilitiaStatus.PathingForShipyard )
                {
                    // Check if already on planet.
                    if ( ship.Planet.Index == shipStatus.PlanetFocus )
                    {
                        // On planet. Begin pathing towards the station.
                        GameEntity_Squad goalStation = null;

                        // Find the trade station.
                        planet.DoForEntities( delegate ( GameEntity_Squad entity )
                        {
                            // If we find its index in our records, thats our trade station.
                            if ( factionData.TradeStations.Contains( entity.PrimaryKeyID ) )
                            {
                                goalStation = entity;
                                return DelReturn.Break;
                            }

                            return DelReturn.Continue;
                        } );

                        if ( goalStation == null )
                            continue;

                        if ( ship.LongRangePlanningData != null && ship.LongRangePlanningData.DestinationPoint == goalStation.WorldLocation )
                            continue; // Stop if we're already enroute.

                        QueueMovementCommand( ship, goalStation.WorldLocation );
                    }
                    else
                    {
                        // Not on planet yet, prepare wormhole navigation.
                        if ( ship.LongRangePlanningData != null && ship.LongRangePlanningData.FinalDestinationIndex != -1 )
                            continue; // Stop if we're already enroute.

                        QueueWormholeCommand( ship, planet );
                    }
                }
                else if ( shipStatus.Status == CivilianMilitiaStatus.EnrouteWormhole )
                {
                    // Enroute movement.
                    // Ship has made it to the planet (and, if detected, the trade station on the planet).
                    // We'll now have it begin moving towards its assigned wormhole.
                    // Distance detection for it is handled in the persecond logic further up, all this handles are movement commands.
                    GameEntity_Other wormhole = shipStatus.getWormhole();
                    if ( wormhole == null )
                    {
                        ArcenDebugging.SingleLineQuickDebug( "Civilian Industries: Failed to find wormhole." );
                        continue;
                    }

                    // Generate our location to move to.
                    ArcenPoint point = ship.WorldLocation.GetPointAtAngleAndDistance( ship.WorldLocation.GetAngleToDegrees( wormhole.WorldLocation ), 5000 );
                    if ( point == ArcenPoint.ZeroZeroPoint )
                        continue;

                    if ( ship.LongRangePlanningData != null && ship.LongRangePlanningData.DestinationPoint.GetDistanceTo( point, true ) < 1000 )
                        continue; // Stop if we're already enroute.

                    QueueMovementCommand( ship, point );
                }
                else if ( shipStatus.Status == CivilianMilitiaStatus.EnrouteMine )
                {
                    // Enroute movement.
                    // Ship has made it to the planet (and, if detected, the trade station on the planet).
                    // We'll now have it begin moving towards its assigned mine.
                    // Distance detection for it is handled in the persecond logic further up, all this handles are movement commands.
                    GameEntity_Squad mine = shipStatus.getMine();
                    if ( mine == null )
                    {
                        ArcenDebugging.SingleLineQuickDebug( "Civilian Industries: Failed to find mine." );
                        continue;
                    }

                    if ( ship.LongRangePlanningData != null && ship.LongRangePlanningData.DestinationPoint == mine.WorldLocation )
                        continue; // Stop if we're enroute.

                    QueueMovementCommand( ship, mine.WorldLocation );
                }
            }
        }

        // Handle reactive moevement of patrolling ship fleets.
        public void DoMilitiaThreatReaction( Faction faction, ArcenLongTermIntermittentPlanningContext Context )
        {
            Engine_Universal.NewTimingsBeingBuilt.StartRememberingFrame( FramePartTimings.TimingType.ShortTermBackgroundThreadEntry, "DoMilitiaThreatReaction" );
            // If we don't have any threat reports yet (usually due to game load) wait.
            if ( factionData.ThreatReports == null || factionData.ThreatReports.Count == 0 )
                return;

            // Amount of strength ready to raid on each planet.
            // This means that it, and all friendly planets adjacent to it, are safe.
            Dictionary<Planet, int> raidStrength = new Dictionary<Planet, int>();

            // Planets that have been given an order. Used for patrolling logic at the bottom.
            List<Planet> isPatrolling = new List<Planet>();

            // Process all militia forces that are currently patrolling.
            #region Defensive Actions
            for ( int x = 0; x < factionData.MilitiaLeaders.Count; x++ )
            {
                GameEntity_Squad post = World_AIW2.Instance.GetEntityByID_Squad( factionData.MilitiaLeaders[x] );
                if ( post == null || post.GetCivilianMilitiaExt().Status != CivilianMilitiaStatus.Patrolling )
                    continue;

                CivilianMilitia militiaData = post.GetCivilianMilitiaExt();

                GameEntity_Squad centerpiece = World_AIW2.Instance.GetEntityByID_Squad( militiaData.Centerpiece );
                if ( centerpiece == null )
                    continue;

                // Where are we going to send all our units?
                Planet targetPlanet = null;

                // If our centerpiece is a battlestation, and the user has requested them to have defensive forces, act on that.
                if ( centerpiece.TypeData.IsBattlestation && DefensiveBattlestationForces )
                    targetPlanet = centerpiece.Planet;

                // If self or an adjacent friendly planet has hostile units on it that outnumber friendly defenses, including incoming waves, protect it.
                for ( int y = 0; y < factionData.ThreatReports.Count && targetPlanet == null; y++ )
                {
                    ThreatReport report = factionData.ThreatReports[y];

                    if ( report.Planet.GetHopsTo( centerpiece.Planet ) > 1 )
                        continue; // Skip if not adjacent

                    if ( report.TotalStrength < report.MilitiaGuardStrength + report.FriendlyGuardStrength )
                        continue; // Skip if defenses are strong enough.

                    if ( factionData.IsPlanetFriendly( faction, report.Planet ) )
                    {
                        targetPlanet = report.Planet;
                        break;
                    }
                }

                // If we have a target for defensive action, act on it.
                if ( targetPlanet != null )
                {
                    isPatrolling.Add( centerpiece.Planet );

                    for ( int y = 0; y < militiaData.Ships.Count; y++ )
                    {
                        for ( int z = 0; z < militiaData.Ships[y].Count; z++ )
                        {
                            GameEntity_Squad entity = World_AIW2.Instance.GetEntityByID_Squad( militiaData.Ships[y][z] );
                            if ( entity == null || entity.LongRangePlanningData == null )
                                continue;

                            // Skip centerpiece.
                            if ( centerpiece.PrimaryKeyID == entity.PrimaryKeyID )
                                continue;

                            if ( entity.Planet.Index == targetPlanet.Index && entity.LongRangePlanningData.FinalDestinationIndex != -1 &&
                                entity.LongRangePlanningData.FinalDestinationIndex != targetPlanet.Index )
                            {
                                // We're on our target planet, but for some reason we're trying to leave it. Stop.
                                entity.Orders.ClearOrders( ClearBehavior.DoNotClearBehaviors, ClearDecollisionOnParent.DoNotClearDecollision, ClearSource.YesClearAnyOrders_IncludingFromHumans );
                            }

                            if ( entity.Planet.Index != targetPlanet.Index && entity.LongRangePlanningData.FinalDestinationIndex != targetPlanet.Index )
                            {
                                if ( entity.Planet.Index != centerpiece.Planet.Index )
                                {
                                    // Not yet on our target planet, and we're not yet on our centerpiece planet. Path to our centerpiece planet first.
                                    if ( entity.LongRangePlanningData.FinalDestinationIndex == centerpiece.Planet.Index )
                                        continue; // Stop if already moving towards it.

                                    QueueWormholeCommand( entity, centerpiece.Planet );
                                }
                                else
                                {
                                    // Not yet on our target planet, and we're on our centerpice planet. Path to our target planet.
                                    QueueWormholeCommand( entity, targetPlanet );
                                }
                            }
                        }
                    }
                }
                else
                {
                    int val = 0;
                    if ( raidStrength.ContainsKey( centerpiece.Planet ) )
                        val = raidStrength[centerpiece.Planet];
                    // If we have at least one planet adjacent to us that is hostile and threatening, add our patrol posts to the raiding pool.
                    centerpiece.Planet.DoForLinkedNeighbors( delegate ( Planet adjPlanet )
                    {
                        var threat = factionData.GetThreat( adjPlanet );
                        if ( !factionData.IsPlanetFriendly( faction, adjPlanet ) && threat.Total > 1000 )
                        {
                            int strength = 0;
                            for ( int y = 0; y < militiaData.Ships.Count; y++ )
                            {
                                for ( int z = 0; z < militiaData.Ships[y].Count; z++ )
                                {
                                    GameEntity_Squad entity = World_AIW2.Instance.GetEntityByID_Squad( militiaData.Ships[y][z] );
                                    if ( entity == null )
                                        continue;

                                    if ( entity.TypeData.IsMobileCombatant && (entity.TypeData.GetHasTag( "CivMobile" ) || entity.TypeData.GetHasTag( "CivProtector" )) )
                                        strength += entity.GetStrengthOfSelfAndContents();
                                }
                            }

                            if ( !raidStrength.ContainsKey( centerpiece.Planet ) )
                                raidStrength.Add( centerpiece.Planet, strength );
                            else
                                raidStrength[centerpiece.Planet] += strength;
                            return DelReturn.Break;
                        }
                        return DelReturn.Continue;
                    } );
                    if ( raidStrength.ContainsKey( centerpiece.Planet ) )
                        val = raidStrength[centerpiece.Planet];
                }
            }
            #endregion

            #region Offensive Actions
            // Figure out the potential strength we would have to attack each planet.
            List<AttackAssessment> attackAssessments = new List<AttackAssessment>();
            if ( raidStrength.Count > 0 && raidStrength.Count > 0 )

                foreach ( KeyValuePair<Planet, int> raidingPlanet in raidStrength )
                {
                    raidingPlanet.Key.DoForLinkedNeighbors( delegate ( Planet adjPlanet )
                    {
                        // If friendly, skip.
                        if ( factionData.IsPlanetFriendly( faction, adjPlanet ) )
                            return DelReturn.Continue;

                        var threat = factionData.GetThreat( adjPlanet );

                        // See if they still have any active guard posts.
                        int reinforcePoints = 0;
                        adjPlanet.DoForEntities( EntityRollupType.ReinforcementLocations, delegate ( GameEntity_Squad reinforcementPoint )
                        {
                            if ( reinforcementPoint.TypeData.SpecialType == SpecialEntityType.GuardPost )
                                reinforcePoints++;
                            return DelReturn.Continue;
                        } );

                        // If we don't yet have an assessment for the planet, and it has enough threat, add it.
                        // Factor out planets that have already been covered by player units.
                        if ( reinforcePoints > 0 || threat.Total > Math.Max( 1000, (threat.FriendlyMobile + threat.FriendlyGuard) / 3 ) )
                        {
                            AttackAssessment adjAssessment = (from o in attackAssessments where o.Target.Index == adjPlanet.Index select o).FirstOrDefault();
                            if ( adjAssessment == null )
                            {
                                adjAssessment = new AttackAssessment( adjPlanet, (int)(threat.Total * MilitiaAttackOverkillPercentage), reinforcePoints > 0 ? true : false );
                                // If we already have units on the planet, mark it as such.
                                if ( threat.MilitiaMobile > 1000 )
                                    adjAssessment.MilitiaOnPlanet = true;

                                attackAssessments.Add( adjAssessment );
                            }
                            // Add our current fleet strength to the attack budget.
                            adjAssessment.Attackers.Add( raidingPlanet.Key, raidingPlanet.Value );
                        }
                        return DelReturn.Continue;
                    } );
                }

            // Sort by strongest planets first. We want to attempt to take down the strongest planet.
            attackAssessments.Sort();

            // Keep poising to attack as long as the target we're aiming for is weak to us.
            while ( attackAssessments.Count > 0 )
            {
                AttackAssessment assessment = attackAssessments[0];

                // See if there are already any player units on the planet.
                // If there are, we should be heading there as soon as possible.
                bool alreadyAttacking = false;
                var threat = factionData.GetThreat( assessment.Target );
                if ( threat.FriendlyMobile + threat.FriendlyGuard > 1000 )
                {
                    // If they need our help, see if we can assist.
                    // Consider hostile strength less effective than regular for this purpose.
                    int effStr = threat.Total / 3;
                    if ( effStr < threat.FriendlyGuard + threat.FriendlyMobile + assessment.AttackPower )
                    {
                        alreadyAttacking = true;
                    }
                    else
                    {
                        attackAssessments.RemoveAt( 0 );
                        continue;
                    }
                }
                // If not strong enough, remove.
                else if ( assessment.AttackPower < assessment.StrengthRequired )
                {
                    attackAssessments.RemoveAt( 0 );
                    continue;
                }

                // If militia are already on the planet, pile in.
                if ( assessment.MilitiaOnPlanet )
                    alreadyAttacking = true;

                // Stop the attack if too many ships aren't ready, unless we're already attacking.
                int notReady = 0, ready = 0;

                for ( int x = 0; x < factionData.MilitiaLeaders.Count; x++ )
                {
                    // Skip checks if we're already attacking or have already gotten enough strength.
                    if ( alreadyAttacking )
                        break;

                    GameEntity_Squad post = World_AIW2.Instance.GetEntityByID_Squad( factionData.MilitiaLeaders[x] );
                    if ( post == null || post.GetCivilianMilitiaExt().Status != CivilianMilitiaStatus.Patrolling )
                        continue;

                    CivilianMilitia militiaData = post.GetCivilianMilitiaExt();

                    GameEntity_Squad centerpiece = World_AIW2.Instance.GetEntityByID_Squad( militiaData.Centerpiece );
                    if ( centerpiece == null )
                        continue;

                    // Skip if not an attacker.
                    bool isAttacker = false;
                    foreach ( Planet attacker in assessment.Attackers.Keys )
                        if ( centerpiece.Planet.Index == attacker.Index )
                        {
                            isAttacker = true;
                            break;
                        }

                    if ( !isAttacker )
                        continue;

                    // Prepare a movement command to gather our ships around a wormhole.
                    GameEntity_Other wormhole = centerpiece.Planet.GetWormholeTo( assessment.Target );

                    for ( int y = 0; y < militiaData.Ships.Count && !alreadyAttacking; y++ )
                    {
                        for ( int z = 0; z < militiaData.Ships[y].Count; z++ )
                        {
                            GameEntity_Squad entity = World_AIW2.Instance.GetEntityByID_Squad( militiaData.Ships[y][z] );
                            if ( entity == null || entity.LongRangePlanningData == null )
                                continue;

                            // Already attacking, stop checking and start raiding.
                            if ( entity.Planet.Index == assessment.Target.Index )
                            {
                                alreadyAttacking = true;
                                break;
                            }

                            // Skip centerpiece.
                            if ( centerpiece.PrimaryKeyID == entity.PrimaryKeyID )
                                continue;

                            // Get them moving if needed.
                            if ( entity.Planet.Index != centerpiece.Planet.Index )
                            {
                                notReady++;
                                if ( entity.LongRangePlanningData.FinalDestinationIndex != centerpiece.Planet.Index )
                                {
                                    QueueWormholeCommand( entity, centerpiece.Planet );
                                }
                            }
                            else if ( wormhole != null && wormhole.WorldLocation.GetExtremelyRoughDistanceTo( entity.WorldLocation ) > 5000
                                && (entity.Orders.QueuedOrders.Count == 0 || entity.Orders.QueuedOrders[0].RelatedPoint != wormhole.WorldLocation) )
                            {
                                notReady++;
                                // Create and add all required parts of a move to point command.
                                if ( wormhole != null )
                                {
                                    QueueMovementCommand( entity, wormhole.WorldLocation );
                                }
                            }
                            else
                                ready++;

                        }
                    }
                }
                // If 33% all of our ships are ready, or we're already raiding, its raiding time.
                if ( ready > notReady * 2 || alreadyAttacking )
                {
                    for ( int x = 0; x < factionData.MilitiaLeaders.Count; x++ )
                    {
                        GameEntity_Squad post = World_AIW2.Instance.GetEntityByID_Squad( factionData.MilitiaLeaders[x] );
                        if ( post == null || post.GetCivilianMilitiaExt().Status != CivilianMilitiaStatus.Patrolling )
                            continue;

                        CivilianMilitia militiaData = post.GetCivilianMilitiaExt();

                        GameEntity_Squad centerpiece = World_AIW2.Instance.GetEntityByID_Squad( militiaData.Centerpiece );
                        if ( centerpiece == null )
                            continue;

                        // Skip if not an attacker.
                        bool isAttacker = false;
                        foreach ( Planet attacker in assessment.Attackers.Keys )
                            if ( centerpiece.Planet.Index == attacker.Index )
                            {
                                isAttacker = true;
                                break;
                            }

                        if ( !isAttacker )
                            continue;

                        // We're here. The AI should release all of its forces to fight us.
                        BadgerFactionUtilityMethods.FlushUnitsFromReinforcementPoints( assessment.Target, faction, Context );
                        // Let the player know we're doing something, if our forces would matter.
                        if ( faction.Ex_MinorFactionCommon_GetPrimitives().Allegiance == "Player Team" && assessment.AttackPower > 5000 )
                        {
                            if ( !LastGameSecondForMessageAboutThisPlanet.GetHasKey( assessment.Target ) )
                                LastGameSecondForMessageAboutThisPlanet.AddPair( assessment.Target, 0 );
                            if ( World_AIW2.Instance.GameSecond - LastGameSecondForMessageAboutThisPlanet[assessment.Target] > 30 )
                            {
                                World_AIW2.Instance.QueueLogMessageCommand( "Civilian Militia are attacking " + assessment.Target.Name + ".", JournalEntryImportance.Normal, Context );
                                LastGameSecondForMessageAboutThisPlanet[assessment.Target] = World_AIW2.Instance.GameSecond;
                            }
                        }

                        isPatrolling.Add( centerpiece.Planet );
                        for ( int y = 0; y < militiaData.Ships.Count; y++ )
                        {
                            for ( int z = 0; z < militiaData.Ships[y].Count; z++ )
                            {
                                GameEntity_Squad entity = World_AIW2.Instance.GetEntityByID_Squad( militiaData.Ships[y][z] );
                                if ( entity == null || entity.LongRangePlanningData == null )
                                    continue;

                                // Skip centerpiece.
                                if ( centerpiece.PrimaryKeyID == entity.PrimaryKeyID )
                                    continue;

                                if ( entity.Planet.Index != assessment.Target.Index && entity.LongRangePlanningData.FinalDestinationIndex != assessment.Target.Index )
                                {
                                    if ( entity.Planet.Index != centerpiece.Planet.Index )
                                    {
                                        // Not yet on our target planet, and we're not yet on our centerpiece planet. Path to our centerpiece planet first.
                                        if ( entity.LongRangePlanningData.FinalDestinationIndex == centerpiece.Planet.Index )
                                            continue; // Stop if already moving towards it.

                                        QueueWormholeCommand( entity, centerpiece.Planet );
                                    }
                                    else
                                    {
                                        // Not yet on our target planet, and we're on our centerpice planet. Path to our target planet.
                                        QueueWormholeCommand( entity, assessment.Target );
                                    }
                                }
                            }
                        }
                    }
                }

                // If any of the planets involved in this attack are in other attacks, remove them from those other attacks.
                foreach ( Planet attackingPlanet in assessment.Attackers.Keys )
                {
                    for ( int y = 1; y < attackAssessments.Count; y++ )
                    {
                        if ( attackAssessments[y].Attackers.ContainsKey( attackingPlanet ) )
                            attackAssessments[y].Attackers.Remove( attackingPlanet );
                    }
                }

                attackAssessments.RemoveAt( 0 );
                attackAssessments.Sort();
            }
            #endregion

            #region Patrolling Actions
            // If we don't have an active defensive or offensive target, withdrawl back to the planet our leader is at.
            for ( int x = 0; x < factionData.MilitiaLeaders.Count; x++ )
            {
                GameEntity_Squad post = World_AIW2.Instance.GetEntityByID_Squad( factionData.MilitiaLeaders[x] );
                if ( post == null || post.GetCivilianMilitiaExt().Status != CivilianMilitiaStatus.Patrolling )
                    continue;

                CivilianMilitia militiaData = post.GetCivilianMilitiaExt();

                GameEntity_Squad centerpiece = World_AIW2.Instance.GetEntityByID_Squad( militiaData.Centerpiece );
                if ( centerpiece == null )
                    continue;

                if ( !isPatrolling.Contains( centerpiece.Planet ) )
                {
                    for ( int y = 0; y < militiaData.Ships.Count; y++ )
                    {
                        for ( int z = 0; z < militiaData.Ships[y].Count; z++ )
                        {
                            GameEntity_Squad entity = World_AIW2.Instance.GetEntityByID_Squad( militiaData.Ships[y][z] );
                            if ( entity == null )
                                continue;

                            // Skip centerpiece.
                            if ( centerpiece.PrimaryKeyID == entity.PrimaryKeyID )
                                continue;

                            var threat = factionData.GetThreat( entity.Planet );

                            // If we're not home, and our current planet does not have threat that we think we can beat, return.
                            // If any of the following are true, return.
                            // Current planet has no threat.
                            // Current planet has threat we can't beat.
                            // current planet is more than 1 hop away from our centerpiece.
                            if ( entity.Planet.Index != centerpiece.Planet.Index && entity.LongRangePlanningData.FinalDestinationIndex != centerpiece.Planet.Index &&
                                (entity.Planet.GetHopsTo( centerpiece.Planet ) > 1 || threat.Total <= 1000 ||
                                threat.MilitiaMobile + threat.MilitiaGuard + threat.FriendlyGuard + threat.FriendlyMobile < threat.Total * MilitiaAttackOverkillPercentage) )
                            {
                                QueueWormholeCommand( entity, centerpiece.Planet );
                            }
                        }
                    }
                }
            }
            #endregion
            Engine_Universal.NewTimingsBeingBuilt.FinishRememberingFrame( FramePartTimings.TimingType.ShortTermBackgroundThreadEntry, "DoMilitiaThreatReaction" );
        }

        // Update ship and turret caps for militia buildings.
        public void UpdateUnitCaps( Faction faction, ArcenLongTermIntermittentPlanningContext Context )
        {
            // Count the number of militia barracks for each planet.
            Dictionary<Planet, int> barracksPerPlanet = new Dictionary<Planet, int>();
            for ( int x = 0; x < World_AIW2.Instance.Galaxies[0].Planets.Count; x++ )
            {
                {
                    Planet planet = World_AIW2.Instance.Galaxies[0].Planets[x];
                    planet.DoForEntities( EntityRollupType.SpecialTypes, delegate ( GameEntity_Squad building )
                    {
                        if ( building.TypeData.SpecialType == SpecialEntityType.NPCFactionCenterpiece && building.TypeData.GetHasTag( "MilitiaBarracks" )
                        && building.SelfBuildingMetalRemaining <= 0 && building.SecondsSpentAsRemains <= 0 )
                            if ( barracksPerPlanet.ContainsKey( planet ) )
                                barracksPerPlanet[planet]++;
                            else
                                barracksPerPlanet.Add( planet, 1 );
                        return DelReturn.Continue;
                    } );
                }
            }
            // Handle once for each militia leader.
            for ( int x = 0; x < factionData.MilitiaLeaders.Count; x++ )
            {
                // Load its ship and status.
                GameEntity_Squad militiaShip = World_AIW2.Instance.GetEntityByID_Squad( factionData.MilitiaLeaders[x] );
                if ( militiaShip == null )
                    continue;
                CivilianMilitia militiaStatus = militiaShip.GetCivilianMilitiaExt();
                if ( militiaStatus.Status == CivilianMilitiaStatus.Defending )
                {
                    // For each type of unit, process.
                    for ( int y = 0; y < (int)CivilianResource.Length; y++ )
                    {
                        if ( militiaStatus.ShipTypeData[y] == "none" )
                            continue; // Skip if not yet loaded.

                        GameEntityTypeData turretData = GameEntityTypeDataTable.Instance.GetRowByName( militiaStatus.ShipTypeData[y], false, null );

                        militiaStatus.ShipCapacity[y] = (factionData.GetCap( faction ) / (FInt.Create( turretData.GetForMark( faction.GetGlobalMarkLevelForShipLine( turretData ) ).StrengthPerSquad, true ) / 10)).GetNearestIntPreferringHigher();
                        militiaShip.Planet.DoForLinkedNeighborsAndSelf( delegate ( Planet otherPlanet )
                        {
                            if ( barracksPerPlanet.ContainsKey( otherPlanet ) )
                                if ( turretData.MultiplierToAllFleetCaps == 0 )
                                    militiaStatus.ShipCapacity[y] += barracksPerPlanet[otherPlanet] * Math.Max( 1, (FInt.Create( militiaStatus.ShipCapacity[y], true ) / 3).GetNearestIntPreferringHigher() );
                                else
                                    militiaStatus.ShipCapacity[y] += barracksPerPlanet[otherPlanet] * Math.Max( (1 / turretData.MultiplierToAllFleetCaps).GetNearestIntPreferringHigher(), (FInt.Create( militiaStatus.ShipCapacity[y], true ) / 3).GetNearestIntPreferringHigher() );
                            return DelReturn.Continue;
                        } );
                        militiaStatus.ShipCapacity[y] = (int)(militiaStatus.ShipCapacity[y] * (militiaStatus.CapMultiplier / 100.0));
                    }
                }
                else if ( militiaStatus.Status == CivilianMilitiaStatus.Patrolling ) // If patrolling, do unit spawning.
                {
                    // For each type of unit, get ship count.
                    for ( int y = 0; y < (int)CivilianResource.Length; y++ )
                    {
                        if ( militiaStatus.ShipTypeData[y] == "none" )
                            continue; // Skip if not yet loaded.

                        GameEntityTypeData shipData = GameEntityTypeDataTable.Instance.GetRowByName( militiaStatus.ShipTypeData[y], false, null );

                        // If advanced, simply set to 1.
                        if ( militiaShip.TypeData.GetHasTag( "BuildsProtectors" ) )
                        {
                            militiaStatus.ShipCapacity[y] = 1;
                            continue;
                        }
                        militiaStatus.ShipCapacity[y] = (factionData.GetCap( faction ) / (FInt.Create( shipData.GetForMark( faction.GetGlobalMarkLevelForShipLine( shipData ) ).StrengthPerSquad, true ) / 10)).GetNearestIntPreferringHigher();
                        militiaShip.Planet.DoForLinkedNeighborsAndSelf( delegate ( Planet otherPlanet )
                        {
                            if ( barracksPerPlanet.ContainsKey( otherPlanet ) )
                                for ( int z = 0; z < barracksPerPlanet[otherPlanet]; z++ )
                                    if ( shipData.MultiplierToAllFleetCaps == 0 )
                                        militiaStatus.ShipCapacity[y] += Math.Max( 1, (FInt.Create( militiaStatus.ShipCapacity[y], true ) / 3).GetNearestIntPreferringHigher() );
                                    else
                                        militiaStatus.ShipCapacity[y] += Math.Max( (1 / shipData.MultiplierToAllFleetCaps).GetNearestIntPreferringHigher(), (FInt.Create( militiaStatus.ShipCapacity[y], true ) / 3).GetNearestIntPreferringHigher() );
                            return DelReturn.Continue;
                        } );
                        militiaStatus.ShipCapacity[y] = (int)(militiaStatus.ShipCapacity[y] * (militiaStatus.CapMultiplier / 100.0));
                    }
                }
            }
        }

        // A collection of every single wormhole command we want to execute.
        ArcenSparseLookup<Planet, ArcenSparseLookup<Planet, List<GameEntity_Squad>>> wormholeCommands;

        /// <summary>
        /// Add an entity that needs a wormhole move command.
        /// </summary>
        /// <param name="entity">The ship to move.</param>
        /// <param name="destination">The planet to move to.</param>
        private void QueueWormholeCommand( GameEntity_Squad entity, Planet destination )
        {
            Planet origin = entity.Planet;
            if ( !wormholeCommands.GetHasKey( origin ) )
                wormholeCommands.AddPair( origin, new ArcenSparseLookup<Planet, List<GameEntity_Squad>>() );
            if ( !wormholeCommands[origin].GetHasKey( destination ) )
                wormholeCommands[origin].AddPair( destination, new List<GameEntity_Squad>() );
            wormholeCommands[origin][destination].Add( entity );
        }

        public void ExecuteWormholeCommands( Faction faction, ArcenLongTermIntermittentPlanningContext Context )
        {
            for ( int x = 0; x < wormholeCommands.GetPairCount(); x++ )
            {
                ArcenSparseLookupPair<Planet, ArcenSparseLookup<Planet, List<GameEntity_Squad>>> originPair = wormholeCommands.GetPairByIndex( x );
                if ( originPair == null )
                    continue;
                Planet origin = originPair.Key;
                ArcenSparseLookup<Planet, List<GameEntity_Squad>> destinations = originPair.Value;
                for ( int y = 0; y < destinations.GetPairCount(); y++ )
                {
                    ArcenSparseLookupPair<Planet, List<GameEntity_Squad>> destinationPair = destinations.GetPairByIndex( y );
                    if ( destinationPair == null )
                        continue;
                    Planet destination = destinationPair.Key;
                    List<GameEntity_Squad> entities = destinationPair.Value;
                    if ( entities == null )
                        continue;
                    List<Planet> path = faction.FindPath( origin, destination, Context );
                    GameCommand command = GameCommand.Create( GameCommandTypeTable.Instance.GetRowByName( "SetWormholePath_CivilianIndustryBulk", false, null ), GameCommandSource.AnythingElse );
                    for ( int p = 0; p < path.Count; p++ )
                        command.RelatedIntegers.Add( path[p].Index );
                    for ( int z = 0; z < entities.Count; z++ )
                    {
                        GameEntity_Squad entity = entities[z];
                        if ( entity != null && entity.LongRangePlanningData.FinalDestinationIndex != destination.Index )
                            command.RelatedEntityIDs.Add( entity.PrimaryKeyID );
                    }
                    if ( command.RelatedEntityIDs.Count > 0 )
                        Context.QueueCommandForSendingAtEndOfContext( command );
                }
            }
            wormholeCommands = null;
        }

        // A collection of every single movement command we want to execute.
        ArcenSparseLookup<Planet, ArcenSparseLookup<ArcenPoint, List<GameEntity_Squad>>> movementCommands;

        /// <summary>
        /// Add an entity that needs a movement command.
        /// </summary>
        /// <param name="entity">The ship to move.</param>
        /// <param name="destination">The point to move to.</param>
        private void QueueMovementCommand( GameEntity_Squad entity, ArcenPoint destination )
        {
            Planet planet = entity.Planet;
            if ( !movementCommands.GetHasKey( planet ) )
                movementCommands.AddPair( planet, new ArcenSparseLookup<ArcenPoint, List<GameEntity_Squad>>() );
            if ( !movementCommands[planet].GetHasKey( destination ) )
                movementCommands[planet].AddPair( destination, new List<GameEntity_Squad>() );
            movementCommands[planet][destination].Add( entity );
        }

        public void ExecuteMovementCommands( Faction faction, ArcenLongTermIntermittentPlanningContext Context )
        {
            for ( int x = 0; x < movementCommands.GetPairCount(); x++ )
            {
                ArcenSparseLookupPair<Planet, ArcenSparseLookup<ArcenPoint, List<GameEntity_Squad>>> planetPair = movementCommands.GetPairByIndex( x );
                if ( planetPair == null )
                    continue;
                Planet planet = planetPair.Key;
                ArcenSparseLookup<ArcenPoint, List<GameEntity_Squad>> destinations = planetPair.Value;
                for ( int y = 0; y < destinations.GetPairCount(); y++ )
                {
                    ArcenSparseLookupPair<ArcenPoint, List<GameEntity_Squad>> destinationPair = destinations.GetPairByIndex( y );
                    if ( destinationPair == null )
                        continue;
                    ArcenPoint destination = destinationPair.Key;
                    List<GameEntity_Squad> entities = destinationPair.Value;
                    if ( entities == null )
                        continue;
                    GameCommand command = GameCommand.Create( GameCommandTypeTable.Instance.GetRowByName( "MoveManyToOnePoint_CivilianIndustryBulk", false, null ), GameCommandSource.AnythingElse );
                    command.RelatedPoints.Add( destination );
                    for ( int z = 0; z < entities.Count; z++ )
                    {
                        GameEntity_Squad entity = entities[z];
                        if ( entities != null && entity.LongRangePlanningData.DestinationPoint != destination )
                            command.RelatedEntityIDs.Add( entity.PrimaryKeyID );
                    }
                    if ( command.RelatedEntityIDs.Count > 0 )
                        Context.QueueCommandForSendingAtEndOfContext( command );
                }
            }
        }

        // Do NOT directly change anything from this function. Doing so may cause desyncs in multiplayer.
        // What you can do from here is queue up game commands for units, and send them to be done via QueueCommandForSendingAtEndOfContext.
        public override void DoLongRangePlanning_OnBackgroundNonSimThread_Subclass( Faction faction, ArcenLongTermIntermittentPlanningContext Context )
        {
            if ( faction.MustBeAwakenedByPlayer && !faction.HasBeenAwakenedByPlayer )
                return;

            if ( factionData == null )
                return; // Wait until we have our faction data ready to go.

            // Set up a list of all movement commands, and execute them once we're done.
            wormholeCommands = new ArcenSparseLookup<Planet, ArcenSparseLookup<Planet, List<GameEntity_Squad>>>();
            movementCommands = new ArcenSparseLookup<Planet, ArcenSparseLookup<ArcenPoint, List<GameEntity_Squad>>>();

            CalculateThreat( faction, Context );
            DoTradeRequests( faction, Context );
            DoCargoShipMovement( faction, Context );
            DoMilitiaConstructionShipMovement( faction, Context );
            DoMilitiaThreatReaction( faction, Context );
            UpdateUnitCaps( faction, Context );

            // Execute all of our movement commands.
            if ( wormholeCommands.GetPairCount() > 0 )
                ExecuteWormholeCommands( faction, Context );
            if ( movementCommands.GetPairCount() > 0 )
                ExecuteMovementCommands( faction, Context );
        }

        // Check for our stuff dying.
        public override void DoOnAnyDeathLogic( GameEntity_Squad entity, EntitySystem FiringSystemOrNull, ArcenSimContext Context )
        {
            // Skip if the ship was not defined by our mod.
            // Things like spawnt patrol ships and turrets don't need to be processed for death here.
            if ( !entity.TypeData.GetHasTag( "CivilianIndustryEntity" ) )
                return;

            // Deal with its death.
            if ( factionData.GrandStation == entity.PrimaryKeyID )
                factionData.GrandStationRebuildTimerInSeconds = 600;

            // Everything else; simply remove it from its respective list(s).
            if ( factionData.TradeStations.Contains( entity.PrimaryKeyID ) )
            {
                factionData.TradeStations.Remove( entity.PrimaryKeyID );
                factionData.SetTradeStationRebuildTimer( entity.Planet, 300 );
            }

            factionData.RemoveCargoShip( entity.PrimaryKeyID );

            if ( factionData.MilitiaLeaders.Contains( entity.PrimaryKeyID ) )
            {
                // Try to scrap all of its units.
                CivilianMilitia militiaData = entity.GetCivilianMilitiaExt();
                for ( int y = 0; y < militiaData.Ships.Count; y++ )
                {
                    for ( int z = 0; z < militiaData.Ships[z].Count; z++ )
                    {
                        GameEntity_Squad squad = World_AIW2.Instance.GetEntityByID_Squad( militiaData.Ships[y][z] );
                        if ( entity == null )
                            continue;
                        militiaData.Ships[y].RemoveAt( z );
                        z--;
                        squad.Despawn( Context, true, InstancedRendererDeactivationReason.SelfDestructOnTooHighOfCap );
                    }
                }
                factionData.MilitiaLeaders.Remove( entity.PrimaryKeyID );
            }

            // Save any changes.
            entity.PlanetFaction.Faction.SetCivilianFactionExt( factionData );
        }
    }
}
