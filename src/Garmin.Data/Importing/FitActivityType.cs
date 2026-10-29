namespace Garmin.Data.Importing;

/// <summary>Maps FIT session sport codes to activity types used by the app.</summary>
public static class FitActivityType
{
    public static string? FromSport(int? sport, int? subSport) => (sport, subSport) switch
    {
        (0, _) => "other",
        (1, _) => "running",
        (2, _) => "cycling",
        (5, _) => "swimming",
        (10, 20) => "strength_training",
        (10, 26) => "indoor_cardio",
        (11, 30) => "casual_walking",
        (11, _) => "walking",
        _ => null
    };
}
