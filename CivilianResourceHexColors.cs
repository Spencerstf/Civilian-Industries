using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKCivilianIndustry
{
    public static class CivilianResourceHexColors
    {
        public static string[] Color;

        static CivilianResourceHexColors()
        {
            Color = new string[(int)CivilianResource.Length];

            Color[(int)CivilianResource.Ambuinum] = "9a9a9a";
            Color[(int)CivilianResource.Steel] = "43464B";
            Color[(int)CivilianResource.Disrupeon] = "e0ca8b";
            Color[(int)CivilianResource.Protium] = "72eb6e";
            Color[(int)CivilianResource.Tritium] = "e8e28b";
            Color[(int)CivilianResource.Tungsten] = "52689c";
            Color[(int)CivilianResource.Radium] = "8f1579";
            Color[(int)CivilianResource.Splackon] = "a83e3e";
            Color[(int)CivilianResource.Silicon] = "10adb3";
            Color[(int)CivilianResource.Techrackum] = "c2ffc6";

        }
    }
}
