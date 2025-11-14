using Xunit;
using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Web.Tests.Pages;

/// <summary>
/// Unit tests for Hierarchy page ordering logic
/// Testing that children (afilhados) are ordered by YearCaloiro when available
/// </summary>
public class HierarchyOrderingTests
{
    [Fact]
    public void HierarchyChildren_ShouldBeOrdered_ByYearCaloiro_ThenFirstName()
    {
        // Arrange - Create test users with different YearCaloiro values
        var children = new List<ApplicationUser>
        {
            new ApplicationUser 
            { 
                Id = "1", 
                FirstName = "Carlos", 
                LastName = "Silva", 
                YearCaloiro = 2018 
            },
            new ApplicationUser 
            { 
                Id = "2", 
                FirstName = "Ana", 
                LastName = "Costa", 
                YearCaloiro = 2016 
            },
            new ApplicationUser 
            { 
                Id = "3", 
                FirstName = "Bruno", 
                LastName = "Alves", 
                YearCaloiro = 2017 
            },
            new ApplicationUser 
            { 
                Id = "4", 
                FirstName = "Diana", 
                LastName = "Martins", 
                YearCaloiro = null  // No year - should go last
            }
        };

        // Act - Apply the same ordering logic as in Hierarchy.razor
        var ordered = children
            .OrderBy(a => a.YearCaloiro.HasValue ? a.YearCaloiro.Value : int.MaxValue)
            .ThenBy(a => a.FirstName)
            .ThenBy(a => a.LastName)
            .ToList();

        // Assert - Verify correct order
        ordered[0].FirstName.Should().Be("Ana", "2016 should come first");
        ordered[0].YearCaloiro.Should().Be(2016);
        
        ordered[1].FirstName.Should().Be("Bruno", "2017 should come second");
        ordered[1].YearCaloiro.Should().Be(2017);
        
        ordered[2].FirstName.Should().Be("Carlos", "2018 should come third");
        ordered[2].YearCaloiro.Should().Be(2018);
        
        ordered[3].FirstName.Should().Be("Diana", "null YearCaloiro should come last");
        ordered[3].YearCaloiro.Should().BeNull();
    }

    [Fact]
    public void HierarchyChildren_WithSameYearCaloiro_ShouldBeOrdered_ByFirstName()
    {
        // Arrange - Create test users with same YearCaloiro
        var children = new List<ApplicationUser>
        {
            new ApplicationUser 
            { 
                Id = "1", 
                FirstName = "Zara", 
                LastName = "Silva", 
                YearCaloiro = 2017 
            },
            new ApplicationUser 
            { 
                Id = "2", 
                FirstName = "Ana", 
                LastName = "Costa", 
                YearCaloiro = 2017 
            },
            new ApplicationUser 
            { 
                Id = "3", 
                FirstName = "Bruno", 
                LastName = "Alves", 
                YearCaloiro = 2017 
            }
        };

        // Act - Apply the ordering logic
        var ordered = children
            .OrderBy(a => a.YearCaloiro.HasValue ? a.YearCaloiro.Value : int.MaxValue)
            .ThenBy(a => a.FirstName)
            .ThenBy(a => a.LastName)
            .ToList();

        // Assert - Verify alphabetical order by FirstName
        ordered[0].FirstName.Should().Be("Ana");
        ordered[1].FirstName.Should().Be("Bruno");
        ordered[2].FirstName.Should().Be("Zara");
    }

    [Fact]
    public void HierarchyChildren_AllWithoutYearCaloiro_ShouldBeOrdered_ByFirstName()
    {
        // Arrange - Create test users without YearCaloiro
        var children = new List<ApplicationUser>
        {
            new ApplicationUser 
            { 
                Id = "1", 
                FirstName = "Zara", 
                LastName = "Silva", 
                YearCaloiro = null 
            },
            new ApplicationUser 
            { 
                Id = "2", 
                FirstName = "Ana", 
                LastName = "Costa", 
                YearCaloiro = null 
            }
        };

        // Act - Apply the ordering logic
        var ordered = children
            .OrderBy(a => a.YearCaloiro.HasValue ? a.YearCaloiro.Value : int.MaxValue)
            .ThenBy(a => a.FirstName)
            .ThenBy(a => a.LastName)
            .ToList();

        // Assert - Verify alphabetical order
        ordered[0].FirstName.Should().Be("Ana");
        ordered[1].FirstName.Should().Be("Zara");
    }
}
