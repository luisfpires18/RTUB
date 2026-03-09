using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public class ShopServiceTests
{
    [Fact]
    public async Task ExchangeFitabForLeitaoAsync_WithEnoughFitab_ShouldSucceed()
    {
        var inventoryRepository = new Mock<IInventoryRepository>();
        inventoryRepository
            .Setup(repo => repo.GetItemAsync("user-1", InventoryItemType.Fitab, It.IsAny<CancellationToken>()))
            .ReturnsAsync(InventoryItem.Create("user-1", InventoryItemType.Fitab, 40));
        inventoryRepository
            .Setup(repo => repo.ConsumeItemAsync("user-1", InventoryItemType.Fitab, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new ShopService(inventoryRepository.Object, Mock.Of<ILogger<ShopService>>());

        var result = await service.ExchangeFitabForLeitaoAsync("user-1");

        result.Success.Should().BeTrue();
        result.NewFitabBalance.Should().Be(15);
        result.Message.Should().Contain("25 FITAB");

        inventoryRepository.Verify(repo => repo.AddItemAsync("user-1", InventoryItemType.Leitao, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExchangeFitabForLeitaoAsync_WithInsufficientFitab_ShouldFail()
    {
        var inventoryRepository = new Mock<IInventoryRepository>();
        inventoryRepository
            .Setup(repo => repo.GetItemAsync("user-1", InventoryItemType.Fitab, It.IsAny<CancellationToken>()))
            .ReturnsAsync(InventoryItem.Create("user-1", InventoryItemType.Fitab, 10));

        var service = new ShopService(inventoryRepository.Object, Mock.Of<ILogger<ShopService>>());

        var result = await service.ExchangeFitabForLeitaoAsync("user-1");

        result.Success.Should().BeFalse();
        result.NewFitabBalance.Should().Be(10);
        result.Message.Should().Contain("FITAB insuficiente");

        inventoryRepository.Verify(repo => repo.AddItemAsync(It.IsAny<string>(), It.IsAny<InventoryItemType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExchangeInstrumentPartsForLeitaoAsync_WithEnoughPartsAcrossTypes_ShouldSucceed()
    {
        var inventoryRepository = new Mock<IInventoryRepository>();
        var partItems = new List<InventoryItem>
        {
            InventoryItem.Create("user-1", InventoryItemType.GuitarraPart, 3)
        };

        inventoryRepository
            .Setup(repo => repo.GetItemsByTypesAsync("user-1", It.IsAny<IEnumerable<InventoryItemType>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(partItems);
        inventoryRepository
            .Setup(repo => repo.ConsumeItemAsync("user-1", InventoryItemType.GuitarraPart, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new ShopService(inventoryRepository.Object, Mock.Of<ILogger<ShopService>>());

        var result = await service.ExchangeInstrumentPartsForLeitaoAsync("user-1");

        result.Success.Should().BeTrue();
        result.NewInstrumentPartsBalance.Should().Be(2);
        result.Message.Should().Contain("peça");

        inventoryRepository.Verify(repo => repo.ConsumeItemAsync("user-1", InventoryItemType.GuitarraPart, 1, It.IsAny<CancellationToken>()), Times.Once);
        inventoryRepository.Verify(repo => repo.AddItemAsync("user-1", InventoryItemType.Leitao, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExchangeInstrumentPartsForLeitaoAsync_WithInsufficientParts_ShouldFail()
    {
        var inventoryRepository = new Mock<IInventoryRepository>();
        var partItems = new List<InventoryItem>();

        inventoryRepository
            .Setup(repo => repo.GetItemsByTypesAsync("user-1", It.IsAny<IEnumerable<InventoryItemType>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(partItems);

        var service = new ShopService(inventoryRepository.Object, Mock.Of<ILogger<ShopService>>());

        var result = await service.ExchangeInstrumentPartsForLeitaoAsync("user-1");

        result.Success.Should().BeFalse();
        result.NewInstrumentPartsBalance.Should().Be(0);
        result.Message.Should().Contain("Peças de instrumento insuficientes");

        inventoryRepository.Verify(repo => repo.ConsumeItemAsync(It.IsAny<string>(), It.IsAny<InventoryItemType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        inventoryRepository.Verify(repo => repo.AddItemAsync(It.IsAny<string>(), It.IsAny<InventoryItemType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
