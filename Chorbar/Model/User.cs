using System.Collections.Immutable;

namespace Chorbar.Model;

public record User(
    Email Email,
    ImmutableDictionary<string, Chore> Chores,
    ImmutableArray<UserEvent> History
);
