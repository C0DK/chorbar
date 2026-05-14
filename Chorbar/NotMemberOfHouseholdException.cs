using Chorbar.Model;

namespace Chorbar;

public class NotMemberOfHouseholdException : Exception
{
    public NotMemberOfHouseholdException() { }

    public NotMemberOfHouseholdException(string message)
        : base(message) { }

    public NotMemberOfHouseholdException(string message, Exception innerException)
        : base(message, innerException) { }

    public NotMemberOfHouseholdException(HouseholdId householdId, Email identity)
        : base($"User '{identity}' is not a member of #{householdId}") { }
}
