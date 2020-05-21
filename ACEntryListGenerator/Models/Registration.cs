using System;
using System.Collections.Generic;

namespace ACEntryListGenerator.Models
{
    public class Registration
    {
        public DateTime Added { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public string SteamId64 { get; set; }
        public string Car { get; set; }
        public string Skin { get; set; }
        public string ExtraInfo { get; set; }
        public bool SkinFound { get; internal set; }
        public string Team { get; internal set; }
        public string SkinFoundMode { get; internal set; }
        public object PositionOnGrid { get; internal set; }
    }

    public class CachedDriver
    {

        public CachedDriver()
        {
        }

        public CachedDriver(Registration reg)
        {
            Added = DateTime.Now;
            Updated = DateTime.Now;
            Email = reg.Email;
            FullName = reg.FullName;
            SteamId64 = reg.SteamId64;
            ExtraInfo = reg.ExtraInfo;
            CarSkins.Add(reg.Car, reg.Skin);
        }

        public DateTime Added { get; set; }
        public DateTime Updated { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public string SteamId64 { get; set; }
        public string ExtraInfo { get; set; }
        public Dictionary<string, string> CarSkins { get; set; } = new Dictionary<string, string>();
    }
}
