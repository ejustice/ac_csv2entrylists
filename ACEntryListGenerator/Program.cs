using ACEntryListGenerator.Models;
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
    partial class Program
    {
        static readonly string CsvPath; // = @"C:\tmp\DNRT-regs\DNRT - KRPE Race 10 Mei Zolder.csv";
        static readonly string AssettoCorsaPath; // = @"C:\Program Files (x86)\Steam\steamapps\common\assettocorsa\";
        const string CarsPath = @"content\cars\";

        public static IConfigurationRoot configuration;

        static readonly Registration BroadcastReg;
        static readonly Registration RaceControlReg;
        static readonly Dictionary<string, string> WrongIdFixDictionary = new Dictionary<string, string>();

        static Program()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            // Create service collection
            LogInfo("Building service provider");
            ServiceCollection serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            CsvPath = configuration["CsvPath"];
            LogInfo($"getting csv at {CsvPath}");
            if (!File.Exists(CsvPath))
            {
                LogError($"File not found: {CsvPath}");
                throw new Exception();
            }

            AssettoCorsaPath = configuration["AssettoCorsaPath"];
            LogInfo($"AssettoCorsaPath at {AssettoCorsaPath}");

            if (!Directory.Exists(AssettoCorsaPath))
            {
                LogError($"Game directory not found: {AssettoCorsaPath}");
                throw new Exception();
            }

            var brodcastId = configuration["BroadcastSteamId"];
            if (String.IsNullOrEmpty(brodcastId))
            {
                BroadcastReg = null;
            }
            else
            {
                BroadcastReg = new Registration()
                {
                    Car = Cars.BMW_E30_GRA,
                    FullName = "Tv Crew",
                    Skin = "knutselpacecar",
                    SteamId64 = brodcastId,
                    Team = "Tv Crew",
                };
            }

            var raceControlSteamId = configuration["RaceControlSteamId"];
            if (String.IsNullOrEmpty(raceControlSteamId))
            {
                RaceControlReg = null;
            }
            else
            {
                RaceControlReg = new Registration()
                {
                    Car = Cars.BMW_E30_GRA,
                    FullName = "Race Control",
                    Skin = "knutselpacecar",
                    SteamId64 = raceControlSteamId,
                };
            }

            var wrongIdFix = configuration["WrongIdFixMapping"];
            if (!string.IsNullOrWhiteSpace(wrongIdFix))
            {
                var list = wrongIdFix.Split(";");
                foreach (var fixItem in list)
                {
                    var parts = fixItem.Split("|");
                    WrongIdFixDictionary.Add(parts[0], parts[1]);
                }
            }


            var (_, error) = Init(configuration);
            if (!String.IsNullOrWhiteSpace(error))
            {
                LogError(error);
            }
        }

        private static (string, string) Init(IConfigurationRoot configuration)
        {

            return (null, null);
        }

        // Key = "carFolder|skinFolder"
        static async Task Main(string[] args)
        {
            try
            {
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
            Console.WriteLine($"[WARNING]: {text}");
        }
        private static void LogError(string text)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR]: {text}");
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

            LogInfo("");
            LogInfo($"Generating entrylist for car {carGroup.Key}");

            // Max in Zolder is 30 -RaceControl, -Broadcast
            int maxCars = GetMaxCars();
            if (carGroup.Count() > 28)
            {
                LogWarning($"Too many cars in group: {carGroup.Count()}/28");
            }

            var skins = LoadSkinsData(carGroup.Key);

            if (!Directory.Exists(carGroup.Key))
            {
                Directory.CreateDirectory(carGroup.Key);
            }
            using (StreamWriter writer = File.CreateText($"{carGroup.Key}\\entry_list.ini"))
            {
                var driversToRegister = new List<Registration>();
                foreach (var reg in carGroup)
                {
                    // Deduplicating
                    var duplicates = carGroup.Where(r => r.Email == reg.Email).ToList();
                    if (duplicates.Count > 1)
                    {
                        LogWarning($"Driver with email address {reg.Email} appears multiple times.");
                        if (reg.Added < duplicates.Max(r => r.Added))
                        {
                            LogWarning("not the latest instance of this driver, skipping.");
                            continue;
                        }
                        else
                        {
                            LogInfo("latest instance of driver, continuing");
                        }
                    }
                    driversToRegister.Add(reg);
                }

                // try to find skin by name
                foreach (var reg in driversToRegister)
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
                bool uesOnlyOnce = skins.Count >= driversToRegister.Count(r => !r.SkinFound);
                foreach (var reg in driversToRegister.Where(r => !r.SkinFound && !String.IsNullOrWhiteSpace(r.Skin)))
                {
                    LogInfo($" - {reg.FullName} - provided skinName: {reg.Skin}");
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
                foreach (var reg in driversToRegister.Where(r => !r.SkinFound))
                {
                    // {reg.Email} - 
                    LogWarning($"{reg.FullName}: No skin found, randomizing.");
                    var index = random.Next(0, skins.Count - 1);
                    var skin = skins[index];
                    reg.Skin = skin.Directory;
                    reg.SkinFoundMode = "Random";
                }

                if (BroadcastReg != null)
                {
                    LogInfo("");
                    LogInfo("Adding TvCrew to list");
                    driversToRegister.Add(BroadcastReg);
                }

                if (RaceControlReg != null)
                {
                    LogInfo("");
                    LogInfo("Adding RaceControl to list");
                    driversToRegister.Add(RaceControlReg);
                }

                int count = 0;
                LogInfo("");
                LogInfo($"Registering drivers for car: {carGroup.Key}");
                foreach (var reg in driversToRegister)
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
                        if (WrongIdFixDictionary.ContainsKey(reg.SteamId64))
                        {
                            LogWarning($"Wrong steamId for {reg.FullName}, fix found. replacing {reg.SteamId64} with {WrongIdFixDictionary[reg.SteamId64]}");
                            reg.SteamId64 = WrongIdFixDictionary[reg.SteamId64];
                        }
                        else
                        {
                            LogError($"Error {reg.FullName}: Wrong SteamId64 (must be 15+ characters): {reg.SteamId64}. Driver not registered!");
                        }
                    }
                    else
                    {
                        await writer.WriteLineAsync($"[CAR_{count}]");
                        await writer.WriteLineAsync($"#EMAIL={reg.Email}");
                        await writer.WriteLineAsync($"DRIVERNAME={reg.FullName}");
                        await writer.WriteLineAsync($"GUID={reg.SteamId64}");
                        await writer.WriteLineAsync($"TEAM={reg.Team}");
                        await writer.WriteLineAsync($"MODEL={reg.Car}");
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

        private static int GetMaxCars()
        {
            int max = 30; // max of zolder
            if (RaceControlReg != null)
            {
                max--;
            }
            if (BroadcastReg != null)
            {
                max--;
            }
            return max;
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
                    // don't really care about actual time  or time one here, just need time to know which is last in case of duplicates.
                    // 2020/05/03 4:29:34 p.m. EET
                    // Yes, it is a shitty way to parse date, but it works
                    var dt = DateTime.ParseExact(jRow["added"].Replace(" EET", "").Replace("a.m.", "AM").Replace("p.m.", "PM"), "yyyy/M/d h:mm:ss tt", CultureInfo.InvariantCulture);
                    jRow["added"] = dt.ToString(CultureInfo.InvariantCulture);
                    // and yes, this is a shitty way to parse my csv row to object, but it does the job :)
                    var regStr = JsonConvert.SerializeObject(jRow);
                    regs.Add(JsonConvert.DeserializeObject<Registration>(regStr));
                }
            }
            // fix/map car property
            foreach (var reg in regs)
            {
                reg.Car = Cars.MapCarKey(reg.Car);
            }

            return regs;
        }

        private static string MapHeader(string header)
        {
            switch (header)
            {
                case "Tijdstempel":
                    return "added";
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
