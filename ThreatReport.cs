using Arcen.AIW2.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKCivilianIndustry
{
    /// <summary>
    /// Used to report on the amount of threat on each planet.
    /// </summary>
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
        public int TotalStrength => CloakedHostileStrength + NonCloakedHostileStrength;
        public (int MilitiaGuard, int MilitiaMobile, int FriendlyGuard, int FriendlyMobile, int CloakedHostileStrength, int NonCloakedHostileStrength, int WaveStrength, int TotalStrength) GetThreat()
        {
            return (MilitiaGuardStrength, MilitiaMobileStrength, FriendlyGuardStrength, FriendlyMobileStrength, CloakedHostileStrength, NonCloakedHostileStrength, WaveStrength, TotalStrength);
        }
        public ThreatReport(Planet planet, int militiaGuardStrength, int militiaMobileStrength, int friendlyGuardStrength, int friendlyMobileStrength, int cloakedHostileStrength, int nonCloakedHostileStrength, int waveStrength)
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

        public int CompareTo(ThreatReport other)
        {
            // We want higher threat to be first in a list, so reverse the normal sorting order.
            return other.TotalStrength.CompareTo(this.TotalStrength);
        }

        public override string ToString()
        {
            return "Planet: " + Planet + " MilitiaGuard: " + MilitiaGuardStrength + " MilitiaMobile: " + MilitiaMobileStrength + " FriendlyGuard: " + FriendlyGuardStrength +
                " FriendlyMobile: " + FriendlyMobileStrength + " CloakedHostile: " + CloakedHostileStrength + " NonCloakedHostile: " + NonCloakedHostileStrength +
                " Wave: " + WaveStrength + " Total: " + TotalStrength;
        }
    }
}
