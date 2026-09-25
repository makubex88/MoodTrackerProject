namespace MoodTrackerProject.Domain;

/// <summary>
/// The four moods the brief asks for, worst to best. Stored as the numeric value so ordering and
/// aggregation work in SQL; display labels live in one place on the client and never reach the API.
/// </summary>
public enum MoodRating : byte
{
    NotGoodAtAll = 1,
    ABitMeh = 2,
    PrettyGood = 3,
    FeelingGreat = 4,
}

public static class MoodRatingExtensions
{
    public static bool IsDefined(this MoodRating rating) =>
        rating is >= MoodRating.NotGoodAtAll and <= MoodRating.FeelingGreat;
}
