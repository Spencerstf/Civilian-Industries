using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKCivilianIndustry
{
    // Used for militia ships for most of the same reason as the above.
    // Slightly more potential actions however.
    public enum CivilianMilitiaStatus
    {
        /// <summary>
        /// Doing nothing
        /// </summary>
        Idle,
        /// <summary>
        /// Pathing towards a trade station.
        /// </summary>
        PathingForWormhole,  
        /// <summary>
        /// Pathing towards a trade station.
        /// </summary>
        PathingForMine,
        /// <summary>
        /// Moving into position next to a wormhole to deploy.
        /// </summary>
        EnrouteWormhole,  
        /// <summary>
        /// Moving into position next to a mine to deploy.
        /// </summary>
        EnrouteMine,
        /// <summary>
        ///  In station form, requesting resources and building static defenses.
        /// </summary>
        Defending,       
        /// <summary>
        /// A more mobile form of defense, requests resources to build mobile strike fleets.
        /// </summary>
        Patrolling,        
        /// <summary>
        /// Pathing towards a trade station.
        /// </summary>
        PathingForShipyard
    }
}
