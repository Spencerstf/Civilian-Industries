using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKCivilianIndustry
{
    /// <summary>
    /// Enum used to keep track of what our cargo and trade ships are doing.
    ///  Bare basic, used mostly for performance sake, so only ships that need to be processed for something are even considered valid targets.
    /// </summary>
    public enum CivilianShipStatus
    {
        /// <summary>
        /// Doing nothing
        /// </summary>
        Idle,
        /// <summary>
        /// Loading resources into ship.
        /// </summary>
        Loading,
        /// <summary>
        /// Offloading resources onto a station.
        /// </summary>
        Unloading,
        /// <summary>
        /// Offloading resources onto a militia building.
        /// </summary>
        Building,
        /// <summary>
        /// Pathing towards a requesting station.
        /// </summary>
        Pathing,
        /// <summary>
        /// Taking resource to another trade station.
        /// </summary>
        Enroute
    }
}
