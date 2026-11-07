namespace OssmmasoftVerticalSlice.Features.Support;

internal static class SupportEventRules
{
    internal static List<int> Recipients(int actor, int requester, int? responsible, bool isInternal, bool responsibleAuthorized)
    {
        if (isInternal) return responsibleAuthorized && responsible.HasValue && responsible != actor && responsible != requester ? [responsible.Value] : [];
        return new[] { requester, responsible ?? 0 }.Where(id => id > 0 && id != actor).Distinct().ToList();
    }
}

