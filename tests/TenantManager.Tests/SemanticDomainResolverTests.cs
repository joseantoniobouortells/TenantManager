using System;
using System.Collections.Generic;
using TenantManager.App.Domain;
using TenantManager.Core.Services.AI;
using Xunit;

namespace TenantManager.Tests;

public class SemanticDomainResolverTests
{
    [Fact]
    public void GetEffectiveEndDate_WithoutExtensions_UsesContractEndDate()
    {
        // Arrange
        var contract = new RentalContract
        {
            EndDate = new DateTimeOffset(new DateTime(2026, 6, 30))
        };
        var extensions = new List<RentalContractExtension>();

        // Act
        var result = SemanticDomainResolver.GetEffectiveEndDate(contract, extensions);

        // Assert
        Assert.Equal(new DateTimeOffset(new DateTime(2026, 6, 30)), result);
    }

    [Fact]
    public void GetEffectiveEndDate_WithOneExtension_UsesExtensionDate()
    {
        // Arrange
        var contract = new RentalContract
        {
            Id = 1,
            EndDate = new DateTimeOffset(new DateTime(2026, 6, 30))
        };
        var extensions = new List<RentalContractExtension>
        {
            new RentalContractExtension
            {
                RentalContractId = 1,
                EndDate = new DateTimeOffset(new DateTime(2026, 8, 31))
            }
        };

        // Act
        var result = SemanticDomainResolver.GetEffectiveEndDate(contract, extensions);

        // Assert
        Assert.Equal(new DateTimeOffset(new DateTime(2026, 8, 31)), result);
    }

    [Fact]
    public void GetEffectiveEndDate_WithMultipleExtensions_UsesLatestExtensionDate()
    {
        // Arrange
        var contract = new RentalContract
        {
            Id = 1,
            EndDate = new DateTimeOffset(new DateTime(2026, 6, 30))
        };
        var extensions = new List<RentalContractExtension>
        {
            new RentalContractExtension
            {
                RentalContractId = 1,
                EndDate = new DateTimeOffset(new DateTime(2026, 8, 31))
            },
            new RentalContractExtension
            {
                RentalContractId = 1,
                EndDate = new DateTimeOffset(new DateTime(2026, 10, 31))
            }
        };

        // Act
        var result = SemanticDomainResolver.GetEffectiveEndDate(contract, extensions);

        // Assert
        Assert.Equal(new DateTimeOffset(new DateTime(2026, 10, 31)), result);
    }

    [Fact]
    public void IsRoomOccupied_WhenActiveContractExists_ReturnsTrue()
    {
        // Arrange
        var room = new Room { Id = 3, IsActive = true };
        var contracts = new List<RentalContract>
        {
            new RentalContract
            {
                RoomId = 3,
                StartDate = DateTimeOffset.Now.AddMonths(-1),
                EndDate = DateTimeOffset.Now.AddMonths(5)
            }
        };
        var extensions = new List<RentalContractExtension>();

        // Act
        var result = SemanticDomainResolver.IsRoomOccupied(room, contracts, extensions, DateTimeOffset.Now);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsRoomAvailable_WhenActiveAndNoActiveContract_ReturnsTrue()
    {
        // Arrange
        var room = new Room { Id = 3, IsActive = true };
        var contracts = new List<RentalContract>();
        var extensions = new List<RentalContractExtension>();

        // Act
        var result = SemanticDomainResolver.IsRoomAvailable(room, contracts, extensions, DateTimeOffset.Now);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetEffectiveTenantMoveOutDate_WithExtensions_UsesLatestExtension()
    {
        // Arrange
        var tenant = new Tenant { Id = 1 };
        var contracts = new List<RentalContract>
        {
            new RentalContract
            {
                Id = 10,
                TenantId = 1,
                StartDate = DateTimeOffset.Now.AddMonths(-5),
                EndDate = DateTimeOffset.Now.AddMonths(1)
            }
        };
        var extensions = new List<RentalContractExtension>
        {
            new RentalContractExtension
            {
                RentalContractId = 10,
                EndDate = DateTimeOffset.Now.AddMonths(3)
            }
        };

        // Act
        var result = SemanticDomainResolver.GetEffectiveTenantMoveOutDate(tenant, contracts, extensions);

        // Assert
        Assert.Equal(DateTimeOffset.Now.AddMonths(3).Date, result?.Date);
    }
}
