namespace ACEntryListGenerator.Models
{
    public class SkinUi
    {
        public string Skinname { get; set; }
        public string Drivername { get; set; }
        public string Country { get; set; }
        public string Team { get; set; }
        public string Number { get; set; }
        public string Priority { get; set; }
        public string Directory { get; set; }
    }

    public static class SkinMode
    {
        public static string None => "";
        public static string DriverName => "DriverName";
        public static string FromCache => "FromCache";
        public static string SkinName => "SkinName";
        public static string Random => "Random";




    }
}
