using Garmin.Data.Entities;
using Dynastream.Fit;
using GarminActivity = Garmin.Data.Entities.Activity;

namespace Garmin.Data.Importing;

public static class FitActivityDecoder
{
    public static GarminActivity Decode(string path, byte[] sourceBytes, string sha256)
    {
        var sessions = new List<SessionMesg>();
        var laps = new List<LapMesg>();
        var records = new List<RecordMesg>();

        var decoder = new Decode();
        var broadcaster = new MesgBroadcaster();
        decoder.MesgEvent += broadcaster.OnMesg;
        decoder.MesgDefinitionEvent += broadcaster.OnMesgDefinition;
        broadcaster.SessionMesgEvent += (_, e) => sessions.Add((SessionMesg)e.mesg);
        broadcaster.LapMesgEvent += (_, e) => laps.Add((LapMesg)e.mesg);
        broadcaster.RecordMesgEvent += (_, e) => records.Add((RecordMesg)e.mesg);

        using (var stream = new MemoryStream(sourceBytes, writable: false))
        {
            decoder.Read(stream);
        }

        if (sessions.Count == 0)
            throw new InvalidDataException("FIT activity contains no session summary.");

        // A small number of Garmin files contain multiple session messages for one recorded activity.
        // Keep the first session as the activity-level summary while retaining all laps and records.
        var session = sessions[0];
        var activity = new GarminActivity
        {
            SourceKind = "FIT",
            SourceSha256 = sha256,
            FitSha256 = sha256,
            OriginalFileName = Path.GetFileName(path),
            FileSizeBytes = sourceBytes.LongLength,
            ImportedAtUtc = System.DateTime.UtcNow,
            StartedAtUtc = AsUtc(session.GetStartTime()),
            Sport = EnumValue(session.GetSport()),
            SubSport = EnumValue(session.GetSubSport()),
            ElapsedTimeSeconds = Number(session.GetTotalElapsedTime()),
            TimerTimeSeconds = Number(session.GetTotalTimerTime()),
            DistanceMeters = Number(session.GetTotalDistance()),
            Calories = Integer(session.GetTotalCalories()),
            AverageHeartRateBpm = Integer(session.GetAvgHeartRate()),
            MaximumHeartRateBpm = Integer(session.GetMaxHeartRate()),
            AverageSpeedMetersPerSecond = Number(session.GetEnhancedAvgSpeed()) ?? Number(session.GetAvgSpeed()),
            MaximumSpeedMetersPerSecond = Number(session.GetEnhancedMaxSpeed()) ?? Number(session.GetMaxSpeed()),
            SourceFile = new ActivitySourceFile { SourceFormat = "FIT", SourceBytes = sourceBytes }
        };

        for (var i = 0; i < laps.Count; i++)
        {
            var lap = laps[i];
            activity.Laps.Add(new ActivityLap
            {
                LapNumber = i + 1,
                StartedAtUtc = AsUtc(lap.GetStartTime()),
                ElapsedTimeSeconds = Number(lap.GetTotalElapsedTime()),
                TimerTimeSeconds = Number(lap.GetTotalTimerTime()),
                DistanceMeters = Number(lap.GetTotalDistance()),
                Calories = Integer(lap.GetTotalCalories()),
                AverageHeartRateBpm = Integer(lap.GetAvgHeartRate()),
                MaximumHeartRateBpm = Integer(lap.GetMaxHeartRate()),
                AverageSpeedMetersPerSecond = Number(lap.GetEnhancedAvgSpeed()) ?? Number(lap.GetAvgSpeed()),
                MaximumSpeedMetersPerSecond = Number(lap.GetEnhancedMaxSpeed()) ?? Number(lap.GetMaxSpeed())
            });
        }

        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            activity.Records.Add(new ActivityRecord
            {
                RecordNumber = i,
                TimestampUtc = AsUtc(record.GetTimestamp()),
                LatitudeDegrees = SemicirclesToDegrees(record.GetPositionLat()),
                LongitudeDegrees = SemicirclesToDegrees(record.GetPositionLong()),
                AltitudeMeters = Number(record.GetEnhancedAltitude()) ?? Number(record.GetAltitude()),
                HeartRateBpm = Integer(record.GetHeartRate()),
                CadenceRpm = Integer(record.GetCadence()),
                DistanceMeters = Number(record.GetDistance()),
                SpeedMetersPerSecond = Number(record.GetEnhancedSpeed()) ?? Number(record.GetSpeed()),
                PowerWatts = Integer(record.GetPower()),
                TemperatureCelsius = Integer(record.GetTemperature())
            });
        }

        return activity;
    }

    private static System.DateTime? AsUtc(Dynastream.Fit.DateTime? value) => value is null
        ? null
        : System.DateTime.SpecifyKind(value.GetDateTime(), System.DateTimeKind.Utc);

    private static double? Number<T>(T? value) where T : struct, IConvertible =>
        value.HasValue ? Convert.ToDouble(value.Value, System.Globalization.CultureInfo.InvariantCulture) : null;

    private static int? Integer<T>(T? value) where T : struct, IConvertible =>
        value.HasValue ? Convert.ToInt32(value.Value, System.Globalization.CultureInfo.InvariantCulture) : null;

    private static int? EnumValue<T>(T? value) where T : struct, Enum =>
        value.HasValue ? Convert.ToInt32(value.Value, System.Globalization.CultureInfo.InvariantCulture) : null;

    private static double? SemicirclesToDegrees(int? value) => value is null
        ? null
        : value.Value * (180.0 / 2147483648.0);
}
