namespace Chorbar.Model;

public record ShoppingListItem(int Id, string Label, DateTimeOffset? Checked)
{
    public bool CheckedOffRecently(TimeProvider timeProvider) =>
        Checked >= timeProvider.GetUtcNow() - TimeSpan.FromHours(6);
}
