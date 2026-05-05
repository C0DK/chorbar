using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Npgsql;

namespace Chorbar;

public sealed class PgXmlRepository : IXmlRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PgXmlRepository(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var connection = _dataSource.OpenConnection();
        using var command = new NpgsqlCommand(
            //language=sql
            "SELECT xml FROM data_protection_key",
            connection
        );
        using var reader = command.ExecuteReader();
        var elements = new List<XElement>();
        while (reader.Read())
            elements.Add(XElement.Parse(reader.GetString(0)));
        return elements;
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var connection = _dataSource.OpenConnection();
        using var command = new NpgsqlCommand(
            //language=sql
            """
            INSERT INTO data_protection_key (friendly_name, xml)
            VALUES ($1, $2)
            ON CONFLICT (friendly_name) DO UPDATE SET xml = EXCLUDED.xml
            """,
            connection
        );
        command.Parameters.AddWithValue(friendlyName);
        command.Parameters.AddWithValue(element.ToString(SaveOptions.DisableFormatting));
        command.ExecuteNonQuery();
    }
}
