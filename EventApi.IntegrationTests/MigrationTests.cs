using Npgsql;

namespace EventsAPI.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class MigrationTests
{
    private readonly PostgreSqlFixture _fixture;

    public MigrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Migrations_CreateTablesConstraintsAndQueryIndexes()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        // Act
        var eventsTableExists = await ExecuteBoolAsync(connection, """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'events');
            """);
        var bookingsTableExists = await ExecuteBoolAsync(connection, """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'bookings');
            """);
        var primaryKeysCount = await ExecuteIntAsync(connection, """
            SELECT COUNT(*)::int
            FROM information_schema.table_constraints
            WHERE table_schema = 'public'
              AND table_name IN ('events', 'bookings')
              AND constraint_type = 'PRIMARY KEY';
            """);
        var foreignKeyExists = await ExecuteBoolAsync(connection, """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                  ON tc.constraint_name = kcu.constraint_name
                 AND tc.constraint_schema = kcu.constraint_schema
                JOIN information_schema.constraint_column_usage ccu
                  ON tc.constraint_name = ccu.constraint_name
                 AND tc.constraint_schema = ccu.constraint_schema
                WHERE tc.constraint_type = 'FOREIGN KEY'
                  AND tc.table_name = 'bookings'
                  AND kcu.column_name = 'event_id'
                  AND ccu.table_name = 'events'
                  AND ccu.column_name = 'id');
            """);
        var cascadeDeleteConfigured = await ExecuteBoolAsync(connection, """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.referential_constraints
                WHERE constraint_schema = 'public' AND delete_rule = 'CASCADE');
            """);
        var requiredColumnsCount = await ExecuteIntAsync(connection, """
            SELECT COUNT(*)::int
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND is_nullable = 'NO'
              AND ((table_name = 'events' AND column_name IN ('title', 'start_at', 'end_at', 'total_seats', 'available_seats'))
                OR (table_name = 'bookings' AND column_name IN ('event_id', 'status', 'created_at')));
            """);
        var queryIndexesCount = await ExecuteIntAsync(connection, """
            SELECT COUNT(*)::int
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND indexname IN ('IX_bookings_status', 'IX_events_start_at', 'IX_events_end_at');
            """);
        var appliedMigrationsCount = await ExecuteIntAsync(connection, """
            SELECT COUNT(*)::int
            FROM "__EFMigrationsHistory"
            WHERE "MigrationId" IN (
                '20260927174014_InitialCreate',
                '20260928110000_AddQueryIndexes');
            """);

        // Assert
        Assert.True(eventsTableExists);
        Assert.True(bookingsTableExists);
        Assert.Equal(2, primaryKeysCount);
        Assert.True(foreignKeyExists);
        Assert.True(cascadeDeleteConfigured);
        Assert.Equal(8, requiredColumnsCount);
        Assert.Equal(3, queryIndexesCount);
        Assert.Equal(2, appliedMigrationsCount);
    }

    private static async Task<bool> ExecuteBoolAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<int> ExecuteIntAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (int)(await command.ExecuteScalarAsync())!;
    }
}
