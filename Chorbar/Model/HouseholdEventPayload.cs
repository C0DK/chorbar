using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chorbar.Model;

[JsonDerivedType(typeof(CreateNewHousehold), CreateNewHousehold.Kind)]
[JsonDerivedType(typeof(Rename), Rename.Kind)]
[JsonDerivedType(typeof(AddMember), AddMember.Kind)]
[JsonDerivedType(typeof(RemoveMember), RemoveMember.Kind)]
[JsonDerivedType(typeof(AddChore), AddChore.Kind)]
[JsonDerivedType(typeof(RemoveChore), RemoveChore.Kind)]
[JsonDerivedType(typeof(RenameChore), RenameChore.Kind)]
[JsonDerivedType(typeof(DoChore), DoChore.Kind)]
[JsonDerivedType(typeof(AddPastChoreCompletion), AddPastChoreCompletion.Kind)]
[JsonDerivedType(typeof(UndoChore), UndoChore.Kind)]
[JsonDerivedType(typeof(SetGoal), SetGoal.Kind)]
[JsonDerivedType(typeof(AddToShoppingList), AddToShoppingList.Kind)]
[JsonDerivedType(typeof(CheckOffShoppingListItem), CheckOffShoppingListItem.Kind)]
[JsonDerivedType(typeof(UnCheckOffShoppingListItem), UnCheckOffShoppingListItem.Kind)]
[JsonDerivedType(typeof(RenameShoppingListItem), RenameShoppingListItem.Kind)]
[JsonDerivedType(typeof(EnableShoppingList), EnableShoppingList.Kind)]
[JsonDerivedType(typeof(GenerateIcalToken), GenerateIcalToken.Kind)]
[JsonDerivedType(typeof(DeleteHousehold), DeleteHousehold.Kind)]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]
public abstract record HouseholdEventPayload
{
    [JsonIgnore]
    public abstract string EventKind { get; }

    public abstract bool IsValid(Household household, DateTimeOffset now);

    public abstract Household Apply(Household household, DateTimeOffset timestamp);

    public string Serialize() => JsonSerializer.Serialize(this);

    public static HouseholdEventPayload Deserialize(string payload) =>
        JsonSerializer.Deserialize<HouseholdEventPayload>(payload)!;
}
