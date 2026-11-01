using Npgsql;
using Testcontainers.PostgreSql;

namespace SchoolHub.Tests.Outils;

/// <summary>
/// Un PostgreSQL jetable, lancé dans Docker pour les tests puis effacé.
/// Chaque classe de tests prend sa propre base, pour ne pas gêner les autres.
/// </summary>
public sealed class PostgreSqlDeTest : IAsyncLifetime
{
    // La même version que docker-compose.yml et qu'Azure.
    private readonly PostgreSqlContainer _conteneur = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ChaineDeConnexion(string nomDeLaBase) =>
        new NpgsqlConnectionStringBuilder(_conteneur.GetConnectionString()) { Database = nomDeLaBase }.ConnectionString;

    public Task InitializeAsync() => _conteneur.StartAsync();

    public Task DisposeAsync() => _conteneur.DisposeAsync().AsTask();
}

[CollectionDefinition(Nom)]
public sealed class CollectionPostgreSql : ICollectionFixture<PostgreSqlDeTest>
{
    public const string Nom = "PostgreSQL";
}
