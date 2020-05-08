using System;
using System.Collections.Generic;
using System.Text;

namespace ACEntryListGenerator.Models
{
    public partial class RaceResults
    {
        public string TrackName { get; set; }
        public string TrackConfig { get; set; }
        public string Type { get; set; }
        public long DurationSecs { get; set; }
        public long RaceLaps { get; set; }
        public Car[] Cars { get; set; }
        public Result[] Result { get; set; }
        public Lap[] Laps { get; set; }
        public Event[] Events { get; set; }
    }

    public partial class Car
    {
        public long CarId { get; set; }
        public Driver Driver { get; set; }
        public string Model { get; set; }
        public string Skin { get; set; }
        public long BallastKg { get; set; }
        public long Restrictor { get; set; }
    }

    public partial class Driver
    {
        public string Name { get; set; }
        public string Team { get; set; }
        public string Nation { get; set; }
        public string Guid { get; set; }
        public string[] GuidsList { get; set; }
    }

    public partial class Event
    {
        public string Type { get; set; }
        public long CarId { get; set; }
        public Driver Driver { get; set; }
        public long OtherCarId { get; set; }
        public Driver OtherDriver { get; set; }
        public double ImpactSpeed { get; set; }
        public Position WorldPosition { get; set; }
        public Position RelPosition { get; set; }
    }

    public partial class Position
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public partial class Lap
    {
        public string DriverName { get; set; }
        public string DriverGuid { get; set; }
        public long CarId { get; set; }
        public string CarModel { get; set; }
        public long Timestamp { get; set; }
        public long LapTime { get; set; }
        public long[] Sectors { get; set; }
        public long Cuts { get; set; }
        public long BallastKg { get; set; }
        public string Tyre { get; set; }
        public long Restrictor { get; set; }
    }

    public partial class Result
    {
        public string DriverName { get; set; }
        public string DriverGuid { get; set; }
        public long CarId { get; set; }
        public string CarModel { get; set; }
        public long BestLap { get; set; }
        public long TotalTime { get; set; }
        public long BallastKg { get; set; }
        public long Restrictor { get; set; }
    }
}
