namespace Learnier.Domain.Billing;

/// <summary>Birebir ders paketlerinin süre, sıklık ve taahhüt bazlı fiyatını hesaplar.</summary>
public static class LessonPackagePricing
{
    private const int WeeksPerMonth = 4;

    public static decimal CalculateTotal(
        int lessonsPerWeek,
        int durationMonths,
        int lessonDurationMinutes)
    {
        var basePricePerLesson = lessonDurationMinutes switch
        {
            30 => 150m,
            50 => 250m,
            _ => throw new ArgumentOutOfRangeException(
                nameof(lessonDurationMinutes),
                lessonDurationMinutes,
                "Ders süresi 30 veya 50 dakika olmalıdır.")
        };
        var frequencyDiscount = lessonsPerWeek switch
        {
            2 => 0m,
            3 => 0.05m,
            5 => 0.12m,
            _ => throw new ArgumentOutOfRangeException(nameof(lessonsPerWeek))
        };
        var durationDiscount = durationMonths switch
        {
            6 => 0m,
            12 => 0.10m,
            _ => throw new ArgumentOutOfRangeException(nameof(durationMonths))
        };
        var totalLessons = lessonsPerWeek * WeeksPerMonth * durationMonths;

        return decimal.Round(
            totalLessons
            * basePricePerLesson
            * (1m - frequencyDiscount - durationDiscount),
            0,
            MidpointRounding.AwayFromZero);
    }
}
