using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Chorbar.Model;

public record SortCategories(ImmutableArray<string> Categories) : HouseholdEventPayload
{
    [JsonIgnore]
    public const string Kind = "sort_categories";

    public override string EventKind => Kind;

    public override bool IsValid(Household household) =>
        Categories.All(category => household.ShoppingListCategories.Contains(category));

    public override Household Apply(Household household, DateTimeOffset timestamp) =>
        household with
        {
            ShoppingListCategories = Categories,
        };
}
