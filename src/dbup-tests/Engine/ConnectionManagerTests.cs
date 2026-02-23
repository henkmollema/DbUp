using System;
using System.Data;
using DbUp.Engine.Output;
using DbUp.Engine.Transactions;
using DbUp.Tests.Common;
using DbUp.Tests.TestInfrastructure;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DbUp.Tests.Engine;

public class ConnectionManagerTests
{
    [Fact]
    public void TryConnect_ShouldNotAffectSubsequentPerformUpgrade()
    {
        // Arrange
        var connectionFactory = new TrackingConnectionFactory();
        var testProvider = new TestProvider();
        testProvider.Builder.Configure(c => c.ConnectionManager = new TestConnectionManager(connectionFactory));
        
        var upgradeEngine = testProvider.Builder
            .WithScript("testscript1", "SELECT 1")
            .Build();

        // Act - Call TryConnect first
        var canConnect = upgradeEngine.TryConnect(out var errorMessage);
        
        // Assert - TryConnect should succeed
        canConnect.ShouldBeTrue();
        errorMessage.ShouldBeEmpty();
        connectionFactory.ConnectionsCreated.ShouldBe(1);
        connectionFactory.ConnectionsDisposed.ShouldBe(1); // TryConnect should dispose its connection
        
        // Act - Then call PerformUpgrade
        var result = upgradeEngine.PerformUpgrade();
        
        // Assert - PerformUpgrade should succeed with a fresh connection
        result.Successful.ShouldBeTrue();
        result.Error.ShouldBeNull();
        connectionFactory.ConnectionsCreated.ShouldBe(2); // New connection created
        connectionFactory.ConnectionsDisposed.ShouldBe(2); // Both connections disposed
    }

    [Fact]
    public void OperationStarting_ShouldCreateNewConnection()
    {
        // Arrange
        var connectionFactory = new TrackingConnectionFactory();
        var testProvider = new TestProvider();
        testProvider.Builder.Configure(c => c.ConnectionManager = new TestConnectionManager(connectionFactory));
        
        var upgradeEngine = testProvider.Builder
            .WithScript("testscript1", "SELECT 1")
            .Build();

        // Act
        var result = upgradeEngine.PerformUpgrade();
        
        // Assert
        result.Successful.ShouldBeTrue();
        result.Error.ShouldBeNull();
        connectionFactory.ConnectionsCreated.ShouldBe(1);
        connectionFactory.ConnectionsDisposed.ShouldBe(1);
    }

    [Fact]
    public void CreateConnection_ReturningNull_ShouldFailWithInvalidOperationException()
    {
        // Arrange
        var connectionFactory = new NullReturningConnectionFactory();
        var testProvider = new TestProvider();
        testProvider.Builder.Configure(c => c.ConnectionManager = new TestConnectionManager(connectionFactory));
        
        var upgradeEngine = testProvider.Builder
            .WithScript("testscript1", "SELECT 1")
            .Build();

        // Act
        var result = upgradeEngine.PerformUpgrade();
        
        // Assert
        result.Successful.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldBeOfType<InvalidOperationException>();
        result.Error.Message.ShouldContain("CreateConnection returned null");
    }

    class TrackingConnectionFactory : IConnectionFactory
    {
        public int ConnectionsCreated { get; private set; }
        public int ConnectionsDisposed { get; private set; }

        public IDbConnection CreateConnection(IUpgradeLog upgradeLog, DatabaseConnectionManager databaseConnectionManager)
        {
            ConnectionsCreated++;
            
            var conn = Substitute.For<IDbConnection>();
            conn.State.Returns(ConnectionState.Closed);
            
            conn.When(c => c.Open()).Do(_ => { });
            conn.When(c => c.Dispose()).Do(_ => ConnectionsDisposed++);
            
            return conn;
        }
    }

    class NullReturningConnectionFactory : IConnectionFactory
    {
        public IDbConnection CreateConnection(IUpgradeLog upgradeLog, DatabaseConnectionManager databaseConnectionManager)
        {
            return null;
        }
    }
}
