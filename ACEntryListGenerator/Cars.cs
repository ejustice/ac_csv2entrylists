namespace ACEntryListGenerator
{
    internal static class Cars
    {
        public const string BMW_E30_GRA = "bmw_m3_e30_gra";
        public const string MAZDA_MX5_CUP = "ks_mazda_mx5_cup";
        public const string PEUGEOT_206_CUP = "peugeot_206_rps_206_gti_cup";
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
                default:
                    return key;
            }
        }
    }
}
