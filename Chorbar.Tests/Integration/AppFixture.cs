using Chorbar.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Chorbar.Tests.Integration;

public class AppFixture : WebApplicationFactory<Program>
{
    public FakeMailer FakeMailer { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<NpgsqlDataSource>();
            services.AddSingleton<NpgsqlDataSource>(DatabaseFixture.DataSource);

            services.RemoveAll<IMailSender>();
            services.AddSingleton<IMailSender>(FakeMailer);
        });
    }
}
