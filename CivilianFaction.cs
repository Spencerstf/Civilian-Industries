using Arcen.AIW2.Core;
using Arcen.AIW2.External;
using Arcen.Universal;
using SKCivilianIndustry.Persistence;
using System;
using System.Collections.Generic;

namespace SKCivilianIndustry
{

    /// <summary>
    /// Invidual storage class for each faction.
    /// </summary>
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

        /// <summary>
        /// Last reported number of failed trade routes due to a lack of cargo ships.
        /// </summary>
        public (int Import, int Export) FailedCounter;

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

        // Returns the base resource cost for ships.
        public int GetResourceCost( Faction faction )
        {
            // 51 - (Intensity ^ 1.5)
            return 51 - (int)Math.Pow( faction.Ex_MinorFactionCommon_GetPrimitives().Intensity, 1.5 );
        }

        /// <summary>
        /// Returns the ship/turret capacity. Base 20, increases based on intensity and trade station count.
        /// </summary>
        /// <returns></returns>
        public int GetCap( Faction faction )
        {
            int cap = 20;
            int intensity = faction.Ex_MinorFactionCommon_GetPrimitives().Intensity;
            double intensityMult = 0.5 * intensity;
            double stationMult = 0.05 * TradeStations.Count;
            double totalMult = intensityMult + stationMult;
            cap = (int)(Math.Ceiling( cap * totalMult ));
            return cap;
        }

        // Should never be used by itself, removes the cargo ship from all applicable statuses, but keeps it in the main cargo ship list.
        private void RemoveCargoShipStatus( int cargoShipID )
        {
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

        /// <summary>
        /// Remove a cargo ship from amy list it is currently in, effectively deleting it from the faction, but NOT from the world.
        /// The entity itself must be killed or despawned before or after this.
        /// </summary>
        /// <param name="cargoShipID">The PrimaryKeyID of the ship to remove.</param>
        public void RemoveCargoShip( int cargoShipID )
        {
            if ( this.CargoShips.Contains( cargoShipID ) )
                this.CargoShips.Remove( cargoShipID );
            RemoveCargoShipStatus( cargoShipID );
        }

        /// <summary>
        /// Remove a cargo ship from whatever it is currently doing, and change its action to the requested action.
        /// </summary>
        /// <param name="cargoShipID">The PrimaryKeyID of the ship to modify.</param>
        /// <param name="status">The status to change to. Idle, Loading, Unloading, Building, Pathing, or Enroute</param>
        public void ChangeCargoShipStatus(GameEntity_Squad cargoShip, string status )
        {
            int cargoShipID = cargoShip.PrimaryKeyID;
            if ( !this.CargoShips.Contains( cargoShipID ) )
                return;
            RemoveCargoShipStatus( cargoShipID );
            switch ( status )
            {
                case "Loading":
                    this.CargoShipsLoading.Add( cargoShipID );
                    break;
                case "Unloading":
                    this.CargoShipsUnloading.Add( cargoShipID );
                    break;
                case "Building":
                    this.CargoShipsBuilding.Add( cargoShipID );
                    break;
                case "Pathing":
                    this.CargoShipsPathing.Add( cargoShipID );
                    break;
                case "Enroute":
                    this.CargoShipsEnroute.Add( cargoShipID );
                    break;
                default:
                    this.CargoShipsIdle.Add( cargoShipID );
                    break;
            }
        }

        /// <summary>
        /// Returns true if we should consider the planet friendly.
        /// </summary>
        /// <param name="faction">The Civilian Industry faction to check.</param>
        /// <param name="planet">The Planet to check.</param>
        /// <returns></returns>
        public bool IsPlanetFriendly( Faction faction, Planet planet )
        {
            if ( planet.GetControllingOrInfluencingFaction().GetIsFriendlyTowards( faction ) )
                return true; // If planet is owned by a friendly faction, its friendly.

            for ( int x = 0; x < TradeStations.Count; x++ )
            {
                GameEntity_Squad tradeStation = World_AIW2.Instance.GetEntityByID_Squad( TradeStations[x] );
                if ( tradeStation == null )
                    continue;
                if ( tradeStation.Planet.Index == planet.Index )
                    return true; // Planet has a trade station on it, its friendly.
            }

            for ( int x = 0; x < MilitiaLeaders.Count; x++ )
            {
                GameEntity_Squad militia = World_AIW2.Instance.GetEntityByID_Squad( MilitiaLeaders[x] );
                if ( militia == null )
                    continue;
                if ( militia.Planet.Index == planet.Index )
                    return true; // Planet has a militia leader on it, its friendly.

                CivilianMilitia militiaData = militia.GetCivilianMilitiaExt();
                if ( militiaData.Centerpiece == -1 )
                    continue;
                GameEntity_Squad centerpiece = World_AIW2.Instance.GetEntityByID_Squad( militiaData.Centerpiece );
                if ( centerpiece == null )
                    continue;
                if ( centerpiece.Planet.Index == planet.Index )
                    return true; // Planet has a militia leader's centerpiece on it, its friendly.
            }

            // Nothing passed. Its hostile.
            return false;
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
            Buffer.AddItem( 2 );
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
            Buffer.AddItem( this.FailedCounter.Import );
            Buffer.AddItem( this.FailedCounter.Export );
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
            if ( this.Version >= 2 )
                this.FailedCounter = (Buffer.ReadInt32(), Buffer.ReadInt32());
            else
                this.FailedCounter = (0, 0);
            this.MilitiaCounter = Buffer.ReadInt32();
            this.NextRaidInThisSeconds = Buffer.ReadInt32();
            this.NextRaidWormholes = DeserializeList( Buffer );

            // Recreate an empty list on load. Will be populated when needed.
            this.ThreatReports = new List<ThreatReport>();
            this.ImportRequests = new List<TradeRequest>();
            this.ExportRequests = new List<TradeRequest>();
        }
    }
}
