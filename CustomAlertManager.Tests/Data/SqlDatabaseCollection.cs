namespace CustomAlertManager.Tests.Data;

// Both SQL test classes share one database, so they run one after another in this collection.
[CollectionDefinition("SqlDatabase")]
public sealed class SqlDatabaseCollection : ICollectionFixture<SqlDatabaseFixture>;
