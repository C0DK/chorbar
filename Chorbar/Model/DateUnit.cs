using System.ComponentModel;

namespace Chorbar.Model;

public enum DateUnit
{
    Day,
    Week,
    Month,
    Year,
}

public static class DateUnitExtensions
{
    public static string Letter(this DateUnit unit) =>
        unit switch
        {
            DateUnit.Day => "d",
            DateUnit.Week => "w",
            DateUnit.Month => "m",
            DateUnit.Year => "y",
            _ => throw new InvalidEnumArgumentException(),
        };
}
