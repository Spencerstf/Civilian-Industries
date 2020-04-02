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
        /// <summary>
        /// The resource to be requested. If length, means any.
        /// </summary>
        public CivilianResource Requested;

        // TODO : It's hard to understand what 'if above is length' means.
        /// <summary>
        /// Resources to be declined, if above is length.
        /// </summary>
        public List<CivilianResource> Declined;

        /// <summary>
        /// Urgency of the request.
        /// </summary>
        public int Urgency { get; }

        /// <summary>
        /// The station with this request.
        /// </summary>
        public GameEntity_Squad Station { get; }

        /// <summary>
        /// Maximum number of hops to look for trade in.
        /// </summary>
        public int MaxSearchHops { get; }

        /// <summary>
        /// Finished being processed.
        /// </summary>
        public bool Processed { get; set; }

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
            string output = $"Requested: {Requested.ToString()} Urgency: {Urgency} Planet: {Station.Planet.Name} Processed: {Processed}";
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
