using Arcen.AIW2.Core;
using Arcen.AIW2.External;
using Arcen.Universal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Arcen.AIW2.SK
{
    // Enum used to keep track of what our cargo and trade ships are doing.
    // Bare basic, used mostly for performance sake, so only ships that need to be processed for something are even considered valid targets.
    public enum CivilianShipStatus
    {
        Idle,           // Doing nothing.
        Loading,        // Loading resources into ship.
        Unloading,      // Offloading resources onto a station.
        Building,       // Offloading resources onto a militia building.
        Pathing,        // Pathing towards a requesting station.
        Enroute        // Taking resource to another trade station.
    }

    // Used for militia ships for most of the same reason as the above.
    // Slightly more potential actions however.
    public enum CivilianMilitiaStatus
    {
        Idle,               // Doing nothing.
        PathingForWormhole, // Pathing towards a trade station.
        PathingForMine,     // Pathing towards a trade station.
        EnrouteWormhole,    // Moving into position next to a wormhole to deploy.
        EnrouteMine,        // Moving into position next to a mine to deploy.
        Defending,          // In station form, requesting resources and building static defenses.
        Patrolling,         // A more mobile form of Defense, requests resources to build mobile strike fleets.
        PathingForShipyard // Pathing towards a trade station.
    }

    // Enum used to keep track of resources used in this mod.
    public enum CivilianResource
    {
        Ambuinum,
        Steel,
        Disrupeon,
        Protium,
        Tritium,
        Tungsten,
        Radium,
        Splackon,
        Silicon,
        Techrackum,
        Length
    }

    // Enum used to keep track of what ship requires what resource.
    public enum CivilianTech
    {
        Ambush,
        Concussion,
        Disruptive,
        Fusion,
        Generalist,
        Melee,
        Raid,
        Splash,
        Subterfuge,
        Technologist,
        Length
    }

    public static class CivilianResourceHexColors
    {
        public static string[] Color = new string[(int)CivilianResource.Length] {
            "9a9a9a",
            "43464B",
            "e0ca8b",
            "72eb6e",
            "e8e28b",
            "52689c",
            "8f1579",
            "a83e3e",
            "10adb3",
            "c2ffc6"
        };
    }

    // World storage class. Everything can be found from here.
    public class CivilianWorld
    {
        // Version of this class.
        public int Version;

        // Faction indexes with an active civilian industry.
        public List<int> Factions;

        // Have we generated resources yet?
        public bool GeneratedResources;

        // Helper function(s).
        // Get the faction that the sent index is for.
        public (bool valid, Faction faction, CivilianFaction factionData) getFactionInfo( int index )
        {
            Faction faction = World_AIW2.Instance.GetFactionByIndex( this.Factions[index] );
            if ( faction == null )
            {
                ArcenDebugging.SingleLineQuickDebug( "Civilian Industries - Failed to find faction for sent index." );
                return (false, null, null);
            }
            CivilianFaction factionData = faction.GetCivilianFactionExt();
            if ( factionData == null )
            {
                ArcenDebugging.SingleLineQuickDebug( "Civilian Industries - Failed to load faction data for found faction: " + faction.GetDisplayName() );
                return (false, faction, null);
            }
            return (true, faction, factionData);
        }

        // Following three functions are used for initializing, saving, and loading data.
        // Initialization function.
        // Default values. Called on creation, NOT on load.
        public CivilianWorld()
        {
            this.Factions = new List<int>();
            this.GeneratedResources = false;
        }
        // Saving our data.
        public void SerializeTo( ArcenSerializationBuffer Buffer )
        {
            Buffer.AddItem( 1 );
            // Lists require a special touch to save.
            // Get the number of items in the list, and store that as well.
            // This is so you know how many items you'll have to load later.
            int count = this.Factions.Count;
            Buffer.AddItem( count );
            for ( int x = 0; x < count; x++ )
                Buffer.AddItem( this.Factions[x] );
            Buffer.AddItem( GeneratedResources );
        }
        // Loading our data. Make sure the loading order is the same as the saving order.
        public CivilianWorld( ArcenDeserializationBuffer Buffer )
        {
            Version = Buffer.ReadInt32();
            // Lists require a special touch to load.
            // We'll have saved the number of items stored up above to be used here to determine the number of items to load.
            // ADDITIONALLY we'll need to recreate a blank list beforehand, as loading does not call the Initialization function.
            // Can't add values to a list that doesn't exist, after all.
            this.Factions = new List<int>();
            int count = Buffer.ReadInt32();
            for ( int x = 0; x < count; x++ )
                this.Factions.Add( Buffer.ReadInt32() );
            this.GeneratedResources = Buffer.ReadBool();
        }
    }

    // Individual storage class for each faction.
    public class CivilianFaction
    {
        // Version of this class.
        public int Version;

        // All values stored are the index value of ships. This is done as to simply the process of saving and loading.
        // We index all of our faction ships so that they can be easily looped through based on what we're doing.

        // Index of this faction's Grand Station.
        public int GrandStation;

        // Rebuild timer for Grand Station.
        public int GrandStationRebuildTimerInSeconds;

        // Index of all trade stations that belong to this faction.
        public List<int> TradeStations;

        // Rebuild timer for Trade Stations by planet index.
        public Dictionary<int, int> TradeStationRebuildTimerInSecondsByPlanet;

        // Functions for setting and getting rebuild timers.
        public int GetTradeStationRebuildTimer( Planet planet )
        {
            return GetTradeStationRebuildTimer( planet.Index );
        }
        public int GetTradeStationRebuildTimer( int planet )
        {
            if ( TradeStationRebuildTimerInSecondsByPlanet.ContainsKey( planet ) )
                return TradeStationRebuildTimerInSecondsByPlanet[planet];
            else
                return 0;
        }
        public void SetTradeStationRebuildTimer( Planet planet, int timer )
        {
            SetTradeStationRebuildTimer( planet.Index, timer );
        }
        public void SetTradeStationRebuildTimer( int planet, int timer )
        {
            if ( TradeStationRebuildTimerInSecondsByPlanet.ContainsKey( planet ) )
                TradeStationRebuildTimerInSecondsByPlanet[planet] = timer;
            else
                TradeStationRebuildTimerInSecondsByPlanet.Add( planet, timer );
        }

        // Index of all cargo ships that belong to this faction.
        public List<int> CargoShips;

        // List of all cargo ships by current status that belong to this faction.
        public List<int> CargoShipsIdle;
        public List<int> CargoShipsLoading;
        public List<int> CargoShipsUnloading;
        public List<int> CargoShipsBuilding;
        public List<int> CargoShipsPathing;
        public List<int> CargoShipsEnroute;

        // Index of all Militia Construction Ships and/or Militia Buildings
        public List<int> MilitiaLeaders;

        // Counter used to determine when another cargo ship should be built.
        public int BuildCounter;

        // Counter used to determine when another militia ship should be built.
        public int MilitiaCounter;

        // How long until the next raid?
        public int NextRaidInThisSeconds;

        // Index of wormholes for the next raid.
        public List<int> NextRaidWormholes;

        // Unlike every other value, the follow values are not stored and saved. They are simply regenerated whenever needed.
        // Contains the calculated threat value on every planet.
        // Calculated threat is all hostile strength - all friendly (excluding our own) strength.
        public List<ThreatReport> ThreatReports;
        public List<TradeRequest> ImportRequests;
        public List<TradeRequest> ExportRequests;

        // Get the threat value for a planet.
        public (int MilitiaGuard, int MilitiaMobile, int FriendlyGuard, int FriendlyMobile, int CloakedHostile, int NonCloakedHostile, int Wave, int Total) GetThreat( Planet planet )
        {
            try
            {
                // If reports aren't generated, return 0.
                if ( ThreatReports == null )
                    return (0, 0, 0, 0, 0, 0, 0, 0);
                else
                    for ( int x = 0; x < ThreatReports.Count; x++ )
                        if ( ThreatReports[x].Planet.Index == planet.Index )
                            return ThreatReports[x].GetThreat();
                // Planet not processed. Return 0.
                return (0, 0, 0, 0, 0, 0, 0, 0);
            }
            catch ( Exception e )
            {
                // Failed to return a report, return 0. Harmless, so we don't worry about informing the player.
                ArcenDebugging.SingleLineQuickDebug( e.Message );
                return (0, 0, 0, 0, 0, 0, 0, 0);
            }
        }
        // Calculate threat values every planet that our mobile forces are on or adjacent to.
        public void CalculateThreat( Faction faction, Faction playerFaction )
        {
            // Empty our dictionary.
            ThreatReports = new List<ThreatReport>();

            // Get the grand station's planet, to easily figure out when we're processing the home planet.
            GameEntity_Squad grandStation = World_AIW2.Instance.GetEntityByID_Squad( GrandStation );
            if ( grandStation == null )
                return;
            Planet grandPlanet = grandStation.Planet;
            if ( grandPlanet == null )
                return;

            List<int> processed = new List<int>();
            faction.Entities.DoForEntities( delegate ( GameEntity_Squad squad )
            {
                squad.Planet.DoForLinkedNeighborsAndSelf( delegate ( Planet planet )
                {
                    // Stop if its already processed.
                    if ( processed.Contains( planet.Index ) )
                        return DelReturn.Continue;

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
                    if ( planet.GetControllingFaction() == playerFaction )
                        nonCloakedHostileStrength += hostileStrengthData.TotalStrength * 3;
                    else // If on hostile planet, don't factor in stealth.
                    { 
                        nonCloakedHostileStrength += hostileStrengthData.TotalStrength - hostileStrengthData.CloakedStrength;
                        cloakedHostileStrength += hostileStrengthData.CloakedStrength;
                    }

                    // Adjacent planet threat matters as well, but not as much as direct threat.
                    // We'll only add it if the planet has no friendly forces on it.
                    if ( planet.GetControllingFaction() == playerFaction )
                        planet.DoForLinkedNeighbors( delegate ( Planet linkedPlanet )
                        {
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
                    ThreatReports.Add( new ThreatReport( planet, militiaGuardStrength, militiaMobileStrength, friendlyGuardStrength, friendlyMobileStrength, cloakedHostileStrength, nonCloakedHostileStrength, waveStrength ) );

                    // Add to the proccessed list.
                    processed.Add( planet.Index );

                    return DelReturn.Continue;
                } );
                return DelReturn.Continue;
            } );
            // Sort our reports.
            ThreatReports.Sort();
        }

        // Returns the base resource cost for ships.
        public int GetResourceCost( Faction faction )
        {
            // 51 - (Intensity ^ 1.5)
            return 51 - (int)Math.Pow( faction.Ex_MinorFactionCommon_GetPrimitives().Intensity, 1.5 );
        }

        // Returns the current capacity for turrets/ships.
        public int GetCap( Faction faction )
        {
            // ((baseCap + (AIP / AIPDivisor)) ^ (1 + (Intensity / IntensityDivisor)))
            int cap = 0;
            int baseCap = 20;
            int AIPDivisor = 2;
            int IntensityDivisor = 25;
            for ( int y = 0; y < World_AIW2.Instance.AIFactions.Count; y++ )
                cap = (int)(Math.Ceiling( Math.Pow( Math.Max( cap, baseCap + World_AIW2.Instance.AIFactions[y].GetAICommonExternalData().AIProgress_Total.ToInt() / AIPDivisor ),
                     1 + (faction.Ex_MinorFactionCommon_GetPrimitives().Intensity / IntensityDivisor) ) ));
            return cap;
        }

        // Return a cargo ship from any lists its in.
        public void RemoveCargoShip( int cargoShipID )
        {
            if ( this.CargoShips.Contains( cargoShipID ) )
                this.CargoShips.Remove( cargoShipID );
            if ( this.CargoShipsIdle.Contains( cargoShipID ) )
                this.CargoShipsIdle.Remove( cargoShipID );
            if ( this.CargoShipsLoading.Contains( cargoShipID ) )
                this.CargoShipsLoading.Remove( cargoShipID );
            if ( this.CargoShipsUnloading.Contains( cargoShipID ) )
                this.CargoShipsUnloading.Remove( cargoShipID );
            if ( this.CargoShipsBuilding.Contains( cargoShipID ) )
                this.CargoShipsBuilding.Remove( cargoShipID );
            if ( this.CargoShipsPathing.Contains( cargoShipID ) )
                this.CargoShipsPathing.Remove( cargoShipID );
            if ( this.CargoShipsEnroute.Contains( cargoShipID ) )
                this.CargoShipsEnroute.Remove( cargoShipID );
        }

        // Following three functions are used for initializing, saving, and loading data.
        // Initialization function.
        // Default values. Called on creation, NOT on load.
        public CivilianFaction()
        {
            this.GrandStation = -1;
            this.GrandStationRebuildTimerInSeconds = 0;
            this.TradeStations = new List<int>();
            this.TradeStationRebuildTimerInSecondsByPlanet = new Dictionary<int, int>();
            this.CargoShips = new List<int>();
            this.CargoShipsIdle = new List<int>();
            this.CargoShipsLoading = new List<int>();
            this.CargoShipsUnloading = new List<int>();
            this.CargoShipsBuilding = new List<int>();
            this.CargoShipsPathing = new List<int>();
            this.CargoShipsEnroute = new List<int>();
            this.MilitiaLeaders = new List<int>();
            this.BuildCounter = 0;
            this.MilitiaCounter = 0;
            this.NextRaidInThisSeconds = 1800;
            this.NextRaidWormholes = new List<int>();

            this.ThreatReports = new List<ThreatReport>();
            this.ImportRequests = new List<TradeRequest>();
            this.ExportRequests = new List<TradeRequest>();
        }
        // Serialize a list.
        private void SerializeList( List<int> list, ArcenSerializationBuffer Buffer )
        {
            // Lists require a special touch to save.
            // Get the number of items in the list, and store that as well.
            // This is so you know how many items you'll have to load later.
            int count = list.Count;
            Buffer.AddItem( count );
            for ( int x = 0; x < count; x++ )
                Buffer.AddItem( list[x] );
        }
        private void SerializeDictionary( Dictionary<int, int> dict, ArcenSerializationBuffer Buffer )
        {
            Buffer.AddItem( dict.Count );
            foreach ( int key in dict.Keys )
            {
                Buffer.AddItem( key );
                Buffer.AddItem( dict[key] );
            }
        }
        // Saving our data.
        public void SerializeTo( ArcenSerializationBuffer Buffer )
        {
            Buffer.AddItem( 1 );
            Buffer.AddItem( this.GrandStation );
            Buffer.AddItem( this.GrandStationRebuildTimerInSeconds );
            SerializeList( TradeStations, Buffer );
            SerializeDictionary( TradeStationRebuildTimerInSecondsByPlanet, Buffer );
            SerializeList( CargoShips, Buffer );
            SerializeList( MilitiaLeaders, Buffer );
            SerializeList( CargoShipsIdle, Buffer );
            SerializeList( CargoShipsLoading, Buffer );
            SerializeList( CargoShipsUnloading, Buffer );
            SerializeList( CargoShipsBuilding, Buffer );
            SerializeList( CargoShipsPathing, Buffer );
            SerializeList( CargoShipsEnroute, Buffer );
            Buffer.AddItem( this.BuildCounter );
            Buffer.AddItem( this.MilitiaCounter );
            Buffer.AddItem( this.NextRaidInThisSeconds );
            SerializeList( this.NextRaidWormholes, Buffer );
        }
        // Deserialize a list.
        public List<int> DeserializeList( ArcenDeserializationBuffer Buffer )
        {
            // Lists require a special touch to load.
            // We'll have saved the number of items stored up above to be used here to determine the number of items to load.
            // ADDITIONALLY we'll need to recreate a blank list beforehand, as loading does not call the Initialization function.
            // Can't add values to a list that doesn't exist, after all.
            int count = Buffer.ReadInt32();
            List<int> newList = new List<int>();
            for ( int x = 0; x < count; x++ )
                newList.Add( Buffer.ReadInt32() );
            return newList;
        }
        public Dictionary<int, int> DeserializeDictionary( ArcenDeserializationBuffer Buffer )
        {
            int count = Buffer.ReadInt32();
            Dictionary<int, int> newDict = new Dictionary<int, int>();
            for ( int x = 0; x < count; x++ )
            {
                int key = Buffer.ReadInt32();
                int value = Buffer.ReadInt32();
                if ( !newDict.ContainsKey( key ) )
                    newDict.Add( key, value );
                else
                    newDict[key] = value;
            }
            return newDict;
        }
        // Loading our data. Make sure the loading order is the same as the saving order.
        public CivilianFaction( ArcenDeserializationBuffer Buffer )
        {
            this.Version = Buffer.ReadInt32();
            this.GrandStation = Buffer.ReadInt32();
            this.GrandStationRebuildTimerInSeconds = Buffer.ReadInt32();
            this.TradeStations = DeserializeList( Buffer );
            this.TradeStationRebuildTimerInSecondsByPlanet = DeserializeDictionary( Buffer );
            this.CargoShips = DeserializeList( Buffer );
            this.MilitiaLeaders = DeserializeList( Buffer );
            this.CargoShipsIdle = DeserializeList( Buffer );
            this.CargoShipsLoading = DeserializeList( Buffer );
            this.CargoShipsUnloading = DeserializeList( Buffer );
            this.CargoShipsBuilding = DeserializeList( Buffer );
            this.CargoShipsPathing = DeserializeList( Buffer );
            this.CargoShipsEnroute = DeserializeList( Buffer );
            this.BuildCounter = Buffer.ReadInt32();
            this.MilitiaCounter = Buffer.ReadInt32();
            this.NextRaidInThisSeconds = Buffer.ReadInt32();
            this.NextRaidWormholes = DeserializeList( Buffer );

            // Recreate an empty list on load. Will be populated when needed.
            this.ThreatReports = new List<ThreatReport>();
            this.ImportRequests = new List<TradeRequest>();
            this.ExportRequests = new List<TradeRequest>();
        }
    }

    // Used to report on the amount of threat on each planet.
    public class ThreatReport : IComparable<ThreatReport>
    {
        public Planet Planet;
        public int MilitiaGuardStrength;
        public int MilitiaMobileStrength;
        public int FriendlyGuardStrength;
        public int FriendlyMobileStrength;
        public int CloakedHostileStrength;
        public int NonCloakedHostileStrength;
        public int WaveStrength;
        public int TotalStrength { get { return CloakedHostileStrength + NonCloakedHostileStrength; } }
        public (int MilitiaGuard, int MilitiaMobile, int FriendlyGuard, int FriendlyMobile, int CloakedHostileStrength, int NonCloakedHostileStrength, int WaveStrength, int TotalStrength) GetThreat()
        {
            return (MilitiaGuardStrength, MilitiaMobileStrength, FriendlyGuardStrength, FriendlyMobileStrength, CloakedHostileStrength, NonCloakedHostileStrength, WaveStrength, TotalStrength);
        }
        public ThreatReport( Planet planet, int militiaGuardStrength, int militiaMobileStrength, int friendlyGuardStrength, int friendlyMobileStrength, int cloakedHostileStrength, int nonCloakedHostileStrength, int waveStrength )
        {
            Planet = planet;
            MilitiaGuardStrength = militiaGuardStrength;
            MilitiaMobileStrength = militiaMobileStrength;
            FriendlyGuardStrength = friendlyGuardStrength;
            FriendlyMobileStrength = friendlyMobileStrength;
            CloakedHostileStrength = cloakedHostileStrength;
            NonCloakedHostileStrength = nonCloakedHostileStrength;
            WaveStrength = waveStrength;
        }

        public int CompareTo( ThreatReport other )
        {
            // We want higher threat to be first in a list, so reverse the normal sorting order.
            return other.TotalStrength.CompareTo( this.TotalStrength );
        }

        public override string ToString()
        {
            return "Planet: " + Planet + " MilitiaGuard: " + MilitiaGuardStrength + " MilitiaMobile: " + MilitiaMobileStrength + " FriendlyGuard: " + FriendlyGuardStrength +
                " FriendlyMobile: " + FriendlyMobileStrength + " CloakedHostile: " + CloakedHostileStrength + " NonCloakedHostile: " + NonCloakedHostileStrength +
                " Wave: " + WaveStrength + " Total: " + TotalStrength;
        }
    }

    // Used to report on how strong an attack would be on a hostile planet.
    public class AttackAssessment : IComparable<AttackAssessment>
    {
        public Planet Target;
        public Dictionary<Planet, int> Attackers;
        public int StrengthRequired;
        public bool MilitiaOnPlanet;
        public bool HasReinforcePoint;
        public int AttackPower { get { return (from o in Attackers select o.Value).Sum(); } }

        public AttackAssessment( Planet target, int strengthRequired, bool hasReinforcePoints )
        {
            Target = target;
            Attackers = new Dictionary<Planet, int>();
            StrengthRequired = strengthRequired;
            MilitiaOnPlanet = false;
            HasReinforcePoint = hasReinforcePoints;
        }
        public int CompareTo( AttackAssessment other )
        {
            // Planets that already have militia get higher priority. Reinforce ourselves.
            if ( MilitiaOnPlanet && !other.MilitiaOnPlanet )
                return -1;
            else if ( other.MilitiaOnPlanet && !MilitiaOnPlanet )
                return 1;
            else
                // We want higher threat to be first in a list, so reverse the normal sorting order.
                return other.StrengthRequired.CompareTo( this.StrengthRequired );
        }

        public override string ToString()
        {
            return "Target: " + Target.Name + " Attacker Count: " + Attackers.Count + " Strength Required:" + StrengthRequired + " Attack Power:" + AttackPower + " Militia Already On Planet? " + MilitiaOnPlanet.ToString();
        }
    }

    // Used on any entity which has resources.
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
            for ( int x = 0; x < this.Amount.Length; x++ )
            {
                this.Amount[x] = 0;
                this.Capacity[x] = 100;
                this.PerSecond[x] = 0;
            }
        }
        // Saving our data.
        public void SerializeTo( ArcenSerializationBuffer Buffer )
        {
            Buffer.AddItem( 1 );
            // Arrays
            // Get the number of items in the list, and store that as well.
            // This is so you know how many items you'll have to load later.
            // As we have one entry for each resource, we'll only have to get the count once.
            int count = this.Amount.Length;
            Buffer.AddItem( count );
            for ( int x = 0; x < count; x++ )
            {
                Buffer.AddItem( this.Amount[x] );
                Buffer.AddItem( this.Capacity[x] );
                Buffer.AddItem( this.PerSecond[x] );
            }
        }
        // Loading our data. Make sure the loading order is the same as the saving order.
        public CivilianCargo( ArcenDeserializationBuffer Buffer )
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
            for ( int x = 0; x < realCount; x++ )
            {
                if ( x >= savedCount )
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

    // Used for the creation and sorting of trade requests.
    public class TradeRequest : IComparable<TradeRequest>
    {
        // The resource to be requested. If length, means any.
        public CivilianResource Requested;

        // Resources to be declined, if above is length.
        public List<CivilianResource> Declined;

        // The urgency of the request.
        public int Urgency;

        // The station with this request.
        public GameEntity_Squad Station;

        // Maximum number of hops to look for trade in.
        public int MaxSearchHops;

        // Finished being processed.
        public bool Processed;

        public TradeRequest( CivilianResource request, List<CivilianResource> declined, int urgency, GameEntity_Squad station, int maxSearchHops )
        {
            Requested = request;
            Declined = declined;
            Urgency = urgency;
            Station = station;
            Processed = false;
            MaxSearchHops = maxSearchHops;
        }
        public TradeRequest( CivilianResource request, int urgency, GameEntity_Squad station, int maxSearchHops )
        {
            Requested = request;
            Declined = new List<CivilianResource>();
            Urgency = urgency;
            Station = station;
            Processed = false;
            MaxSearchHops = maxSearchHops;
        }

        public int CompareTo( TradeRequest other )
        {
            // We want higher urgencies to be first in a list, so reverse the normal sorting order.
            return other.Urgency.CompareTo( this.Urgency );
        }

        public override string ToString()
        {
            string output = "Requested: " + Requested.ToString() + " Urgency: " + Urgency + " Planet: " + Station.Planet.Name + " Processed: " + Processed;
            if ( Declined.Count > 0 )
            {
                output += " Declined: ";
                for ( int x = 0; x < Declined.Count; x++ )
                    output += Declined[x].ToString() + " ";
            }
            return "Requested:" + Requested.ToString() + " Urgency:" + Urgency + " Planet:" + Station.Planet.Name + " Processed:" + Processed;
        }
    }

    // Used on mobile ships. Tells us what they're currently doing.
    public class CivilianStatus
    {
        public int Version;

        // The ship's current status.
        public CivilianShipStatus Status;

        // The index of the requesting station.
        // If -1, its being sent from the grand station.
        public int Origin;

        // The index of the ship's destination station, if any.
        public int Destination;

        // The amount of time left before departing from a loading job.
        // Usually 2 minutes.
        public int LoadTimer;

        // Following three functions are used for initializing, saving, and loading data.
        // Initialization function.
        // Default values. Called on creation, NOT on load.
        public CivilianStatus()
        {
            this.Status = CivilianShipStatus.Idle;
            this.Origin = -1;
            this.Destination = -1;
            this.LoadTimer = 0;
        }
        // Saving our data.
        public void SerializeTo( ArcenSerializationBuffer Buffer )
        {
            Buffer.AddItem( 1 );
            Buffer.AddItem( (int)this.Status );
            Buffer.AddItem( this.Origin );
            Buffer.AddItem( this.Destination );
            Buffer.AddItem( this.LoadTimer );
        }
        // Loading our data. Make sure the loading order is the same as the saving order.
        public CivilianStatus( ArcenDeserializationBuffer Buffer )
        {
            this.Version = Buffer.ReadInt32();
            this.Status = (CivilianShipStatus)Buffer.ReadInt32();
            this.Origin = Buffer.ReadInt32();
            this.Destination = Buffer.ReadInt32();
            this.LoadTimer = Buffer.ReadInt32();
        }
    }

    // Used on militia fleets. Tells us what their focus is.
    public class CivilianMilitia
    {
        public int Version;

        // The centerpiece of the flee.t
        public int Centerpiece;

        // The status of the fleet.
        public CivilianMilitiaStatus Status;

        // The planet that this fleet's focused on.
        // It will only interact to hostile forces on or adjacent to this.
        public int PlanetFocus;

        // Wormhole that this fleet has been assigned to. If -1, it will instead find an unclaimed mine on the planet.
        public int EntityFocus;

        // GameEntityTypeData that this militia builds, a list of every ship of that type under their control, and their capacity.
        public Dictionary<int, string> ShipTypeData;
        public Dictionary<int, List<int>> Ships;
        public Dictionary<int, int> ShipCapacity;

        public int GetShipCount( string entityTypeDataInternalName )
        {
            int index = -1;
            for ( int x = 0; x < ShipTypeData.Count; x++ )
                if ( ShipTypeData[x] == entityTypeDataInternalName )
                {
                    index = x;
                    break;
                }
            if ( index == -1 )
                return 0;
            int shipCount = 0;
            for ( int x = 0; x < Ships[index].Count; x++ )
            {
                GameEntity_Squad squad = World_AIW2.Instance.GetEntityByID_Squad( Ships[index][x] );
                if ( squad == null )
                    continue;
                shipCount++;
                shipCount += squad.ExtraStackedSquadsInThis;
            }
            return shipCount;
        }

        // Multipliers for various things.
        public int CostMultiplier;
        public int CapMultiplier;

        // Following three functions are used for initializing, saving, and loading data.
        // Initialization function.
        // Default values. Called on creation, NOT on load.
        public CivilianMilitia()
        {
            this.Centerpiece = -1;
            this.Status = CivilianMilitiaStatus.Idle;
            this.PlanetFocus = -1;
            this.EntityFocus = -1;
            this.ShipTypeData = new Dictionary<int, string>();
            this.Ships = new Dictionary<int, List<int>>();
            this.ShipCapacity = new Dictionary<int, int>();
            for ( int x = 0; x < (int)CivilianResource.Length; x++ )
            {
                this.ShipTypeData.Add( x, "none" );
                this.Ships.Add( x, new List<int>() );
                this.ShipCapacity.Add( x, 0 );
            }
            this.CostMultiplier = 100; // 100%
            this.CapMultiplier = 100; // 100%
        }
        // Saving our data.
        public void SerializeTo( ArcenSerializationBuffer Buffer )
        {
            Buffer.AddItem( 1 );
            Buffer.AddItem( this.Centerpiece );
            Buffer.AddItem( (int)this.Status );
            Buffer.AddItem( this.PlanetFocus );
            Buffer.AddItem( this.EntityFocus );
            int count = (int)CivilianResource.Length;
            Buffer.AddItem( count );
            for ( int x = 0; x < count; x++ )
            {
                Buffer.AddItem( this.ShipTypeData[x] );
                int subCount = this.Ships[x].Count;
                Buffer.AddItem( subCount );
                for ( int y = 0; y < subCount; y++ )
                    Buffer.AddItem( this.Ships[x][y] );
                Buffer.AddItem( this.ShipCapacity[x] );
            }
            Buffer.AddItem( this.CostMultiplier );
            Buffer.AddItem( this.CapMultiplier );
        }
        // Loading our data. Make sure the loading order is the same as the saving order.
        public CivilianMilitia( ArcenDeserializationBuffer Buffer )
        {
            this.Version = Buffer.ReadInt32();
            this.Centerpiece = Buffer.ReadInt32();
            this.Status = (CivilianMilitiaStatus)Buffer.ReadInt32();
            this.PlanetFocus = Buffer.ReadInt32();
            this.EntityFocus = Buffer.ReadInt32();
            this.ShipTypeData = new Dictionary<int, string>();
            this.Ships = new Dictionary<int, List<int>>();
            this.ShipCapacity = new Dictionary<int, int>();
            int count = Buffer.ReadInt32();
            for ( int x = 0; x < count; x++ )
            {
                this.ShipTypeData.Add( x, Buffer.ReadString() );
                this.Ships[x] = new List<int>();
                int subCount = Buffer.ReadInt32();
                for ( int y = 0; y < subCount; y++ )
                    this.Ships[x].Add( Buffer.ReadInt32() );
                this.ShipCapacity[x] = Buffer.ReadInt32();
            }
            if ( this.ShipTypeData.Count < (int)CivilianResource.Length )
            {
                for ( int x = count; x < (int)CivilianResource.Length; x++ )
                {
                    this.ShipTypeData.Add( x, "none" );
                    this.Ships.Add( x, new List<int>() );
                    this.ShipCapacity.Add( x, 0 );
                }
            }
            this.CostMultiplier = Buffer.ReadInt32();
            this.CapMultiplier = Buffer.ReadInt32();
        }

        public GameEntity_Squad getMine()
        {
            return World_AIW2.Instance.GetEntityByID_Squad( this.EntityFocus );
        }

        public GameEntity_Other getWormhole()
        {
            return World_AIW2.Instance.GetEntityByID_Other( this.EntityFocus );
        }
    }

    public class CivilianPlanet
    {
        // Version of ths class.
        public int Version;

        // What resource this planet has.
        public CivilianResource Resource;

        public CivilianPlanet()
        {
            Resource = CivilianResource.Length;
        }
        // Saving our data.
        public void SerializeTo( ArcenSerializationBuffer Buffer )
        {
            Buffer.AddItem( 1 );
            Buffer.AddItem( (int)this.Resource );
        }
        // Loading our data. Make sure the loading order is the same as the saving order.
        public CivilianPlanet( ArcenDeserializationBuffer Buffer )
        {
            this.Version = Buffer.ReadInt32();
            this.Resource = (CivilianResource)Buffer.ReadInt32();
        }
    }

    // Description classes.
    // Grand Stations
    // Used to display faction-related info to the player.
    public class GrandStationDescriptionAppender : IGameEntityDescriptionAppender
    {
        public void AddToDescriptionBuffer( GameEntity_Squad RelatedEntityOrNull, GameEntityTypeData RelatedEntityTypeData, ArcenDoubleCharacterBuffer Buffer )
        {
            // Make sure we are getting an entity.
            if ( RelatedEntityOrNull == null )
                return;
            // Need to find our faction data to display information.
            // Look through our world data, first, to find which faction controls our starting station, and load its faction data.
            CivilianWorld worldData = World.Instance.GetCivilianWorldExt();
            CivilianFaction factionData = null;
            // Look through our saved factions to find which one has our starting station
            for ( int x = 0; x < worldData.Factions.Count; x++ )
            {
                var tempData = worldData.getFactionInfo( x );
                if ( tempData.factionData.GrandStation == RelatedEntityOrNull.PrimaryKeyID )
                {
                    factionData = tempData.factionData;
                }
            }

            // If we found our faction data, inform them about build requests in the faction
            if ( factionData != null )
            {
                int baseCost = factionData.GetResourceCost( RelatedEntityOrNull.PlanetFaction.Faction );
                int cargoCost = (int)(baseCost + (baseCost * (factionData.CargoShips.Count / 10.0)));
                int percForCargo = (int)Math.Min( 100, 100.0 * factionData.BuildCounter / cargoCost );
                int militiaCost = (int)(baseCost + (baseCost * (factionData.MilitiaLeaders.Count / 10.0)));
                int percForMilitia = (int)Math.Min( 100, 100.0 * factionData.MilitiaCounter / militiaCost );
                Buffer.Add( "\n" + percForCargo + "% to next Cargo Ship." );
                Buffer.Add( "\n" + percForMilitia + "% to next Militia Construction Ship." );
            }

            // Add in an empty line to stop any other gunk (such as the fleet display) from messing up our given information.
            Buffer.Add( "\n" );
            return;
        }
    }

    // Trade Stations
    // Used to display stored cargo
    public class TradeStationDescriptionAppender : IGameEntityDescriptionAppender
    {
        public void AddToDescriptionBuffer( GameEntity_Squad RelatedEntityOrNull, GameEntityTypeData RelatedEntityTypeData, ArcenDoubleCharacterBuffer Buffer )
        {
            // Make sure we are getting an entity.
            if ( RelatedEntityOrNull == null )
                return;
            // Load our cargo data.
            CivilianCargo cargoData = RelatedEntityOrNull.GetCivilianCargoExt();

            // Inform them about what the station has on it.
            for ( int x = 0; x < cargoData.Amount.Length; x++ )
            {
                Buffer.StartColor( CivilianResourceHexColors.Color[x] );
                if ( cargoData.Amount[x] > 0 || cargoData.PerSecond[x] != 0 )
                    Buffer.Add( "\n" + cargoData.Amount[x] + "/" + cargoData.Capacity[x] + " " + ((CivilianResource)x).ToString() );
                Buffer.EndColor();
                // If resource has generation or drain, notify them.
                if ( cargoData.PerSecond[x] > 0 )
                {
                    int income = cargoData.PerSecond[x] + RelatedEntityOrNull.CurrentMarkLevel;
                    Buffer.Add( " +" + income + " per second" );
                }
            }

            // Add in an empty line to stop any other gunk (such as the fleet display) from messing up our given information.
            Buffer.Add( "\n" );
            return;
        }
    }

    // Cargo Ships
    // Used to display stored cargo and the ship's status
    public class CargoShipDescriptionAppender : IGameEntityDescriptionAppender
    {
        public void AddToDescriptionBuffer( GameEntity_Squad RelatedEntityOrNull, GameEntityTypeData RelatedEntityTypeData, ArcenDoubleCharacterBuffer Buffer )
        {
            // Make sure we are getting an entity.
            if ( RelatedEntityOrNull == null )
                return;
            // Need to find our faction data to display information.
            // Look through our world data, first, to find which faction controls our starting station, and load its faction data.
            CivilianWorld worldData = World.Instance.GetCivilianWorldExt();
            CivilianFaction factionData = null;
            // Look through our saved factions to find which one has our starting station
            for ( int x = 0; x < worldData.Factions.Count; x++ )
            {
                var tempData = worldData.getFactionInfo( x );
                if ( tempData.factionData.CargoShips.Contains( RelatedEntityOrNull.PrimaryKeyID ) )
                    factionData = tempData.factionData;
            }

            // Load our cargo data.
            CivilianCargo cargoData = RelatedEntityOrNull.GetCivilianCargoExt();
            // Load our status data.
            CivilianStatus shipStatus = RelatedEntityOrNull.GetCivilianStatusExt();

            // Inform them about what the ship is doing.
            Buffer.Add( "\nThis ship is currently " + shipStatus.Status.ToString() );
            // If currently pathing or enroute, continue to explain towards where
            if ( shipStatus.Status == CivilianShipStatus.Enroute )
                Buffer.Add( " towards " + World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Destination ).GetQualifiedName() + " on planet " + World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Destination ).Planet.Name );
            if ( shipStatus.Status == CivilianShipStatus.Pathing )
                Buffer.Add( " towards " + World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Origin ).Planet.Name );
            // Inform them about what the ship has on it.
            for ( int x = 0; x < cargoData.Amount.Length; x++ )
                if ( cargoData.Amount[x] > 0 )
                {
                    Buffer.StartColor( CivilianResourceHexColors.Color[x] );
                    Buffer.Add( "\n" + cargoData.Amount[x] + "/" + cargoData.Capacity[x] + " " + ((CivilianResource)x).ToString() );
                    Buffer.EndColor();
                }
            // Add in an empty line to stop any other gunk (such as the fleet display) from messing up our given information.
            Buffer.Add( "\n" );
            return;
        }
    }

    // Miltia Ships
    // Used to display defensive focuses and ship's status.
    public class MilitiaShipDescriptionAppender : IGameEntityDescriptionAppender
    {
        public void AddToDescriptionBuffer( GameEntity_Squad RelatedEntityOrNull, GameEntityTypeData RelatedEntityTypeData, ArcenDoubleCharacterBuffer Buffer )
        {
            // Make sure we are getting an entity.
            if ( RelatedEntityOrNull == null )
                return;
            // Load our militia data
            CivilianMilitia militiaData = RelatedEntityOrNull.GetCivilianMilitiaExt();
            CivilianCargo cargoData = RelatedEntityOrNull.GetCivilianCargoExt();

            // In order to find our player faction (which we'll need to display the ship capacity, as its based on aip)
            // We'll have to load our world data.
            CivilianWorld worldData = World.Instance.GetCivilianWorldExt();
            Faction playerFaction = null;
            CivilianFaction factionData = null;
            // Look through our saved factions to find which one has our militia ship
            for ( int x = 0; x < worldData.Factions.Count; x++ )
            {
                CivilianFaction tempData = worldData.getFactionInfo( x ).factionData;
                if ( tempData.MilitiaLeaders.Contains( RelatedEntityOrNull.PrimaryKeyID ) )
                {
                    playerFaction = worldData.getFactionInfo( x ).faction;
                    factionData = playerFaction.GetCivilianFactionExt();
                }
            }

            if ( factionData == null || playerFaction == null )
                return;

            // Inform them about any focus the ship may have.
            GameEntity_Squad centerpiece = World_AIW2.Instance.GetEntityByID_Squad( militiaData.Centerpiece );
            if ( centerpiece != null && centerpiece.PrimaryKeyID != RelatedEntityOrNull.PrimaryKeyID )
                Buffer.Add( " This structure is producing ships for " + centerpiece.FleetMembership.Fleet.GetName() + " on the planet " + centerpiece.Planet.Name + "." );
            else
            {
                Planet targetPlanet = World_AIW2.Instance.GetPlanetByIndex( militiaData.PlanetFocus );
                if ( targetPlanet != null )
                    Buffer.Add( " This ship's planetary focus is " + targetPlanet.Name );
                else
                    Buffer.Add( " This ship is currently waiting for a protection request." );
            }

            if ( militiaData.Ships.Count > 0 )
            {
                for ( int x = 0; x < (int)CivilianResource.Length; x++ )
                {
                    GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRowByName( militiaData.ShipTypeData[x], false, null );
                    if ( entityData != null )
                    {
                        int count = militiaData.GetShipCount( entityData.InternalName );
                        Buffer.Add( "\n" + entityData.DisplayName + ":" );
                        Buffer.StartColor( UnityEngine.Color.green );
                        Buffer.Add( " " + count + "/" + militiaData.ShipCapacity[x] );
                        Buffer.EndColor();
                        Buffer.StartColor( CivilianResourceHexColors.Color[x] );
                        Buffer.Add( " (" + (CivilianResource)x + ")" );
                        Buffer.EndColor();

                        int cost = 0;
                        if ( RelatedEntityOrNull.TypeData.GetHasTag( "BuildsProtectors" ) )
                            cost = (int)(12000 * SpecialFaction_SKCivilianIndustry.CostIntensityModifier( RelatedEntityOrNull.PlanetFaction.Faction ));
                        else
                        {
                            double countCostModifier = 1.0 + (1.0 - ((militiaData.ShipCapacity[x] - count + 1.0) / militiaData.ShipCapacity[x]));
                            int baseCost = entityData.CostForAIToPurchase;
                            cost = (int)(SpecialFaction_SKCivilianIndustry.CostIntensityModifier( RelatedEntityOrNull.PlanetFaction.Faction ) * (baseCost * countCostModifier * (militiaData.CostMultiplier / 100.0)));
                        }

                        if ( count < militiaData.ShipCapacity[x] )
                        {
                            double perc = Math.Min( 100, 100.0 * (1.0 * cargoData.Amount[x] / cost) );
                            Buffer.Add( " " + perc.ToString( "0.##" ) + "% (Building)" );
                        }
                        else
                        {
                            double perc = Math.Min( 100, 100.0 * (1.0 * cargoData.Amount[x] / cargoData.Capacity[x]) );
                            Buffer.Add( " " + perc.ToString( "0.##" ) + "% (Stockpiling)" );
                        }
                        Buffer.EndColor();
                    }
                }
            }

            // Add in an empty line to stop any other gunk (such as the fleet display) from messing up our given information.
            Buffer.Add( "\n" );
            return;
        }
    }

    // The main faction class.
    public class SpecialFaction_SKCivilianIndustry : BaseSpecialFaction
    {
        // Information required for our faction.
        // General identifier for our faction.
        protected override string TracingName => "SKCivilianIndustry";

        // Let the game know we're going to want to use the DoLongRangePlanning_OnBackgroundNonSimThread_Subclass function.
        // This function is generally used for things that do not need to always run, such as navigation requests.
        protected override bool EverNeedsToRunLongRangePlanning => true;

        // When was the last time we sent a journel message? To update the player about civies are doing.
        protected ArcenSparseLookup<Planet, int> LastGameSecondForMessageAboutThisPlanet;
        protected ArcenSparseLookup<Planet, int> LastGameSecondForLastTachyonBurstOnThisPlanet;

        public static CivilianWorld worldData;

        public SpecialFaction_SKCivilianIndustry() : base()
        {
            worldData = null;
            LastGameSecondForMessageAboutThisPlanet = new ArcenSparseLookup<Planet, int>();
            LastGameSecondForLastTachyonBurstOnThisPlanet = new ArcenSparseLookup<Planet, int>();
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
            for ( int i = 0; i < World_AIW2.Instance.Factions.Count; i++ ) // Go through a list of all factions in the game.
            {
                Faction otherFaction = World_AIW2.Instance.Factions[i];
                if ( faction == otherFaction ) // Found ourself.
                    continue; // Shun ourself.
                switch ( otherFaction.Type )
                {
                    case FactionType.AI: // Hostile to AI.
                        faction.MakeHostileTo( otherFaction );
                        otherFaction.MakeHostileTo( faction );
                        break;
                    case FactionType.SpecialFaction: // Hostile to other non player factions.
                        faction.MakeHostileTo( otherFaction );
                        otherFaction.MakeHostileTo( faction );
                        break;
                    case FactionType.Player: // Friendly to players. This entire faction is used for all players, and should thus be friendly to all.
                        faction.MakeFriendlyTo( otherFaction );
                        otherFaction.MakeFriendlyTo( faction );
                        break;
                }
            }
        }

        // Handle stack splitting logic.
        public override void DoOnStackSplit( GameEntity_Squad originalSquad, GameEntity_Squad newSquad )
        {
            // If we have no world data, uh-oh, we won't be able to find where they're supposed to go.
            // Eeventually, add some sort of fallback logic for militia ships. For now, just skip em.
            if ( SpecialFaction_SKCivilianIndustry.worldData != null )
            {
                // Find out what fleet the original unit is in, and add our new squad to it.
                for ( int x = 0; x < worldData.Factions.Count; x++ )
                {
                    // Load in this faction's data.
                    (bool valid, Faction playerFaction, CivilianFaction factionData) = worldData.getFactionInfo( x );
                    if ( !valid )
                        continue;

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
        }

        // Handle the creation of the Grand Station.
        public void CreateGrandStation( Faction faction, Faction playerFaction, CivilianFaction factionData, ArcenSimContext Context )
        {
            // Look through the game's list of king units.
            World_AIW2.Instance.DoForEntities( EntityRollupType.KingUnitsOnly, delegate ( GameEntity_Squad kingEntity )
             {
                 // Make sure its the correct faction.
                 if ( kingEntity.PlanetFaction.Faction.FactionIndex != playerFaction.FactionIndex )
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
        }

        // Handle creation of trade stations.
        public void CreateTradeStations( Faction faction, Faction playerFaction, CivilianFaction factionData, CivilianWorld worldData, ArcenSimContext Context )
        {
            playerFaction.Entities.DoForEntities( EntityRollupType.CommandStation, delegate ( GameEntity_Squad commandStation )
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
                     if ( station == null )
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
        }

        // Add buildings for the player to build.
        public void AddMilitiaBuildings( Faction faction, Faction playerFaction, CivilianFaction factionData, ArcenSimContext Context )
        {
            playerFaction.Entities.DoForEntities( EntityRollupType.Battlestation, delegate ( GameEntity_Squad battlestation )
             {
                 if ( battlestation.TypeData.IsBattlestation ) // Will hopefully fix a weird bug where planets could get battlestation buildings.
                 {
                     // Add buildings to the battlestation/citadel's build list.
                     GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "MilitiaHeadquarters" );
                     Fleet.Membership mem = battlestation.FleetMembership.Fleet.GetOrAddMembershipGroupBasedOnSquadType_AssumeNoDuplicates( entityData );
                     mem.ExplicitBaseSquadCap = 1;

                     entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "TradePost" );
                     mem = battlestation.FleetMembership.Fleet.GetOrAddMembershipGroupBasedOnSquadType_AssumeNoDuplicates( entityData );
                     mem.ExplicitBaseSquadCap = 5;

                     entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "MilitiaProtectorShipyards" );
                     mem = battlestation.FleetMembership.Fleet.GetOrAddMembershipGroupBasedOnSquadType_AssumeNoDuplicates( entityData );
                     mem.ExplicitBaseSquadCap = 1;
                 }

                 return DelReturn.Continue;
             } );
            playerFaction.DoForControlledPlanets( delegate ( Planet planet )
            {
                // Add buildings to the planet's build list.
                GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "MilitiaBarracks" );

                // Attempt to add to the planet's build list.
                Fleet.Membership mem = planet.GetCommandStationOrNull().FleetMembership.Fleet.GetOrAddMembershipGroupBasedOnSquadType_AssumeNoDuplicates( entityData );

                // Set the building caps.
                mem.ExplicitBaseSquadCap = 3;

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
        public void ScanForMilitiaBuildings( Faction faction, Faction playerFaction, CivilianFaction factionData, CivilianWorld worldData, ArcenSimContext Context )
        {
            playerFaction.Entities.DoForEntities( EntityRollupType.SpecialTypes, delegate ( GameEntity_Squad entity )
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
        public void DoResources( Faction faction, Faction playerFaction, CivilianFaction factionData, ArcenSimContext Context )
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
        public void DoShipSpawns( Faction faction, Faction playerFaction, CivilianFaction factionData, ArcenSimContext Context )
        {
            // Continue only if starting station is valid.
            if ( factionData.GrandStation == -1 || factionData.GrandStation == -2 )
                return;

            // Load our grand station.
            GameEntity_Squad grandStation = World_AIW2.Instance.GetEntityByID_Squad( factionData.GrandStation );

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

                // Spawn in the ship.
                GameEntity_Squad entity = GameEntity_Squad.CreateNew( pFaction, entityData, entityData.MarkFor( pFaction ), pFaction.FleetUsedAtPlanet, 0, spawnPoint, Context );

                // Add the cargo ship to our faction data.
                factionData.CargoShips.Add( entity.PrimaryKeyID );
                factionData.CargoShipsIdle.Add( entity.PrimaryKeyID );

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
        public void DoShipArrival( Faction faction, Faction playerFaciton, CivilianFaction factionData, ArcenSimContext Context )
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
                    factionData.CargoShipsEnroute.Remove( cargoShip.PrimaryKeyID );
                    factionData.CargoShipsIdle.Add( cargoShip.PrimaryKeyID );
                    shipStatus.Status = CivilianShipStatus.Idle;
                    cargoShip.SetCivilianStatusExt( shipStatus );
                    x--;
                    continue;
                }

                // If ship not at destination planet yet, do nothing.
                if ( cargoShip.Planet.Index != destinationStation.Planet.Index )
                    continue;

                // If ship is close to destination station, start unloading.
                if ( cargoShip.GetDistanceTo_ExpensiveAccurate( destinationStation.WorldLocation, true, true ) < 2000 )
                {
                    factionData.CargoShipsEnroute.Remove( cargoShip.PrimaryKeyID );
                    if ( factionData.TradeStations.Contains( destinationStation.PrimaryKeyID ) )
                    {
                        shipStatus.Status = CivilianShipStatus.Unloading;
                        factionData.CargoShipsUnloading.Add( cargoShip.PrimaryKeyID );
                        shipStatus.LoadTimer = 120;
                    }
                    else if ( factionData.MilitiaLeaders.Contains( destinationStation.PrimaryKeyID ) )
                    {
                        shipStatus.Status = CivilianShipStatus.Building;
                        factionData.CargoShipsBuilding.Add( cargoShip.PrimaryKeyID );
                        shipStatus.LoadTimer = 120;
                    }
                    else
                    {
                        shipStatus.Status = CivilianShipStatus.Idle;
                        factionData.CargoShipsIdle.Add( cargoShip.PrimaryKeyID );
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
                    factionData.CargoShipsPathing.Remove( cargoShip.PrimaryKeyID );
                    factionData.CargoShipsIdle.Add( cargoShip.PrimaryKeyID );
                    shipStatus.Status = CivilianShipStatus.Idle;
                    cargoShip.SetCivilianStatusExt( shipStatus );
                    x--;
                    continue;
                }

                // If ship not at origin planet yet, do nothing.
                if ( cargoShip.Planet.Index != originStation.Planet.Index )
                    continue;

                // If ship is close to origin station, start loading.
                if ( cargoShip.GetDistanceTo_ExpensiveAccurate( originStation.WorldLocation, true, true ) < 2000 )
                {
                    factionData.CargoShipsPathing.Remove( cargoShip.PrimaryKeyID );
                    factionData.CargoShipsLoading.Add( cargoShip.PrimaryKeyID );
                    shipStatus.Status = CivilianShipStatus.Loading;
                    shipStatus.LoadTimer = 120;
                    x--;
                    cargoShip.SetCivilianStatusExt( shipStatus );
                }
            }
        }

        // Handle resource transferring.
        public void DoResourceTransfer( Faction faction, Faction playerFaction, CivilianFaction factionData, ArcenSimContext Context )
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
                    factionData.CargoShipsLoading.Remove( cargoShip.PrimaryKeyID );
                    factionData.CargoShipsIdle.Add( cargoShip.PrimaryKeyID );
                    shipStatus.Status = CivilianShipStatus.Idle;
                    cargoShip.SetCivilianStatusExt( shipStatus );
                    x--;
                    continue;
                }
                CivilianCargo originCargo = originStation.GetCivilianCargoExt();
                bool isFinished = true;

                // Send the resources, if the station has any left.
                for ( int y = 0; y < (int)CivilianResource.Length; y++ )
                {
                    // When loading up resources, we should try to take as much from the station as we can.
                    if ( originCargo.PerSecond[y] <= 0 )
                    {
                        if ( originCargo.Amount[y] > 0 && shipCargo.Amount[y] < shipCargo.Capacity[y] )
                        {
                            shipCargo.Amount[y]++;
                            originCargo.Amount[y]--;
                            isFinished = false;
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
                        isFinished = false;
                    }
                }

                // If load timer hit 0, or we're finished stop loading.
                if ( shipStatus.LoadTimer <= 0 || isFinished )
                {
                    // If none of our resources are over half, stop.
                    bool hasEnough = false;
                    for ( int y = 0; y < (int)CivilianResource.Length; y++ )
                        if ( shipCargo.Amount[y] >= shipCargo.Capacity[y] / 2 )
                        {
                            hasEnough = true;
                            break;
                        }
                    if ( hasEnough )
                    {
                        factionData.CargoShipsLoading.Remove( cargoShip.PrimaryKeyID );
                        factionData.CargoShipsEnroute.Add( cargoShip.PrimaryKeyID );
                        shipStatus.Status = CivilianShipStatus.Enroute;
                    }
                    else
                    {
                        factionData.CargoShipsLoading.Remove( cargoShip.PrimaryKeyID );
                        factionData.CargoShipsIdle.Add( cargoShip.PrimaryKeyID );
                        shipStatus.Status = CivilianShipStatus.Idle;
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
                    factionData.CargoShipsUnloading.Remove( cargoShip.PrimaryKeyID );
                    factionData.CargoShipsIdle.Add( cargoShip.PrimaryKeyID );
                    shipStatus.Status = CivilianShipStatus.Idle;
                    cargoShip.SetCivilianStatusExt( shipStatus );
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
                    factionData.CargoShipsUnloading.Remove( cargoShip.PrimaryKeyID );
                    factionData.CargoShipsIdle.Add( cargoShip.PrimaryKeyID );
                    shipStatus.Status = CivilianShipStatus.Idle;
                    cargoShip.SetCivilianStatusExt( shipStatus );
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
                    factionData.CargoShipsBuilding.Remove( cargoShip.PrimaryKeyID );
                    factionData.CargoShipsIdle.Add( cargoShip.PrimaryKeyID );
                    shipStatus.Status = CivilianShipStatus.Idle;
                    cargoShip.SetCivilianStatusExt( shipStatus );
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
                    factionData.CargoShipsBuilding.Remove( cargoShip.PrimaryKeyID );
                    factionData.CargoShipsIdle.Add( cargoShip.PrimaryKeyID );
                    shipStatus.Status = CivilianShipStatus.Idle;
                    cargoShip.SetCivilianStatusExt( shipStatus );
                    x--;
                }
            }
        }

        // Handle assigning militia to our ThreatReports.
        public void DoMilitiaAssignment( Faction faction, Faction playerFaction, CivilianFaction factionData, ArcenSimContext Context )
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

                // If not our planet, skip.
                if ( factionData.ThreatReports[x].Planet.GetControllingFaction() != playerFaction )
                    continue;

                // If we ran out of free militia, update our request.
                if ( freeMilitia.Count == 0 )
                {
                    factionData.MilitiaCounter++;
                    continue;
                }

                // See if any wormholes are still unassigned.
                GameEntity_Other foundWormhole = null;
                factionData.ThreatReports[x].Planet.DoForLinkedNeighbors( delegate ( Planet otherPlanet )
                 {
                     // Get its wormhole.
                     GameEntity_Other wormhole = factionData.ThreatReports[x].Planet.GetWormholeTo( otherPlanet );
                     if ( wormhole == null )
                         return DelReturn.Continue;

                     // Skip if too close to the planet's command station.
                     bool isClose = false;
                     factionData.ThreatReports[x].Planet.DoForEntities( EntityRollupType.CommandStation, delegate ( GameEntity_Squad command )
                      {
                          if ( wormhole.WorldLocation.GetDistanceTo( command.WorldLocation, true ) <= 6000 )
                              isClose = true;
                          return DelReturn.Continue;
                      } );
                     if ( isClose )
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
                         if ( otherPlanet.GetControllingFaction().GetIsHostileTowards( faction ) )
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
        public void DoMilitiaDeployment( Faction faction, Faction playerFaction, CivilianFaction factionData, ArcenSimContext Context )
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
                         if ( factionData.TradeStations.Contains( entity.PrimaryKeyID ) && entity.TypeData.GetHasTag( "TradeStation" ) )
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
                        int range = 10000;
                        if ( stationDist > range * 0.3 &&
                            (stationDist > range || wormDist < range) )
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
                                GameEntity_Squad entity = GameEntity_Squad.CreateNew( pFaction, turretData, turretData.MarkFor( pFaction ), pFaction.FleetUsedAtPlanet, 0, spawnPoint, Context );

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
                                    GameEntity_Squad entity = GameEntity_Squad.CreateNew( pFaction, shipData, shipData.MarkFor( pFaction ), pFaction.FleetUsedAtPlanet, 0, militiaShip.WorldLocation, Context );

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
        public void PrepareAIRaid( Faction faction, Faction playerFaction, CivilianFaction factionData, ArcenSimContext Context )
        {
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
        public void DoAIRaid( Faction faction, Faction playerFaction, CivilianFaction factionData, ArcenSimContext Context )
        {
            if ( factionData.NextRaidWormholes.Count == 0 )
            {
                PrepareAIRaid( faction, playerFaction, factionData, Context );
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
            // Update mark levels every half a minute.
            if ( World_AIW2.Instance.GameSecond % 60 == 0 )
            {
                faction.InheritsTechUpgradesFromPlayerFactions = true;
                faction.RecalculateMarkLevelsAndInheritedTechUnlocks();
                faction.Entities.DoForEntities( delegate ( GameEntity_Squad entity )
                 {
                     entity.SetCurrentMarkLevelIfHigherThanCurrent( faction.GetGlobalMarkLevelForShipLine( entity.TypeData ), Context );
                     return DelReturn.Continue;
                 } );
            }

            // Update faction relations. Generally a good idea to have this in your DoPerSecondLogic function since other factions can also change their allegiances.
            allyThisFactionToHumans( faction );

            // Load our data.
            // Start with world.
            CivilianWorld worldData = World.Instance.GetCivilianWorldExt();
            if ( worldData == null )
                return;

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

            // Make sure we have a faction entry in our global data for every player faction in game.
            for ( int i = 0; i < World_AIW2.Instance.Factions.Count; i++ )
            {
                Faction otherFaction = World_AIW2.Instance.Factions[i];
                if ( otherFaction.Type == FactionType.Player && !worldData.Factions.Contains( otherFaction.FactionIndex ) )
                    worldData.Factions.Add( otherFaction.FactionIndex );
            }

            // Next, do logic once for each faction that has a registered industry.
            for ( int x = 0; x < worldData.Factions.Count; x++ )
            {
                // Load in this faction's data.
                (bool valid, Faction playerFaction, CivilianFaction factionData) = worldData.getFactionInfo( x );
                if ( !valid )
                    continue;

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
                for(int y = 0; y < LastGameSecondForMessageAboutThisPlanet.GetPairCount(); y++ )
                {
                    Planet planet = LastGameSecondForMessageAboutThisPlanet.GetPairByIndex( x ).Key;
                    int lastSecond = LastGameSecondForMessageAboutThisPlanet[planet];

                    if ( !LastGameSecondForLastTachyonBurstOnThisPlanet.GetHasKey( planet ) )
                        LastGameSecondForLastTachyonBurstOnThisPlanet.AddPair( planet, lastSecond );

                    if (World_AIW2.Instance.GameSecond - LastGameSecondForLastTachyonBurstOnThisPlanet[planet] >= 30 )
                    {
                        var threat = factionData.GetThreat( planet );
                        if ( threat.CloakedHostile > threat.Total * 0.9 )
                            BadgerFactionUtilityMethods.TachyonBlastPlanet( planet, faction );
                        LastGameSecondForLastTachyonBurstOnThisPlanet[planet] = World_AIW2.Instance.GameSecond;
                    }
                }

                // Grand Station creation.
                while ( factionData.GrandStation == -1 && factionData.GrandStationRebuildTimerInSeconds == 0 )
                    CreateGrandStation( faction, playerFaction, factionData, Context );

                // Handle spawning of trade stations.
                CreateTradeStations( faction, playerFaction, factionData, worldData, Context );

                // Add buildings for the player to build.
                if ( World_AIW2.Instance.GameSecond % 15 == 0 )
                    AddMilitiaBuildings( faction, playerFaction, factionData, Context );

                // Scan for any new buildings that the player has placed related to the mod.
                ScanForMilitiaBuildings( faction, playerFaction, factionData, worldData, Context );

                // Handle basic resource generation. (Resources with no requirements, ala Goods or Ore.)
                DoResources( faction, playerFaction, factionData, Context );

                // Handle the creation of ships.
                DoShipSpawns( faction, playerFaction, factionData, Context );

                // Check for ship arrival.
                DoShipArrival( faction, playerFaction, factionData, Context );

                // Handle resource transfering.
                DoResourceTransfer( faction, playerFaction, factionData, Context );

                // Calculate threat as needed.
                factionData.CalculateThreat( faction, playerFaction );

                // Handle assigning militia to our ThreatReports.
                DoMilitiaAssignment( faction, playerFaction, factionData, Context );

                // Handle militia deployment and unit building.
                DoMilitiaDeployment( faction, playerFaction, factionData, Context );

                // Handle AI response. Have some variation on wave timers.
                if ( factionData.NextRaidInThisSeconds > 120 )
                    factionData.NextRaidInThisSeconds = Math.Max( 120, factionData.NextRaidInThisSeconds - Context.RandomToUse.Next( 1, 3 ) );
                else if ( factionData.NextRaidInThisSeconds > 0 )
                    factionData.NextRaidInThisSeconds -= Context.RandomToUse.Next( 1, 3 );

                // Prepare (and warn the player about) an upcoming raid.
                if ( factionData.NextRaidInThisSeconds == 120 )
                    PrepareAIRaid( faction, playerFaction, factionData, Context );

                // Raid!
                if ( factionData.NextRaidInThisSeconds <= 0 )
                    DoAIRaid( faction, playerFaction, factionData, Context );

                // Save our faction data.
                playerFaction.SetCivilianFactionExt( factionData );
            }

            // Save our world data.
            World.Instance.SetCivilianWorldExt( worldData );

            // Make it globally available.
            SpecialFaction_SKCivilianIndustry.worldData = worldData;
        }

        // Handle station requests.
        private const int BASE_MIL_URGENCY = 20;
        private const int MIL_URGENCY_REDUCTION_PER_REGULAR = 8;
        private const int MIL_URGENCY_REDUCTION_PER_LARGE = 4;

        private const int BASE_CIV_URGENCY = 5;
        private const int CIV_URGENCY_REDUCTION_PER_REGULAR = 1;
        public void DoTradeRequests( Faction faction, Faction playerFaction, CivilianFaction factionData, ArcenLongTermIntermittentPlanningContext Context )
        {
            // If no free cargo ships, increment build counter and stop.
            if ( factionData.CargoShipsIdle.Count == 0 )
            {
                factionData.BuildCounter += (factionData.MilitiaLeaders.Count + factionData.TradeStations.Count);
                return;
            }
            Engine_Universal.NewTimingsBeingBuilt.StartRememberingFrame( FramePartTimings.TimingType.ShortTermBackgroundThreadEntry, "DoTradeRequests" );
            // Clear our lists.
            factionData.ImportRequests = new List<TradeRequest>();
            factionData.ExportRequests = new List<TradeRequest>();
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

                // Don't request resources we're full on.
                List<CivilianResource> fullOf = new List<CivilianResource>();
                CivilianCargo militiaCargo = militia.GetCivilianCargoExt();
                for ( int y = 0; y < militiaCargo.Amount.Length; y++ )
                    if ( militiaCargo.Amount[y] > 0 && militiaCargo.Amount[y] >= militiaCargo.Capacity[y] )
                        fullOf.Add( (CivilianResource)y );

                // Stop if we're full of everything.
                if ( fullOf.Count == (int)CivilianResource.Length )
                    continue;

                // See if we already have cargo ships enroute.
                int cargoEnroute = 0;
                for ( int y = 0; y < factionData.CargoShips.Count; y++ )
                {
                    GameEntity_Squad cargoShip = World_AIW2.Instance.GetEntityByID_Squad( factionData.CargoShips[y] );
                    if ( cargoShip == null )
                    {
                        factionData.RemoveCargoShip( factionData.CargoShips[y] );
                        y--;
                        continue;
                    }
                    if ( cargoShip.GetCivilianStatusExt().Status == CivilianShipStatus.Idle )
                        continue;
                    if ( cargoShip.GetCivilianStatusExt().Destination == militia.PrimaryKeyID )
                    {
                        cargoEnroute++;
                    }
                }

                int urgency = BASE_MIL_URGENCY;
                if ( militia.TypeData.GetHasTag( "BuildsProtectors" ) ) // Allow more inbound ships for larger projects.
                    urgency -= MIL_URGENCY_REDUCTION_PER_LARGE * cargoEnroute;
                else
                    urgency -= MIL_URGENCY_REDUCTION_PER_REGULAR * cargoEnroute;

                // Add a request for any resource.
                factionData.ImportRequests.Add( new TradeRequest( CivilianResource.Length, fullOf, urgency, militia, 1 ) );
            }
            #endregion

            #region Trade Station Imports and Exports

            // If no free cargo ships, increment build counter and stop.
            if ( factionData.CargoShipsIdle.Count == 0 )
            {
                factionData.BuildCounter += (factionData.TradeStations.Count);
                Engine_Universal.NewTimingsBeingBuilt.FinishRememberingFrame( FramePartTimings.TimingType.ShortTermBackgroundThreadEntry, "DoTradeRequests" );
                return;
            }

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

                int incomingForPickup = 0;
                int incomingForDropoff = 0;
                // Lower urgency for each ship inbound to pickup.
                for ( int z = 0; z < factionData.CargoShips.Count; z++ )
                {
                    GameEntity_Squad cargoShip = World_AIW2.Instance.GetEntityByID_Squad( factionData.CargoShips[z] );
                    if ( cargoShip == null )
                        continue;
                    CivilianStatus cargoStatus = cargoShip.GetCivilianStatusExt();
                    if ( (cargoStatus.Status == CivilianShipStatus.Enroute || cargoStatus.Status == CivilianShipStatus.Unloading) && cargoStatus.Destination == factionData.TradeStations[x] )
                        incomingForDropoff++;
                    if ( (cargoStatus.Status == CivilianShipStatus.Pathing || cargoStatus.Status == CivilianShipStatus.Loading) && cargoStatus.Origin == factionData.TradeStations[x] )
                        incomingForPickup++;
                }

                // Check each type of cargo seperately.
                for ( int y = 0; y < requesterCargo.PerSecond.Length; y++ )
                {
                    // Skip if we don't accept it.
                    if ( requesterCargo.Capacity[y] <= 0 )
                        continue;

                    // Resources we generate.
                    if ( requesterCargo.PerSecond[y] > 0 )
                    {
                        // Generates urgency based on how close to full capacity we are.
                        if ( requesterCargo.Amount[y] > 100 )
                        {
                            int urgency = ((int)Math.Ceiling( (1.0 * requesterCargo.Amount[y] / requesterCargo.Capacity[y]) * (requesterCargo.PerSecond[y] * 2) )) - incomingForPickup;

                            if ( urgency > 0 )
                                factionData.ExportRequests.Add( new TradeRequest( (CivilianResource)y, urgency, requester, 5 ) );
                        }
                    }
                    // Resource we store. Simply put out a super tiny order to import/export based on current stores.
                    else if ( requesterCargo.Amount[y] >= requesterCargo.Capacity[y] * 0.5 )
                    {
                        int urgency = BASE_CIV_URGENCY;
                        urgency -= incomingForPickup * CIV_URGENCY_REDUCTION_PER_REGULAR;

                        if ( urgency > 0 )
                            factionData.ExportRequests.Add( new TradeRequest( (CivilianResource)y, 0, requester, 3 ) );
                    }
                    else if ( requesterCargo.Amount[y] < requesterCargo.Capacity[y] * 0.5 )
                    {
                        int urgency = BASE_CIV_URGENCY;
                        urgency -= incomingForDropoff * CIV_URGENCY_REDUCTION_PER_REGULAR;

                        factionData.ImportRequests.Add( new TradeRequest( (CivilianResource)y, urgency, requester, 3 ) );
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

            // Initially limit the number of hops to search through, to try and find closer matches to start with.
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
                    break;
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
                    factionData.CargoShipsIdle.Remove( foundCargoShip.PrimaryKeyID );
                    factionData.CargoShipsEnroute.Add( foundCargoShip.PrimaryKeyID );
                    CivilianStatus cargoShipStatus = foundCargoShip.GetCivilianStatusExt();
                    cargoShipStatus.Origin = -1;    // No origin station required.
                    cargoShipStatus.Destination = requestingEntity.PrimaryKeyID;
                    cargoShipStatus.Status = CivilianShipStatus.Enroute;
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

                    factionData.CargoShipsIdle.Remove( foundCargoShip.PrimaryKeyID );
                    factionData.CargoShipsPathing.Add( foundCargoShip.PrimaryKeyID );
                    cargoShipStatus.Status = CivilianShipStatus.Pathing;
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
                factionData.BuildCounter += (factionData.ImportRequests.Count + factionData.TradeStations.Count);

            #endregion

            Engine_Universal.NewTimingsBeingBuilt.FinishRememberingFrame( FramePartTimings.TimingType.MainSimThreadNormal, "DoTradeRequests" );
        }

        // Handle movement of cargo ships to their orign and destination points.
        public void DoCargoShipMovement( Faction faction, Faction playerFaction, CivilianFaction factionData, ArcenLongTermIntermittentPlanningContext Context )
        {
            // Loop through each of our cargo ships.
            for ( int x = 0; x < factionData.CargoShips.Count; x++ )
            {
                // Load the ship and its status.
                GameEntity_Squad ship = World_AIW2.Instance.GetEntityByID_Squad( factionData.CargoShips[x] );
                if ( ship == null )
                    continue;
                CivilianStatus shipStatus = ship.GetCivilianStatusExt();
                if ( shipStatus == null )
                    continue;

                switch ( shipStatus.Status )
                {
                    case CivilianShipStatus.Loading:
                    case CivilianShipStatus.Pathing:
                        // Ship currently moving towards origin station.
                        GameEntity_Squad originStation = World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Origin );
                        if ( originStation == null )
                            continue;
                        Planet originPlanet = originStation.Planet;

                        // Check if already on planet.
                        if ( ship.Planet.Index == originPlanet.Index )
                        {
                            if ( originStation.GetDistanceTo_ExpensiveAccurate( ship.WorldLocation, true, true ) < 2000 )
                                break; // Stop if already close enough.
                            if ( ship.Orders.QueuedOrders.Count > 0
                            && originStation.GetDistanceTo_ExpensiveAccurate( ship.Orders.QueuedOrders[0].RelatedPoint, true, true ) < 2000 )
                                break; // Stop if already enroute.

                            // On planet. Begin pathing towards the station.
                            // Tell the game what kind of command we want to do.
                            // Here, we'll be using the self descriptive MoveManyToOnePoint command.
                            // Note: Despite saying Many, it is also used for singular movement commands.
                            GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.MoveManyToOnePoint], GameCommandSource.AnythingElse );

                            // Let the game know where we want to move to. In this case, to our origin station's location.
                            command.RelatedPoints.Add( originStation.WorldLocation );

                            // Have the command apply to our ship.
                            command.RelatedEntityIDs.Add( ship.PrimaryKeyID );

                            // Tell the game to apply our command.
                            Context.QueueCommandForSendingAtEndOfContext( command );
                        }
                        else
                        {
                            if ( ship.LongRangePlanningData != null && ship.LongRangePlanningData.FinalDestinationIndex == originPlanet.Index )
                                break; // Stop if already enroute.

                            // Not on planet yet, prepare wormhole navigation.
                            // Tell the game wehat kind of command we want to do.
                            // Here we'll be using the self descriptive SetWormholePath command.
                            GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_NPCSingleUnit], GameCommandSource.AnythingElse );

                            // For wormhole pathing, we'll need to get our path from here to our goal.
                            FactionCommonExternalData factionExternal = faction.GetCommonExternal();
                            PlanetPathfinder pathfinder, pathfinderMain;
                            factionExternal.GetPathfindersOfRelevanceFromContext( Context, out pathfinder, out pathfinderMain );

                            List<Planet> path = pathfinder.FindPath( ship.Planet, originPlanet, 0, 0, Context );

                            // Set the goal to the next planet in our path.
                            command.RelatedIntegers.Add( path[1].Index );

                            // Have the command apply to our ship.
                            command.RelatedEntityIDs.Add( ship.PrimaryKeyID );

                            // Tell the game to apply our command.
                            Context.QueueCommandForSendingAtEndOfContext( command );
                        }
                        break;
                    case CivilianShipStatus.Unloading:
                    case CivilianShipStatus.Building:
                    case CivilianShipStatus.Enroute:
                        // Enroute movement.
                        // ship currently moving towards destination station.
                        GameEntity_Squad destinationStation = World_AIW2.Instance.GetEntityByID_Squad( shipStatus.Destination );
                        if ( destinationStation == null )
                            continue;
                        Planet destinationPlanet = destinationStation.Planet;

                        // Check if already on planet.
                        if ( ship.Planet.Index == destinationPlanet.Index )
                        {
                            if ( destinationStation.GetDistanceTo_ExpensiveAccurate( ship.WorldLocation, true, true ) < 2000 )
                                break; // Stop if already close enough.
                            if ( ship.Orders.QueuedOrders.Count > 0
                            && destinationStation.GetDistanceTo_ExpensiveAccurate( ship.Orders.QueuedOrders[0].RelatedPoint, true, true ) < 2000 )
                                break; // Stop if already enroute.

                            // On planet. Begin pathing towards the station.
                            // Tell the game what kind of command we want to do.
                            // Here, we'll be using the self descriptive MoveManyToOnePoint command.
                            // Note: Despite saying Many, it is also used for singular movement commands.
                            GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.MoveManyToOnePoint], GameCommandSource.AnythingElse );

                            // Let the game know where we want to move to. In this case, to our d station's location.
                            command.RelatedPoints.Add( destinationStation.WorldLocation );

                            // Have the command apply to our ship.
                            command.RelatedEntityIDs.Add( ship.PrimaryKeyID );

                            // Tell the game to apply our command.
                            Context.QueueCommandForSendingAtEndOfContext( command );
                        }
                        else
                        {
                            if ( ship.LongRangePlanningData != null && ship.LongRangePlanningData.FinalDestinationIndex == destinationPlanet.Index )
                                break; // Stop if already enroute.

                            // Not on planet yet, prepare wormhole navigation.
                            // Tell the game wehat kind of command we want to do.
                            // Here we'll be using the self descriptive SetWormholePath command.
                            GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_NPCSingleUnit], GameCommandSource.AnythingElse );

                            // For wormhole pathing, we'll need to get our path from here to our goal.
                            FactionCommonExternalData factionExternal = faction.GetCommonExternal();
                            PlanetPathfinder pathfinder, pathfinderMain;
                            factionExternal.GetPathfindersOfRelevanceFromContext( Context, out pathfinder, out pathfinderMain );
                            List<Planet> path = pathfinder.FindPath( ship.Planet, destinationPlanet, 0, 0, Context );

                            // Set the goal to the next planet in our path.
                            command.RelatedIntegers.Add( path[1].Index );

                            // Have the command apply to our ship.
                            command.RelatedEntityIDs.Add( ship.PrimaryKeyID );

                            // Tell the game to apply our command.
                            Context.QueueCommandForSendingAtEndOfContext( command );
                        }
                        break;
                    case CivilianShipStatus.Idle:
                    default:
                        break;
                }
            }
        }

        // Handle movement of militia construction ships.
        public void DoMilitiaConstructionShipMovement( Faction faction, Faction playerFaction, CivilianFaction factionData, ArcenLongTermIntermittentPlanningContext Context )
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
                             if ( factionData.TradeStations.Contains( entity.PrimaryKeyID ) && entity.TypeData.GetHasTag( "TradeStation" ) )
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

                        // Tell the game what kind of command we want to do.
                        // Here, we'll be using the self descriptive MoveManyToOnePoint command.
                        // Note: Despite saying Many, it is also used for singular movement commands.
                        GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.MoveManyToOnePoint], GameCommandSource.AnythingElse );

                        // Let the game know where we want to move to. In this case, to our origin station's location.
                        command.RelatedPoints.Add( goalStation.WorldLocation );

                        // Have the command apply to our ship.
                        command.RelatedEntityIDs.Add( ship.PrimaryKeyID );

                        // Tell the game to apply our command.
                        Context.QueueCommandForSendingAtEndOfContext( command );
                    }
                    else
                    {
                        // Not on planet yet, prepare wormhole navigation.
                        if ( ship.LongRangePlanningData != null && ship.LongRangePlanningData.FinalDestinationIndex == planet.Index )
                            continue; // Stop if we're already enroute.

                        // Tell the game wehat kind of command we want to do.
                        // Here we'll be using the self descriptive SetWormholePath command.
                        GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_NPCSingleUnit], GameCommandSource.AnythingElse );

                        // For wormhole pathing, we'll need to get our path from here to our goal.
                        FactionCommonExternalData factionExternal = faction.GetCommonExternal();
                        PlanetPathfinder pathfinder, pathfinderMain;
                        factionExternal.GetPathfindersOfRelevanceFromContext( Context, out pathfinder, out pathfinderMain );
                        List<Planet> path = pathfinder.FindPath( ship.Planet, planet, 0, 0, Context );

                        // Set the goal to the next planet in our path.
                        command.RelatedIntegers.Add( path[1].Index );

                        // Have the command apply to our ship.
                        command.RelatedEntityIDs.Add( ship.PrimaryKeyID );

                        // Tell the game to apply our command.
                        Context.QueueCommandForSendingAtEndOfContext( command );
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

                    // Tell the game what kind of command we want to do.
                    // Here, we'll be using the self descriptive MoveManyToOnePoint command.
                    // Note: Despite saying Many, it is also used for singular movement commands.
                    GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.MoveManyToOnePoint], GameCommandSource.AnythingElse );

                    // Let the game know where we want to move to.
                    command.RelatedPoints.Add( point );

                    // Have the command apply to our ship.
                    command.RelatedEntityIDs.Add( ship.PrimaryKeyID );

                    // Tell the game to apply our command.
                    Context.QueueCommandForSendingAtEndOfContext( command );
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

                    // Tell the game what kind of command we want to do.
                    // Here, we'll be using the self descriptive MoveManyToOnePoint command.
                    // Note: Despite saying Many, it is also used for singular movement commands.
                    GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.MoveManyToOnePoint], GameCommandSource.AnythingElse );

                    // Let the game know where we want to move to.
                    command.RelatedPoints.Add( mine.WorldLocation );

                    // Have the command apply to our ship.
                    command.RelatedEntityIDs.Add( ship.PrimaryKeyID );

                    // Tell the game to apply our command.
                    Context.QueueCommandForSendingAtEndOfContext( command );
                }
            }
        }

        // Handle reactive moevement of patrolling ship fleets.
        public void DoMilitiaThreatReaction( Faction faction, Faction playerFaction, CivilianFaction factionData, ArcenLongTermIntermittentPlanningContext Context )
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

                // If self or an adjacent friendly planet has hostile units on it that outnumber friendly defenses, including incoming waves, protect it.
                for ( int y = 0; y < factionData.ThreatReports.Count && targetPlanet == null; y++ )
                {
                    ThreatReport report = factionData.ThreatReports[y];

                    if ( report.Planet.GetHopsTo( centerpiece.Planet ) <= 1 && report.TotalStrength > report.MilitiaGuardStrength + report.FriendlyGuardStrength
                     && (report.Planet.GetControllingFaction() == playerFaction || centerpiece.Planet.Index == report.Planet.Index) )
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
                                if ( entity.Planet.Index != centerpiece.Planet.Index && entity.LongRangePlanningData.FinalDestinationIndex != centerpiece.Planet.Index )
                                {
                                    // Not yet on our target planet, and we're not yet on our centerpiece planet. Path to our centerpiece planet first.
                                    // Get a path for the ship to take, and give them the command.
                                    List<Planet> path = faction.FindPath( entity.Planet, centerpiece.Planet, Context );

                                    // Create and add all required parts of a wormhole move command.
                                    GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_NPCSingleUnit], GameCommandSource.AnythingElse );
                                    command.RelatedEntityIDs.Add( entity.PrimaryKeyID );
                                    for ( int p = 0; p < path.Count; p++ )
                                        command.RelatedIntegers.Add( path[p].Index );
                                    Context.QueueCommandForSendingAtEndOfContext( command );
                                }
                                else
                                {
                                    // Not yet on our target planet, and we're on our centerpice planet. Path to our target planet.
                                    // Get a path for the ship to take, and give them the command.
                                    List<Planet> path = faction.FindPath( entity.Planet, targetPlanet, Context );

                                    // Create and add all required parts of a wormhole move command.
                                    GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_NPCSingleUnit], GameCommandSource.AnythingElse );
                                    command.RelatedEntityIDs.Add( entity.PrimaryKeyID );
                                    for ( int p = 0; p < path.Count; p++ )
                                        command.RelatedIntegers.Add( path[p].Index );
                                    Context.QueueCommandForSendingAtEndOfContext( command );
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
                        if ( adjPlanet.GetControllingOrInfluencingFaction().GetIsHostileTowards( playerFaction ) && threat.Total > 1000 )
                        {
                            int strength = 0;
                            for ( int y = 0; y < militiaData.Ships.Count; y++ )
                            {
                                for ( int z = 0; z < militiaData.Ships[y].Count; z++ )
                                {
                                    GameEntity_Squad entity = World_AIW2.Instance.GetEntityByID_Squad( militiaData.Ships[y][z] );
                                    if ( entity == null )
                                        continue;

                                    if ( entity.TypeData.IsMobileCombatant && (entity.TypeData.GetHasTag( "CivMobile" ) || entity.TypeData.GetHasTag("CivProtector")) )
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
                         if ( adjPlanet.GetControllingFaction().GetIsFriendlyTowards( playerFaction ) )
                             return DelReturn.Continue;

                         var threat = factionData.GetThreat( adjPlanet );

                         // See if they still have any active guard posts.
                         int reinforcePoints = 0;
                         adjPlanet.DoForEntities( EntityRollupType.ReinforcementLocations, delegate ( GameEntity_Squad reinforcementPoint )
                         {
                             if (reinforcementPoint.TypeData.SpecialType == SpecialEntityType.GuardPost)
                                reinforcePoints++;
                             return DelReturn.Continue;
                         } );

                         if ( adjPlanet.Name == "Typhoon" )
                             ArcenDebugging.SingleLineQuickDebug( "Typhoon Info: Hostiles: " + threat.Total + " Reinforcements: " + reinforcePoints );

                         // If we don't yet have an assessment for the planet, and it has enough threat, add it.
                         // Factor out planets that have already been covered by player units.
                         if ( reinforcePoints > 0 || threat.Total > Math.Max( 1000, (threat.FriendlyMobile + threat.FriendlyGuard) / 3 ) )
                         {
                             AttackAssessment adjAssessment = (from o in attackAssessments where o.Target.Index == adjPlanet.Index select o).FirstOrDefault();
                             if ( adjAssessment == null )
                             {
                                 adjAssessment = new AttackAssessment( adjPlanet, (int)(threat.Total * 1.25), reinforcePoints > 0 ? true : false );
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
                    if ( effStr < threat.FriendlyGuard + threat.FriendlyMobile + assessment.AttackPower)
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
                    GameCommand wormholeCommand = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.MoveManyToOnePoint], GameCommandSource.AnythingElse );
                    wormholeCommand.RelatedPoints.Add( wormhole.WorldLocation );
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
                                    // Get a path for the ship to take, and give them the command.
                                    List<Planet> path = faction.FindPath( entity.Planet, centerpiece.Planet, Context );

                                    // Create and add all required parts of a wormhole move command.
                                    GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_NPCSingleUnit], GameCommandSource.AnythingElse );
                                    command.RelatedEntityIDs.Add( entity.PrimaryKeyID );
                                    for ( int p = 0; p < path.Count; p++ )
                                    {
                                        Planet nextPlanet = path[p];
                                        if ( nextPlanet != null )
                                            command.RelatedIntegers.Add( nextPlanet.Index );
                                    }
                                    Context.QueueCommandForSendingAtEndOfContext( command );
                                }
                            }
                            else if ( wormhole != null && wormhole.WorldLocation.GetExtremelyRoughDistanceTo( entity.WorldLocation ) > 5000
                                && (entity.Orders.QueuedOrders.Count == 0 || entity.Orders.QueuedOrders[0].RelatedPoint != wormhole.WorldLocation) )
                            {
                                notReady++;
                                // Create and add all required parts of a move to point command.
                                if ( wormhole != null )
                                {
                                    wormholeCommand.RelatedEntityIDs.Add( entity.PrimaryKeyID );
                                }
                            }
                            else
                                ready++;

                        }
                    }
                    if ( wormholeCommand.RelatedEntityIDs.Count > 0 && !alreadyAttacking )
                        Context.QueueCommandForSendingAtEndOfContext( wormholeCommand );
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
                        if ( assessment.AttackPower > 5000 )
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

                                if ( entity.Planet.Index == assessment.Target.Index && entity.LongRangePlanningData.FinalDestinationIndex != -1 &&
                                entity.LongRangePlanningData.FinalDestinationIndex != assessment.Target.Index )
                                {
                                    // We're on our target planet, but for some reason we're trying to leave it. Stop.
                                    entity.Orders.ClearOrders( ClearBehavior.DoNotClearBehaviors, ClearDecollisionOnParent.DoNotClearDecollision, ClearSource.YesClearAnyOrders_IncludingFromHumans );
                                }

                                if ( entity.Planet.Index != assessment.Target.Index && entity.LongRangePlanningData.FinalDestinationIndex != assessment.Target.Index )
                                {
                                    // Get a path for the ship to take, and give them the command.
                                    List<Planet> path = faction.FindPath( entity.Planet, assessment.Target, Context );

                                    // Create and add all required parts of a wormhole move command.
                                    GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_NPCSingleUnit], GameCommandSource.AnythingElse );
                                    command.RelatedEntityIDs.Add( entity.PrimaryKeyID );
                                    for ( int p = 0; p < path.Count; p++ )
                                        command.RelatedIntegers.Add( path[p].Index );
                                    Context.QueueCommandForSendingAtEndOfContext( command );
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
                            if ( entity.Planet.Index != centerpiece.Planet.Index && entity.LongRangePlanningData.FinalDestinationIndex != centerpiece.Planet.Index &&
                                (threat.Total <= 0 || threat.MilitiaMobile < threat.Total * 1.25) )
                            {
                                // Get a path for the ship to take, and give them the command.
                                List<Planet> path = faction.FindPath( entity.Planet, centerpiece.Planet, Context );

                                // Create and add all required parts of a wormhole move command.
                                GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_NPCSingleUnit], GameCommandSource.AnythingElse );
                                command.RelatedEntityIDs.Add( entity.PrimaryKeyID );
                                for ( int p = 0; p < path.Count; p++ )
                                    command.RelatedIntegers.Add( path[p].Index );
                                Context.QueueCommandForSendingAtEndOfContext( command );
                            }
                        }
                    }
                }
            }
            #endregion
            Engine_Universal.NewTimingsBeingBuilt.FinishRememberingFrame( FramePartTimings.TimingType.ShortTermBackgroundThreadEntry, "DoMilitiaThreatReaction" );
        }

        // Update ship and turret caps for militia buildings.
        public void UpdateUnitCaps( Faction faction, Faction playerFaction, CivilianFaction factionData, ArcenLongTermIntermittentPlanningContext Context )
        {
            // Count the number of militia barracks for each planet.
            Dictionary<Planet, int> barracksPerPlanet = new Dictionary<Planet, int>();
            playerFaction.DoForControlledPlanets( delegate ( Planet planet )
            {
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
                return DelReturn.Continue;
            } );
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
                                for ( int z = 0; z < barracksPerPlanet[otherPlanet]; z++ )
                                    if ( turretData.MultiplierToAllFleetCaps == 0 )
                                        militiaStatus.ShipCapacity[y] += Math.Max( 1, (FInt.Create( militiaStatus.ShipCapacity[y], true ) / 3).GetNearestIntPreferringHigher() );
                                    else
                                        militiaStatus.ShipCapacity[y] += Math.Max( (1 / turretData.MultiplierToAllFleetCaps).GetNearestIntPreferringHigher(), (FInt.Create( militiaStatus.ShipCapacity[y], true ) / 3).GetNearestIntPreferringHigher() );
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

        // Do NOT directly change anything from this function. Doing so may cause desyncs in multiplayer.
        // What you can do from here is queue up game commands for units, and send them to be done via QueueCommandForSendingAtEndOfContext.
        public override void DoLongRangePlanning_OnBackgroundNonSimThread_Subclass( Faction faction, ArcenLongTermIntermittentPlanningContext Context )
        {
            // Load our data.
            // Start with world.
            CivilianWorld worldData = World.Instance.GetCivilianWorldExt();
            if ( worldData == null )
                return;

            // Next, do logic once for each faction that has a registered industry.
            for ( int x = 0; x < worldData.Factions.Count; x++ )
            {
                // Load in this faction's data.
                Faction playerFaction = World_AIW2.Instance.GetFactionByIndex( worldData.Factions[x] );
                if ( playerFaction == null )
                    continue;
                CivilianFaction factionData = playerFaction.GetCivilianFactionExt();
                if ( factionData == null )
                    continue;

                DoTradeRequests( faction, playerFaction, factionData, Context );
                DoCargoShipMovement( faction, playerFaction, factionData, Context );
                DoMilitiaConstructionShipMovement( faction, playerFaction, factionData, Context );
                DoMilitiaThreatReaction( faction, playerFaction, factionData, Context );
                UpdateUnitCaps( faction, playerFaction, factionData, Context );
            }
        }

        // Check for our stuff dying.
        public override void DoOnAnyDeathLogic( GameEntity_Squad entity, EntitySystem FiringSystemOrNull, ArcenSimContext Context )
        {
            // Skip if the ship was not defined by our mod.
            // Things like spawnt patrol ships and turrets don't need to be processed for death.
            if ( !entity.TypeData.GetHasTag( "CivilianIndustryEntity" ) )
                return;

            // Load the faction data of the dead entity's faction.
            // Look through our world data, first, to find which faction controls our starting station, and load its faction data.
            CivilianWorld worldData = World.Instance.GetCivilianWorldExt();
            Faction faction = null;
            CivilianFaction factionData = null;
            // Look through our saved factions to find which our entity belongs to
            for ( int x = 0; x < worldData.Factions.Count; x++ )
            {
                CivilianFaction tempData = worldData.getFactionInfo( x ).factionData;
                if ( tempData.GrandStation == entity.PrimaryKeyID
                || tempData.CargoShips.Contains( entity.PrimaryKeyID )
                || tempData.MilitiaLeaders.Contains( entity.PrimaryKeyID )
                || tempData.TradeStations.Contains( entity.PrimaryKeyID ) )
                {
                    factionData = tempData;
                    faction = World_AIW2.Instance.GetFactionByIndex( worldData.Factions[x] );
                    break;
                }
            }

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
            faction.SetCivilianFactionExt( factionData );
        }
    }
}