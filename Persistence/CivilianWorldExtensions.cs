using Arcen.Universal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKCivilianIndustry.Persistence
{
    /// <summary>
    /// The following is a helper function , designed to allow us to save and load data on demand.
    /// </summary>
    public static class CivilianWorldExtensions
    {
        // This loads the data assigned to whatever ParentObject you pass. So, say, you could assign the same class to different ships, and each would be able to get back the values assigned to it.
        // In our specific case here, we're going to be assigning a dictionary to every faction.
        public static CivilianWorld GetCivilianWorldExt(this World ParentObject)
        {
            return (CivilianWorld)ParentObject.ExternalData.GetCollectionByPatternIndex((int)CivilianWorldExternalData.PatternIndex).Data[0];
        }
        /// <summary>
        /// This meanwhile saves the data, assigning it to whatever ParentObject you pass.
        /// </summary>
        public static void SetCivilianWorldExt(this World ParentObject, CivilianWorld data)
        {
            ParentObject.ExternalData.GetCollectionByPatternIndex((int)CivilianWorldExternalData.PatternIndex).Data[0] = data;
        }
    }
}
