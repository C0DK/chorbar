using Chorbar.Model;

namespace Chorbar.Utils;

public static class SessionHelpers
{
    public const string HouseHoldNameSessionKey = "householdName";

    public static void UpdateHousehold(this HttpContext context, Household household) =>
        context.Session.SetString(HouseHoldNameSessionKey, household.Name);

    public static void ClearHousehold(this HttpContext context) =>
        context.Session.Remove(HouseHoldNameSessionKey);

    public static string? GetHouseholdName(this HttpContext context) =>
        context.Session.GetString(HouseHoldNameSessionKey);
}
