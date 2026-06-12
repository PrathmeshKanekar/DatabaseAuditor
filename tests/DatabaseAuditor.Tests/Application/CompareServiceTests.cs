using DatabaseAuditor.Application.Services;
using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DatabaseAuditor.Tests.Application;

public class CompareServiceTests
{
    [Fact]
    public async Task CompareAsync_WhenColumnAdded_ShouldGenerateSyncScriptForSqlServer()
    {
        // Arrange
        var mockProvider = new Mock<IDatabaseProvider>();
        var mockResolver = new Mock<IDatabaseProviderResolver>();

        var sourceProfile = new ConnectionProfile
        {
            Id = Guid.NewGuid(),
            Name = "SourceDb",
            DatabaseType = DatabaseType.SqlServer
        };
        var targetProfile = new ConnectionProfile
        {
            Id = Guid.NewGuid(),
            Name = "TargetDb",
            DatabaseType = DatabaseType.SqlServer
        };

        mockResolver.Setup(r => r.GetProvider(DatabaseType.SqlServer)).Returns(mockProvider.Object);

        // Source column exists
        var sourceColumns = new List<ColumnSchema>
        {
            new ColumnSchema
            {
                Name = "Email",
                SchemaName = "dbo",
                TableName = "Users",
                DataType = "nvarchar",
                MaxLength = 255,
                IsNullable = true,
                DefaultValue = "'no-email@example.com'"
            }
        };

        // Target has no columns
        var targetColumns = new List<ColumnSchema>();

        mockProvider.Setup(p => p.GetColumnsAsync(sourceProfile, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceColumns);
        mockProvider.Setup(p => p.GetColumnsAsync(targetProfile, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetColumns);

        var compareService = new CompareService(mockResolver.Object);

        // Act
        var session = await compareService.CompareAsync(
            sourceProfile,
            targetProfile,
            CompareType.Column,
            ComparisonScope.EntireDatabase);

        // Assert
        session.Results.Should().ContainSingle();
        var result = session.Results.Single();
        result.ObjectType.Should().Be(CompareType.Column);
        result.ChangeType.Should().Be(ChangeType.Added);
        result.SyncScript.Should().Be("ALTER TABLE [dbo].[Users] ADD [Email] nvarchar(255)  DEFAULT 'no-email@example.com' NULL;");
    }

    [Fact]
    public async Task CompareAsync_WhenColumnAdded_ShouldGenerateSyncScriptForPostgreSql()
    {
        // Arrange
        var mockProvider = new Mock<IDatabaseProvider>();
        var mockResolver = new Mock<IDatabaseProviderResolver>();

        var sourceProfile = new ConnectionProfile
        {
            Id = Guid.NewGuid(),
            Name = "SourceDb",
            DatabaseType = DatabaseType.PostgreSql
        };
        var targetProfile = new ConnectionProfile
        {
            Id = Guid.NewGuid(),
            Name = "TargetDb",
            DatabaseType = DatabaseType.PostgreSql
        };

        mockResolver.Setup(r => r.GetProvider(DatabaseType.PostgreSql)).Returns(mockProvider.Object);

        var sourceColumns = new List<ColumnSchema>
        {
            new ColumnSchema
            {
                Name = "email",
                SchemaName = "public",
                TableName = "users",
                DataType = "varchar",
                MaxLength = 255,
                IsNullable = false
            }
        };
        var targetColumns = new List<ColumnSchema>();

        mockProvider.Setup(p => p.GetColumnsAsync(sourceProfile, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceColumns);
        mockProvider.Setup(p => p.GetColumnsAsync(targetProfile, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetColumns);

        var compareService = new CompareService(mockResolver.Object);

        // Act
        var session = await compareService.CompareAsync(
            sourceProfile,
            targetProfile,
            CompareType.Column,
            ComparisonScope.EntireDatabase);

        // Assert
        session.Results.Should().ContainSingle();
        var result = session.Results.Single();
        result.SyncScript.Should().Be("ALTER TABLE \"public\".\"users\" ADD COLUMN \"email\" varchar(255)  NOT NULL;");
    }
}
