namespace Chorbar.Model;

// TODO: do correctly!
public class IdentityProvider(Email email)
{
    public Email GetIdentity() => email;
}
