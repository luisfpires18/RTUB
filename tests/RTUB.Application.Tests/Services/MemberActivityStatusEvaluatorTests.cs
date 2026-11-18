using RTUB.Application.Services;

namespace RTUB.Application.Tests.Services;

/// <summary>The member activity rules (task 044B), pure: a fixed "now", no database.</summary>
public partial class MemberActivityStatusEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime D(int year, int month, int day) => new(year, month, day, 20, 0, 0);

    private static MemberActivityEvaluation Active(params DateTime[] activity) =>
        MemberActivityStatusEvaluator.Evaluate(false, activity, null, Now);

    private static MemberActivityEvaluation Reformed(DateTime now, params DateTime[] activity) =>
        MemberActivityStatusEvaluator.Evaluate(true, activity, null, now);

    // ---------- New Leitão: the six months run from the start date until there is activity ----------

    private static readonly DateTime LeitaoStart = new(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

    // ---------- Countdown (hotfix: the final partial month counts as 1) ----------

    private static readonly DateTime HotfixNow = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
}
