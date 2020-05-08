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
        static readonly bool GridFromResult;
        static readonly bool InvertedGrid;

        static readonly string ResultsFilePath;

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

            ResultsFilePath = configuration["ResultsFile"];
            GridFromResult = Boolean.TryParse(configuration["GridFromResult"] ?? "false", out var gridFromResult) ? gridFromResult : false;
            InvertedGrid = Boolean.TryParse(configuration["InvertedGrid"] ?? "false", out var invertedGrid) ? invertedGrid : false;

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
                    FullName = "Broadcast",
                    Skin = "knutselpacecar",
                    SteamId64 = brodcastId
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
                    SteamId64 = raceControlSteamId
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
        }

        /*
         ACEntryListGenerator.exe: generates entrylist
         ACEntryListGenerator.exe -result [resultFile]: generates results and score for this race
         ACEntryListGenerator.exe -revertgrid [resultFile]: generates new REVERSED grid based on results
        */
        static async Task Main(string[] args)
        {
            try
            {
                // laod CSV
                var registrations = ReadCVS();
                var carGroups = registrations.GroupBy(r => r.Car);

                var results = ReadResultsFile();

                if (results != null)
                {
                    var resultPath = ResultsFilePath.Substring(0, ResultsFilePath.Length - 5) + ".csv";
                    GenerateResultsScoreFile(results, resultPath, registrations);
                }

                foreach (var carGroup in carGroups)
                {
                    await GenerateEntryList(carGroup, results);
                }

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

        static int[] Points = new[] { 25, 18, 15, 12, 10, 8, 6, 4, 2, 1 };
        private static void GenerateResultsScoreFile(RaceResults results, string filePath, List<Registration> registrations)
        {
            using (StreamWriter writer = File.CreateText(filePath))
            {
                writer.WriteLine("Pos,Driver,car,skin,Laps,TotalTime,GapToFirst,BestLap,Contacts,Points");
                int pos = 1;
                int firstLaps = results.Laps.Count(l => l.DriverGuid == results.Result.First().DriverGuid);
                var firstTime = TimeSpan.FromMilliseconds(results.Result.First().TotalTime);
                foreach (var result in results.Result)
                {
                    if (string.IsNullOrEmpty(result.DriverGuid))
                        continue;

                    var registeredDriver = registrations.FirstOrDefault(reg => reg.SteamId64 == result.DriverGuid);
                    var laps = results.Laps.Where(l => l.DriverGuid == result.DriverGuid);
                    var totalTime = TimeSpan.FromMilliseconds(result.TotalTime);
                    var bestLap = TimeSpan.FromMilliseconds(result.BestLap);
                    var diffToFirst = (totalTime - firstTime);
                    var contacts = results.Events.Count(e => e.Type == "COLLISION_WITH_CAR" && e.CarId == result.CarId);
                    var lapsGap = firstLaps - laps.Count();
                    var gapToFirst = lapsGap == 0 ? $"{diffToFirst:mm\\:ss\\.fff}" : $"{lapsGap} Lap{(lapsGap > 1 ? "s" : "")}";
                    var totalTimeString = totalTime == TimeSpan.Zero ? "DNF" : totalTime.ToString(@"mm\:ss\.fff");
                    var points = GetPoints(pos);
                    writer.WriteLine($"{pos},{registeredDriver?.FullName ?? result.DriverName},{result.CarModel},{registeredDriver?.Skin ?? "unknown"},{laps.Count()},{totalTimeString},+{gapToFirst},{bestLap:mm\\:ss\\.fff},{contacts},{points}");
                    pos++;
                }
            }
        }

        private static int GetPoints(int pos)
        {
            if (pos > Points.Length)
                return 0;
            return Points[pos - 1];
        }

        private static RaceResults ReadResultsFile()
        {
            if (String.IsNullOrWhiteSpace(ResultsFilePath))
            {
                return null;
            }

            if (!File.Exists(ResultsFilePath))
            {
                LogWarning($"Result file not found, skipping: {ResultsFilePath}");
                return null;
            }

            var resultStr = File.ReadAllText(ResultsFilePath);
            var raceResults = JsonConvert.DeserializeObject<RaceResults>(resultStr);
            return raceResults;
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

        private static async Task GenerateEntryList(IGrouping<string, Registration> carGroup, RaceResults results)
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


            if (!Directory.Exists(carGroup.Key))
            {
                Directory.CreateDirectory(carGroup.Key);
            }
            using (StreamWriter writer = File.CreateText($"{carGroup.Key}\\entry_list.ini"))
            {
                List<Registration> driversToRegister = new List<Registration>();

                if (GridFromResult && results != null)
                {

                    var sortedResults = results.Result.Where(
                            i => !(
                                    string.IsNullOrWhiteSpace(i.DriverGuid)
                                    || i.DriverGuid == BroadcastReg?.SteamId64
                                    || i.DriverGuid == RaceControlReg?.SteamId64
                                    || i.DriverName.Equals("Race Control", StringComparison.InvariantCultureIgnoreCase)
                                    || i.DriverName.Equals("Broadcast", StringComparison.InvariantCultureIgnoreCase)
                                    )).ToList();
                    if (InvertedGrid)
                    {
                        sortedResults.Reverse();
                    }

                    foreach (var result in sortedResults)
                    {
                        var registeredDriver = carGroup.FirstOrDefault(reg => reg.SteamId64 == result.DriverGuid);
                        if (registeredDriver == null)
                        {
                            LogWarning($"driver from result not found in registration {result.DriverName}: {result.DriverGuid}");
                        }
                        else
                        {
                            driversToRegister.Add(registeredDriver);
                        }
                    }
                    // Add drivers that were not in result file as last
                    foreach (var driver in carGroup)
                    {
                        if (!driversToRegister.Exists(d => d.SteamId64 == driver.SteamId64))
                        {
                            driversToRegister.Add(driver);
                        }
                    }
                }
                else
                {
                    driversToRegister = carGroup.OrderBy(c => c.PositionOnGrid).ToList();
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
                    // SteamId64: {reg.SteamId64},
                    LogSuccess($"Registered driver: {reg.FullName}, car: {reg.Car}, skin: {reg.Skin} ({reg.SkinFoundMode})");
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

            return FixRegistrationList(regs);
        }

        private static List<Registration> FixRegistrationList(List<Registration> regs)
        {
            List<Registration> fixedList = new List<Registration>();
            foreach (var reg in regs)
            {
                // Deduplicating
                var duplicates = regs.Where(r => r.Email == reg.Email).ToList();
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

                // checkSteamId
                reg.Car = Cars.MapCarKey(reg.Car);
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
                        continue;
                    }
                }
                fixedList.Add(reg);
            }

            FixSkins(fixedList);

            return fixedList;
        }

        private static void FixSkins(List<Registration> fixedList)
        {
            var skinsDic = new Dictionary<string, List<SkinUi>>();
            foreach (var car in fixedList.Select(r => r.Car).Distinct())
            {
                skinsDic.Add(car, LoadSkinsData(car));
            }

            // try to find skin by name
            foreach (var reg in fixedList)
            {
                var skins = skinsDic[reg.Car];
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
            foreach (var reg in fixedList.Where(r => !r.SkinFound && !String.IsNullOrWhiteSpace(r.Skin)))
            {
                var skins = skinsDic[reg.Car];
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
                    if (skins.Count >= fixedList.Count(r => !r.SkinFound))
                    {
                        skins.Remove(skinByName);
                    }
                }
            }
            // randomizeSkin
            var random = new Random(DateTime.Now.Millisecond);
            foreach (var reg in fixedList.Where(r => !r.SkinFound))
            {
                var skins = skinsDic[reg.Car];
                // {reg.Email} - 
                LogWarning($"{reg.FullName}: No skin found, randomizing.");
                var index = random.Next(0, skins.Count - 1);
                var skin = skins[index];
                reg.Skin = skin.Directory;
                reg.SkinFoundMode = "Random";
            }
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
