namespace Chorbar.Model;

public record UserInfo(Email Email, string DisplayName)
{
    public static UserInfo Create(Email email) => new UserInfo(Email: email, DisplayName: email);
}
