using Arcen.AIW2.Core;
using Arcen.AIW2.External;
using Arcen.Universal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public int GetTradeStationRebuildTimer(Planet planet)
        {
            return GetTradeStationRebuildTimer(planet.Index);
        }
        public int GetTradeStationRebuildTimer(int planet)
        {
            if (TradeStationRebuildTimerInSecondsByPlanet.ContainsKey(planet))
                return TradeStationRebuildTimerInSecondsByPlanet[planet];
            else
                return 0;
        }
        public void SetTradeStationRebuildTimer(Planet planet, int timer)
        {
            SetTradeStationRebuildTimer(planet.Index, timer);
        }
        public void SetTradeStationRebuildTimer(int planet, int timer)
        {
            if (TradeStationRebuildTimerInSecondsByPlanet.ContainsKey(planet))
                TradeStationRebuildTimerInSecondsByPlanet[planet] = timer;
            else
                TradeStationRebuildTimerInSecondsByPlanet.Add(planet, timer);
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
        public (int MilitiaGuard, int MilitiaMobile, int FriendlyGuard, int FriendlyMobile, int CloakedHostile, int NonCloakedHostile, int Wave, int Total) GetThreat(Planet planet)
        {
            try
            {
                // If reports aren't generated, return 0.
                if (ThreatReports == null)
                    return (0, 0, 0, 0, 0, 0, 0, 0);
                else
                    for (int x = 0; x < ThreatReports.Count; x++)
                        if (ThreatReports[x].Planet.Index == planet.Index)
                            return ThreatReports[x].GetThreat();
                // Planet not processed. Return 0.
                return (0, 0, 0, 0, 0, 0, 0, 0);
            }
            catch (Exception e)
            {
                // Failed to return a report, return 0. Harmless, so we don't worry about informing the player.
                ArcenDebugging.SingleLineQuickDebug(e.Message);
                return (0, 0, 0, 0, 0, 0, 0, 0);
            }
        }
        // Calculate threat values every planet that our mobile forces are on or adjacent to.
        public void CalculateThreat(Faction faction, Faction playerFaction)
        {
            // Empty our dictionary.
            ThreatReports = new List<ThreatReport>();

            // Get the grand station's planet, to easily figure out when we're processing the home planet.
            GameEntity_Squad grandStation = World_AIW2.Instance.GetEntityByID_Squad(GrandStation);
            if (grandStation == null)
                return;
            Planet grandPlanet = grandStation.Planet;
            if (grandPlanet == null)
                return;

            List<int> processed = new List<int>();
            faction.Entities.DoForEntities(delegate (GameEntity_Squad squad)
            {
                squad.Planet.DoForLinkedNeighborsAndSelf(delegate (Planet planet)
                {
                    // Stop if its already processed.
                    if (processed.Contains(planet.Index))
                        return DelReturn.Continue;

                    // Prepare variables to hold our soon to be detected threat values.
                    int friendlyMobileStrength = 0, friendlyGuardStrength = 0, cloakedHostileStrength = 0, nonCloakedHostileStrength = 0, militiaMobileStrength = 0, militiaGuardStrength = 0, waveStrength = 0;
                    // Wave detection.
                    for (int j = 0; j < World_AIW2.Instance.AIFactions.Count; j++)
                    {
                        Faction aiFaction = World_AIW2.Instance.AIFactions[j];
                        List<PlannedWave> QueuedWaves = aiFaction.GetWaveList();
                        for (int k = 0; k < QueuedWaves.Count; k++)
                        {
                            PlannedWave wave = QueuedWaves[k];

                            if (wave.targetPlanetIdx != planet.Index)
                                continue;

                            if (wave.gameTimeInSecondsForLaunchWave - World_AIW2.Instance.GameSecond <= 90)
                                nonCloakedHostileStrength += wave.CalculateStrengthOfWave(aiFaction) * 3;

                            else if (wave.playerBeingAlerted)
                                waveStrength += wave.CalculateStrengthOfWave(aiFaction);
                        }
                    }

                    // Get hostile strength.
                    LongRangePlanningData_PlanetFaction linkedPlanetFactionData = planet.LongRangePlanningData.PlanetFactionDataByIndex[faction.FactionIndex];
                    LongRangePlanning_StrengthData_PlanetFaction_Stance hostileStrengthData = linkedPlanetFactionData.DataByStance[FactionStance.Hostile];
                    // If on friendly planet, triple the threat.
                    if (planet.GetControllingFaction() == playerFaction)
                        nonCloakedHostileStrength += hostileStrengthData.TotalStrength * 3;
                    else // If on hostile planet, don't factor in stealth.
                    {
                        nonCloakedHostileStrength += hostileStrengthData.TotalStrength - hostileStrengthData.CloakedStrength;
                        cloakedHostileStrength += hostileStrengthData.CloakedStrength;
                    }

                    // Adjacent planet threat matters as well, but not as much as direct threat.
                    // We'll only add it if the planet has no friendly forces on it.
                    if (planet.GetControllingFaction() == playerFaction)
                        planet.DoForLinkedNeighbors(delegate (Planet linkedPlanet)
                        {
                            linkedPlanetFactionData = linkedPlanet.LongRangePlanningData.PlanetFactionDataByIndex[faction.FactionIndex];
                            LongRangePlanning_StrengthData_PlanetFaction_Stance attackingStrengthData = linkedPlanetFactionData.DataByStance[FactionStance.Friendly];
                            int attackingStrength = attackingStrengthData.TotalStrength;
                            attackingStrengthData = linkedPlanetFactionData.DataByStance[FactionStance.Self];
                            attackingStrength += attackingStrengthData.TotalStrength;

                            if (attackingStrength < 1000)
                            {
                                hostileStrengthData = linkedPlanetFactionData.DataByStance[FactionStance.Hostile];
                                nonCloakedHostileStrength += hostileStrengthData.RelativeToHumanTeam_ThreatStrengthVisible;
                                nonCloakedHostileStrength += hostileStrengthData.TotalHunterStrengthVisible;
                            }

                            return DelReturn.Continue;
                        });

                    // If on home plant, double the total threat.
                    if (planet.Index == grandPlanet.Index)
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
                    ThreatReports.Add(new ThreatReport(planet, militiaGuardStrength, militiaMobileStrength, friendlyGuardStrength, friendlyMobileStrength, cloakedHostileStrength, nonCloakedHostileStrength, waveStrength));

                    // Add to the proccessed list.
                    processed.Add(planet.Index);

                    return DelReturn.Continue;
                });
                return DelReturn.Continue;
            });
            // Sort our reports.
            ThreatReports.Sort();
        }

        // Returns the base resource cost for ships.
        public int GetResourceCost(Faction faction)
        {
            // 51 - (Intensity ^ 1.5)
            return 51 - (int)Math.Pow(faction.Ex_MinorFactionCommon_GetPrimitives().Intensity, 1.5);
        }

        // Returns the current capacity for turrets/ships.
        public int GetCap(Faction faction)
        {
            // ((baseCap + (AIP / AIPDivisor)) ^ (1 + (Intensity / IntensityDivisor)))
            int cap = 0;
            int baseCap = 20;
            int AIPDivisor = 2;
            int IntensityDivisor = 25;
            for (int y = 0; y < World_AIW2.Instance.AIFactions.Count; y++)
                cap = (int)(Math.Ceiling(Math.Pow(Math.Max(cap, baseCap + World_AIW2.Instance.AIFactions[y].GetAICommonExternalData().AIProgress_Total.ToInt() / AIPDivisor),
                     1 + (faction.Ex_MinorFactionCommon_GetPrimitives().Intensity / IntensityDivisor))));
            return cap;
        }

        // Return a cargo ship from any lists its in.
        public void RemoveCargoShip(int cargoShipID)
        {
            if (this.CargoShips.Contains(cargoShipID))
                this.CargoShips.Remove(cargoShipID);
            if (this.CargoShipsIdle.Contains(cargoShipID))
                this.CargoShipsIdle.Remove(cargoShipID);
            if (this.CargoShipsLoading.Contains(cargoShipID))
                this.CargoShipsLoading.Remove(cargoShipID);
            if (this.CargoShipsUnloading.Contains(cargoShipID))
                this.CargoShipsUnloading.Remove(cargoShipID);
            if (this.CargoShipsBuilding.Contains(cargoShipID))
                this.CargoShipsBuilding.Remove(cargoShipID);
            if (this.CargoShipsPathing.Contains(cargoShipID))
                this.CargoShipsPathing.Remove(cargoShipID);
            if (this.CargoShipsEnroute.Contains(cargoShipID))
                this.CargoShipsEnroute.Remove(cargoShipID);
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
        private void SerializeList(List<int> list, ArcenSerializationBuffer Buffer)
        {
            // Lists require a special touch to save.
            // Get the number of items in the list, and store that as well.
            // This is so you know how many items you'll have to load later.
            int count = list.Count;
            Buffer.AddItem(count);
            for (int x = 0; x < count; x++)
                Buffer.AddItem(list[x]);
        }
        private void SerializeDictionary(Dictionary<int, int> dict, ArcenSerializationBuffer Buffer)
        {
            Buffer.AddItem(dict.Count);
            foreach (int key in dict.Keys)
            {
                Buffer.AddItem(key);
                Buffer.AddItem(dict[key]);
            }
        }
        // Saving our data.
        public void SerializeTo(ArcenSerializationBuffer Buffer)
        {
            Buffer.AddItem(1);
            Buffer.AddItem(this.GrandStation);
            Buffer.AddItem(this.GrandStationRebuildTimerInSeconds);
            SerializeList(TradeStations, Buffer);
            SerializeDictionary(TradeStationRebuildTimerInSecondsByPlanet, Buffer);
            SerializeList(CargoShips, Buffer);
            SerializeList(MilitiaLeaders, Buffer);
            SerializeList(CargoShipsIdle, Buffer);
            SerializeList(CargoShipsLoading, Buffer);
            SerializeList(CargoShipsUnloading, Buffer);
            SerializeList(CargoShipsBuilding, Buffer);
            SerializeList(CargoShipsPathing, Buffer);
            SerializeList(CargoShipsEnroute, Buffer);
            Buffer.AddItem(this.BuildCounter);
            Buffer.AddItem(this.MilitiaCounter);
            Buffer.AddItem(this.NextRaidInThisSeconds);
            SerializeList(this.NextRaidWormholes, Buffer);
        }
        // Deserialize a list.
        public List<int> DeserializeList(ArcenDeserializationBuffer Buffer)
        {
            // Lists require a special touch to load.
            // We'll have saved the number of items stored up above to be used here to determine the number of items to load.
            // ADDITIONALLY we'll need to recreate a blank list beforehand, as loading does not call the Initialization function.
            // Can't add values to a list that doesn't exist, after all.
            int count = Buffer.ReadInt32();
            List<int> newList = new List<int>();
            for (int x = 0; x < count; x++)
                newList.Add(Buffer.ReadInt32());
            return newList;
        }
        public Dictionary<int, int> DeserializeDictionary(ArcenDeserializationBuffer Buffer)
        {
            int count = Buffer.ReadInt32();
            Dictionary<int, int> newDict = new Dictionary<int, int>();
            for (int x = 0; x < count; x++)
            {
                int key = Buffer.ReadInt32();
                int value = Buffer.ReadInt32();
                if (!newDict.ContainsKey(key))
                    newDict.Add(key, value);
                else
                    newDict[key] = value;
            }
            return newDict;
        }
        // Loading our data. Make sure the loading order is the same as the saving order.
        public CivilianFaction(ArcenDeserializationBuffer Buffer)
        {
            this.Version = Buffer.ReadInt32();
            this.GrandStation = Buffer.ReadInt32();
            this.GrandStationRebuildTimerInSeconds = Buffer.ReadInt32();
            this.TradeStations = DeserializeList(Buffer);
            this.TradeStationRebuildTimerInSecondsByPlanet = DeserializeDictionary(Buffer);
            this.CargoShips = DeserializeList(Buffer);
            this.MilitiaLeaders = DeserializeList(Buffer);
            this.CargoShipsIdle = DeserializeList(Buffer);
            this.CargoShipsLoading = DeserializeList(Buffer);
            this.CargoShipsUnloading = DeserializeList(Buffer);
            this.CargoShipsBuilding = DeserializeList(Buffer);
            this.CargoShipsPathing = DeserializeList(Buffer);
            this.CargoShipsEnroute = DeserializeList(Buffer);
            this.BuildCounter = Buffer.ReadInt32();
            this.MilitiaCounter = Buffer.ReadInt32();
            this.NextRaidInThisSeconds = Buffer.ReadInt32();
            this.NextRaidWormholes = DeserializeList(Buffer);

            // Recreate an empty list on load. Will be populated when needed.
            this.ThreatReports = new List<ThreatReport>();
            this.ImportRequests = new List<TradeRequest>();
            this.ExportRequests = new List<TradeRequest>();
        }
    }
}
