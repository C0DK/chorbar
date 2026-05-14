using Chorbar.Utils;

namespace Chorbar.Model;

public class ClaimsBasedIdentityProvider(IHttpContextAccessor httpContextAccessor)
    : IIdentityProvider
{
    public Email GetIdentity()
    {
        // TODO: not like this right?
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated is true)
            return user.GetEmail();

        throw new LoginRequiredException();
    }
}

public class LoginRequiredException : Exception
{
    public LoginRequiredException() { }

    public LoginRequiredException(string message)
        : base(message) { }

    public LoginRequiredException(string message, Exception innerException)
        : base(message, innerException) { }
}
