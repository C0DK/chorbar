using Chorbar.Model;

namespace Chorbar;

public class NotMemberOfHouseholdException(HouseholdId HouseholdId, Email Identity)
    : Exception($"User '{Identity}' is not a member of #{HouseholdId}");
