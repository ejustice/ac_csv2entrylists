using System.Collections.Generic;

namespace ACEntryListGenerator
{
    internal static class Cars
    {
        public const string BMW_E30_GRA = "bmw_m3_e30_gra";
        public const string MAZDA_MX5_CUP = "ks_mazda_mx5_cup";
        public const string MAZDA_MAX5_CUP = "ks_mazda_max5_racing";
        public const string PEUGEOT_206_CUP = "peugeot_206_rps_206_gti_cup";

        public static readonly IReadOnlyList<string> AllCars = new[] { BMW_E30_GRA, MAZDA_MX5_CUP, MAZDA_MAX5_CUP, PEUGEOT_206_CUP };

        public static string MapCarKey(string key)
        {
            switch (key)
            {
                case "BMW E30 grA":
                    return BMW_E30_GRA;
                case "Peugeot 206 Gti":
                    return PEUGEOT_206_CUP;
                case "Mazda MX5 CUP":
                    return MAZDA_MX5_CUP;
                case "Mazda MAX5 CUP":
                    return MAZDA_MAX5_CUP;
                default:
                    return key;
            }
        }
    }
}
