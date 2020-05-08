using System;

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
    }
}
