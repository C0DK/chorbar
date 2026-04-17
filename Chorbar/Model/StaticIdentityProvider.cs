namespace Chorbar.Model;

public class StaticIdentityProvider(Email email) : IIdentityProvider
{
    public Email GetIdentity() => email;
}
