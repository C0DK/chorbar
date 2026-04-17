using Chorbar.Utils;

namespace Chorbar.Model;

public class ClaimsBasedIdentityProvider(IHttpContextAccessor httpContextAccessor)
    : IIdentityProvider
{
    public Email GetIdentity()
    {
        // TODO: not like this right?
        var user = httpContextAccessor.HttpContext?.User;
        if (user is not null && user?.Identity?.IsAuthenticated is true)
            return user.GetEmail();

        throw new LoginRequiredException();
    }
}

public class LoginRequiredException() : Exception();
