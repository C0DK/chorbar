using System.Security.Claims;
using Chorbar.Model;

namespace Chorbar.Utils;

public static class ClaimsExtensions
{
    public static Email? GetEmailOrNull(this ClaimsPrincipal principal)
    {
        var claim = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);

        if (claim is null)
            return null;

        return Email.Parse(claim.Value);
    }

    public static Email GetEmail(this ClaimsPrincipal principal) =>
        principal.GetEmailOrNull()
        ?? throw new InvalidOperationException("Authenticated user has no email claim.");
}
