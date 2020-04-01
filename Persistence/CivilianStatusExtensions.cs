using Arcen.AIW2.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKCivilianIndustry.Persistence
{
    public static class CivilianStatusExtensions
    {
        // This loads the data assigned to whatever ParentObject you pass. So, say, you could assign the same class to different ships, and each would be able to get back the values assigned to it.
        // In our specific case here, we're going to be assigning a dictionary to every faction.
        public static CivilianStatus GetCivilianStatusExt(this GameEntity_Squad ParentObject)
        {
            return (CivilianStatus)ParentObject.ExternalData.GetCollectionByPatternIndex((int)CivilianStatusExternalData.PatternIndex).Data[0];
        }
        // This meanwhile saves the data, assigning it to whatever ParentObject you pass.
        public static void SetCivilianStatusExt(this GameEntity_Squad ParentObject, CivilianStatus data)
        {
            ParentObject.ExternalData.GetCollectionByPatternIndex((int)CivilianStatusExternalData.PatternIndex).Data[0] = data;
        }
    }
}
