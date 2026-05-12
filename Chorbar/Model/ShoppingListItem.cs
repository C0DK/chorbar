namespace Chorbar.Model;

public record ShoppingListItem(
    int Id,
    string Label,
    DateTimeOffset? Checked = null,
    string? Category = null,
    int Order = -1
)
{
    public bool IsChecked => Checked is not null;

    public bool CheckedOffRecently(TimeProvider timeProvider) =>
        Checked >= timeProvider.GetUtcNow() - TimeSpan.FromHours(2);
}
