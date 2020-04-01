using Arcen.AIW2.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKCivilianIndustry
{
    /// <summary>
    /// Used for the creation and sorting of trade requests.
    /// </summary>
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

        public TradeRequest(CivilianResource request, List<CivilianResource> declined, int urgency, GameEntity_Squad station, int maxSearchHops)
        {
            Requested = request;
            Declined = declined;
            Urgency = urgency;
            Station = station;
            Processed = false;
            MaxSearchHops = maxSearchHops;
        }
        public TradeRequest(CivilianResource request, int urgency, GameEntity_Squad station, int maxSearchHops)
        {
            Requested = request;
            Declined = new List<CivilianResource>();
            Urgency = urgency;
            Station = station;
            Processed = false;
            MaxSearchHops = maxSearchHops;
        }

        public int CompareTo(TradeRequest other)
        {
            // We want higher urgencies to be first in a list, so reverse the normal sorting order.
            return other.Urgency.CompareTo(this.Urgency);
        }

        public override string ToString()
        {
            string output = "Requested: " + Requested.ToString() + " Urgency: " + Urgency + " Planet: " + Station.Planet.Name + " Processed: " + Processed;
            if (Declined.Count > 0)
            {
                output += " Declined: ";
                for (int x = 0; x < Declined.Count; x++)
                    output += Declined[x].ToString() + " ";
            }
            return $"Requested: {Requested} Urgency:{Urgency} Planet:{Station.Planet.Name} Processed:{Processed}";
        }
    }

}
