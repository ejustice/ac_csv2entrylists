using ExcelDataReader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACEntryListGenerator
{
    class Program
    {
        static string CsvPath; // = @"C:\tmp\DNRT-regs\DNRT - KRPE Race 10 Mei Zolder.csv";
        static string AssettoCorsaPath; // = @"C:\Program Files (x86)\Steam\steamapps\common\assettocorsa\";
        static string CarsPath = @"content\cars\";

        public static IConfigurationRoot configuration;


        // Key = "carFolder|skinFolder"
        static Dictionary<string, SkinUi> Skins = new Dictionary<string, SkinUi>();
        static async Task Main(string[] args)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                // Create service collection
                LogInfo("Building service provider");
                ServiceCollection serviceCollection = new ServiceCollection();
                ConfigureServices(serviceCollection);
                var serviceProvider = serviceCollection.BuildServiceProvider();
                //            var logger = serviceProvider.GetService<ILogger<Program>>();

                CsvPath = configuration["CsvPath"];
                LogInfo($"getting csv at {CsvPath}");
                if (!File.Exists(CsvPath))
                {
                    LogError($"Fatal: File not found: {CsvPath}");
                    Console.ReadKey();
                    return;
                }

                AssettoCorsaPath = configuration["AssettoCorsaPath"];
                LogInfo($"AssettoCorsaPath at {AssettoCorsaPath}");

                if (!Directory.Exists(AssettoCorsaPath))
                {
                    LogError($"Fatal: game directory not found: {AssettoCorsaPath}");
                    Console.ReadKey();
                    return;
                }

                // laod CSV
                var registrations = ReadCVS();


                var carGroups = registrations.GroupBy(r => r.Car);

                foreach (var carGroup in carGroups)
                {
                    await GenerateEntryList(carGroup);
                }


                // scan cars skins


                // generate entry_list
                /*
                [CAR_2]
                MODEL=bmw_m3_e30_gra
                SKIN=1991_BTCC_77_Baird
                BALLAST=0
                RESTRICTOR=0
                DRIVERNAME=Robin Ruijter
                GUID=76561197998202739
                */

                LogSuccess("Completed. press key to exit.");
                Console.ReadKey();
            }
            catch (Exception e)
            {
                LogError($"Fatal: {e.Message}");
                LogInfo("press a key to exit");
                Console.ReadKey();
                return;
            }
        }

        private static void LogInfo(string text)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(text);
        }

        private static void LogSuccess(string text)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(text);
        }

        private static void LogWarning(string text)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(text);
        }
        private static void LogError(string text)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(text);
        }


        private static void ConfigureServices(IServiceCollection serviceCollection)
        {
            // Add logging
            //serviceCollection.AddSingleton(LoggerFactory.Create(builder =>
            //{
            //    builder.AddConsole();
            //}));

            serviceCollection.AddLogging(configure => configure.AddConsole())
            .Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Information);
            //            .AddSingleton<ILogger>();

            serviceCollection.AddLogging();

            // Build configuration
            configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("appsettings.json", false)
                .Build();

            // Add access to generic IConfigurationRoot
            serviceCollection.AddSingleton<IConfigurationRoot>(configuration);

            // Add app
            //            serviceCollection.AddTransient<Program>();
        }

        private static async Task GenerateEntryList(IGrouping<string, Registration> carGroup)
        {
            if (!Directory.Exists(AssettoCorsaPath))
            {
                throw new Exception($"Game directory not found: {AssettoCorsaPath}");
            }

            var carKey = MapCarKey(carGroup.Key);
            LogInfo("");
            LogInfo($"Generating entrylist for car {carKey}");
            var skins = LoadSkinsData(carKey);

            if (!Directory.Exists(carKey))
            {
                Directory.CreateDirectory(carKey);
            }
            using (StreamWriter writer = File.CreateText($"{carKey}\\entry_list.ini"))
            {
                // try to find skin by name
                foreach (var reg in carGroup)
                {
                    var skinByDriverName = skins.FirstOrDefault(s => s.Drivername.Equals(reg.FullName, StringComparison.InvariantCultureIgnoreCase));
                    if (skinByDriverName != null)
                    {
                        reg.Skin = skinByDriverName.Directory;
                        reg.Team = skinByDriverName.Team;
                        reg.SkinFound = true;
                        reg.SkinFoundMode = "DriverName";
                        skins.Remove(skinByDriverName);
                    }
                }
                // try to find skin by given skin id
                bool uesOnlyOnce = skins.Count >= carGroup.Count(r => !r.SkinFound);
                foreach (var reg in carGroup.Where(r => !r.SkinFound && !String.IsNullOrEmpty(r.Skin)))
                {
                    LogInfo($" - - provided skinName: {reg.Skin}");
                    var skinByName = skins.FirstOrDefault(
                        s => s.Directory.Equals(reg.Skin, StringComparison.InvariantCultureIgnoreCase) ||
                        s.Skinname.Equals(reg.Skin, StringComparison.InvariantCultureIgnoreCase)
                        );
                    if (skinByName != null)
                    {
                        reg.Skin = skinByName.Directory;
                        reg.SkinFoundMode = "SkinName";
                        reg.SkinFound = true;
                        if (uesOnlyOnce)
                        {
                            skins.Remove(skinByName);
                        }
                    }
                }
                // randomizeSkin
                var random = new Random(DateTime.Now.Millisecond);
                foreach (var reg in carGroup.Where(r => !r.SkinFound))
                {
                    // {reg.Email} - 
                    LogWarning($"{reg.FullName}: No skin found, randomizing.");
                    var index = random.Next(0, skins.Count - 1);
                    var skin = skins[index];
                    reg.Skin = skin.Directory;
                    reg.SkinFoundMode = "Random";
                }

                var drivers = carGroup.ToList();

                LogInfo("");
                LogInfo("Adding TvCrew to list");
                drivers.Add(new Registration()
                {
                    FullName = "Tv Crew",
                    Skin = "knutselpacecar",
                    // Jake
                    SteamId64 = "76561198055060398",
                    Team = "Tv Crew",
                });


                LogInfo("");
                LogInfo("Adding RaceControl to list");
                drivers.Add(new Registration()
                {
                    FullName = "Race Control",
                    Skin = "knutselpacecar",
                    // Marcel
                    SteamId64 = "76561199043953770"
                });


                int count = 0;
                LogInfo("");
                LogInfo($"Registering drivers for car: {carKey}");
                foreach (var reg in drivers)
                {
                    /*
                    [CAR_2]
                    MODEL=bmw_m3_e30_gra
                    SKIN=1991_BTCC_77_Baird
                    BALLAST=0
                    RESTRICTOR=0
                    DRIVERNAME=Robin Ruijter
                    GUID=76561197998202739
                    */
                    //if (reg.SteamId64.Length < 17)
                    //{
                    //    LogError($"{reg.Email} - {reg.FullName}: Wrong SteamId64: {reg.SteamId64}.");
                    //}
                    if (reg.SteamId64.Length < 14)
                    {
                        LogError($"Error {reg.FullName}: Wrong SteamId64 (must be 15+ characters): {reg.SteamId64}. Driver not registered!");

                    }
                    else
                    {
                        await writer.WriteLineAsync($"[CAR_{count}]");
                        await writer.WriteLineAsync($"#EMAIL={reg.Email}");
                        await writer.WriteLineAsync($"DRIVERNAME={reg.FullName}");
                        await writer.WriteLineAsync($"GUID={reg.SteamId64}");
                        await writer.WriteLineAsync($"TEAM={reg.Team}");
                        await writer.WriteLineAsync($"MODEL={carKey}");
                        await writer.WriteLineAsync($"#SkinMode={reg.SkinFoundMode}");
                        await writer.WriteLineAsync($"SKIN={reg.Skin}");
                        await writer.WriteLineAsync($"BALLAST=0");
                        await writer.WriteLineAsync($"RESTRICTOR=0");
                        await writer.WriteLineAsync();
                        count++;
                        LogSuccess($"Registered driver: {reg.FullName}, SteamId64: {reg.SteamId64}, skin: {reg.Skin} ({reg.SkinFoundMode})");

                    }
                }
            }
        }

        private static List<SkinUi> LoadSkinsData(string carKey)
        {
            var skins = new List<SkinUi>();
            var carPath = Path.Combine(AssettoCorsaPath, CarsPath, carKey);
            if (!Directory.Exists(carPath))
            {
                throw new Exception($"car path not found: {carPath}");
            }

            string skinsPath = Path.Combine(carPath, "skins");

            foreach (var directory in Directory.EnumerateDirectories(skinsPath))
            {
                var SkinUi = LoadSkinUI(Path.Combine(directory, "ui_skin.json"));
                if (SkinUi != null)
                {
                    SkinUi.Directory = directory.Split("\\").Last();
                    skins.Add(SkinUi);
                }
            }
            return skins;
        }

        private static SkinUi LoadSkinUI(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }
            string uiContent = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<SkinUi>(uiContent);
        }

        private static string MapCarKey(string key)
        {
            switch (key)
            {
                case "BMW E30 grA":
                    return "bmw_m3_e30_gra";
                case "Peugeot 206 Gti":
                    return "peugeot_206_rps_206_GTi_Cup";
                case "Mazda MX5 CUP":
                    return "ks_mazda_mx5_cup";
                default:
                    return key;
            }
        }

        private static List<Registration> ReadCVS()
        {
            List<Registration> regs = new List<Registration>();
            var config = new ExcelReaderConfiguration()
            {
                // Gets or sets the encoding to use when the input XLS lacks a CodePage
                // record, or when the input CSV lacks a BOM and does not parse as UTF8. 
                // Default: cp1252 (XLS BIFF2-5 and CSV only)
                FallbackEncoding = Encoding.UTF8,

                // Gets or sets the password used to open password protected workbooks.
                Password = "password",

                // Gets or sets an array of CSV separator candidates. The reader 
                // autodetects which best fits the input data. Default: , ; TAB | # 
                // (CSV only)
                AutodetectSeparators = new char[] { ',', ';', '\t', '|', '#' },

                // Gets or sets a value indicating whether to leave the stream open after
                // the IExcelDataReader object is disposed. Default: false
                LeaveOpen = false,

                // Gets or sets a value indicating the number of rows to analyze for
                // encoding, separator and field count in a CSV. When set, this option
                // causes the IExcelDataReader.RowCount property to throw an exception.
                // Default: 0 - analyzes the entire file (CSV only, has no effect on other
                // formats)
                AnalyzeInitialCsvRows = 0,
            };

            using (FileStream fs = new FileStream(CsvPath, FileMode.Open))
            {
                var reader = ExcelReaderFactory.CreateCsvReader(fs, config);
                if (!reader.Read())
                {
                    throw new Exception($"Cannot read csv file: {CsvPath}");
                }

                List<string> headerRow = new List<string>();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    headerRow.Add(reader.GetString(i));
                }

                while (reader.Read())
                {
                    var jRow = new Dictionary<string, string>();
                    for (var i = 0; i < headerRow.Count; i++)
                    {
                        jRow.Add(MapHeader(headerRow[i]), reader.GetString(i));
                    }
                    var regStr = JsonConvert.SerializeObject(jRow);
                    regs.Add(JsonConvert.DeserializeObject<Registration>(regStr));
                }
            }
            return regs;
        }

        public class Registration
        {
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

        private static string MapHeader(string header)
        {
            switch (header)
            {
                case "Gebruikersnaam":
                    return "email";
                case "Volledige Naam":
                    return "fullName";
                case "SteamId64":
                    return "SteamId64";
                case "Kies je Auto":
                    return "car";
                case "Heb je een eigen livery/skin voor jouw auto gemaakt en naar ons gemaild, wat is de naam van de skin map?":
                    return "skin";
                case "Vertel iets over je zelf":
                    return "extraInfo";
                default:
                    return header;
            }
        }
    }
}
