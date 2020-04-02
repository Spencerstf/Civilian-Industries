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
        public Planet Planet { get; }
        public int MilitiaGuardStrength { get; }
        public int MilitiaMobileStrength { get; }
        public int FriendlyGuardStrength { get; }
        public int FriendlyMobileStrength { get; }
        public int CloakedHostileStrength { get; }
        public int NonCloakedHostileStrength { get; }
        public int WaveStrength { get; }
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
            StringBuilder b = new StringBuilder();
            b.Append($"Planet: {Planet} ");
            b.Append($"MilitiaGuard: {MilitiaGuardStrength} ");
            b.Append($"MilitiaMobile: {MilitiaMobileStrength} ");
            b.Append($"FriendlyGuard: {FriendlyGuardStrength} ");
            b.Append($"FriendlyMobile: {FriendlyMobileStrength} ");
            b.Append($"CloakedHostile: {CloakedHostileStrength} ");
            b.Append($"NonCloakedHostile: {NonCloakedHostileStrength} ");
            b.Append($"Wave: {WaveStrength} ");
            b.Append($"Total: {TotalStrength}.");
            return b.ToString();
        }
    }
}
