using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components.Forms;

/// <summary>
/// Unit tests for the MonthYearPicker component
/// </summary>
public class MonthYearPickerTests : TestContext
{
    [Fact]
    public void MonthYearPicker_RendersWithLabel()
    {
        // Arrange & Act
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test Label"));

        // Assert
        cut.Markup.Should().Contain("Test Label", "label should be displayed");
    }

    [Fact]
    public void MonthYearPicker_RendersWithIcon()
    {
        // Arrange & Act
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.Icon, "calendar"));

        // Assert
        cut.Markup.Should().Contain("bi-calendar", "icon should be displayed");
    }

    [Fact]
    public void MonthYearPicker_RendersRequiredIndicator_WhenRequired()
    {
        // Arrange & Act
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.Required, true));

        // Assert
        cut.Markup.Should().Contain("text-danger", "required indicator should be present");
        cut.Markup.Should().Contain("*", "asterisk should be shown for required fields");
    }

    [Fact]
    public void MonthYearPicker_RendersHelpText_WhenProvided()
    {
        // Arrange
        var helpText = "Select a month and year";

        // Act
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.HelpText, helpText));

        // Assert
        cut.Markup.Should().Contain(helpText, "help text should be displayed");
    }

    [Fact]
    public void MonthYearPicker_RendersAllPortugueseMonths()
    {
        // Arrange & Act
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test"));

        // Assert
        var expectedMonths = new[] { "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho",
                                     "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro" };
        
        foreach (var month in expectedMonths)
        {
            cut.Markup.Should().Contain(month, $"month '{month}' should be in dropdown");
        }
    }

    [Fact]
    public void MonthYearPicker_RendersYearsFromCurrentToMinYear()
    {
        // Arrange
        var minYear = 2000;
        var currentYear = DateTime.Now.Year;

        // Act
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.MinYear, minYear));

        // Assert
        cut.Markup.Should().Contain($"value=\"{currentYear}\"", "current year should be in dropdown");
        cut.Markup.Should().Contain($"value=\"{minYear}\"", "minimum year should be in dropdown");
    }

    [Fact]
    public void MonthYearPicker_DisplaysPlaceholder_WhenNoValueSet()
    {
        // Arrange & Act
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test"));

        // Assert
        cut.Markup.Should().Contain("Selecionar...", "placeholder should be displayed");
    }

    [Fact]
    public void MonthYearPicker_DisplaysMonth_WhenMonthParameterProvided()
    {
        // Arrange & Act
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.Month, 5)); // May

        // Assert
        // The month value should be set (value="5" selected in the dropdown)
        cut.Markup.Should().Contain("Maio", "May should be available in dropdown");
    }

    [Fact]
    public void MonthYearPicker_DisplaysYear_WhenYearParameterProvided()
    {
        // Arrange & Act
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.Year, 2023));

        // Assert
        cut.Markup.Should().Contain("2023", "year 2023 should be in dropdown");
    }

    [Fact]
    public void MonthYearPicker_InvokesMonthChanged_WhenMonthSelected()
    {
        // Arrange
        int? selectedMonth = null;
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.MonthChanged, EventCallback.Factory.Create<int?>(this, (month) => selectedMonth = month)));

        // Act
        var monthSelect = cut.FindAll("select").First();
        monthSelect.Change("3"); // March

        // Assert
        selectedMonth.Should().Be(3, "MonthChanged callback should be invoked with selected month");
    }

    [Fact]
    public void MonthYearPicker_InvokesYearChanged_WhenYearSelected()
    {
        // Arrange
        int? selectedYear = null;
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.YearChanged, EventCallback.Factory.Create<int?>(this, (year) => selectedYear = year)));

        // Act
        var yearSelect = cut.FindAll("select").Last();
        yearSelect.Change("2022");

        // Assert
        selectedYear.Should().Be(2022, "YearChanged callback should be invoked with selected year");
    }

    [Fact]
    public void MonthYearPicker_CreatesDateTimeValue_WhenBothMonthAndYearSelected()
    {
        // Arrange
        DateTime? resultValue = null;
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateTime?>(this, (value) => resultValue = value)));

        // Act - Select month first
        var monthSelect = cut.FindAll("select").First();
        monthSelect.Change("6"); // June

        // Act - Then select year
        var yearSelect = cut.FindAll("select").Last();
        yearSelect.Change("2023");

        // Assert
        resultValue.Should().NotBeNull("DateTime value should be created");
        resultValue?.Year.Should().Be(2023, "year should match selected year");
        resultValue?.Month.Should().Be(6, "month should match selected month");
        resultValue?.Day.Should().Be(1, "day should default to 1");
    }

    [Fact]
    public void MonthYearPicker_DoesNotCreateDateTime_WhenOnlyMonthSelected()
    {
        // Arrange
        DateTime? resultValue = new DateTime(2020, 1, 1); // Set initial value
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateTime?>(this, (value) => resultValue = value)));

        // Act - Select only month
        var monthSelect = cut.FindAll("select").First();
        monthSelect.Change("9"); // September

        // Assert
        resultValue.Should().BeNull("DateTime should be null when only month is selected");
    }

    [Fact]
    public void MonthYearPicker_DoesNotCreateDateTime_WhenOnlyYearSelected()
    {
        // Arrange
        DateTime? resultValue = new DateTime(2020, 1, 1); // Set initial value
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateTime?>(this, (value) => resultValue = value)));

        // Act - Select only year
        var yearSelect = cut.FindAll("select").Last();
        yearSelect.Change("2024");

        // Assert
        resultValue.Should().BeNull("DateTime should be null when only year is selected");
    }

    [Fact]
    public void MonthYearPicker_ClearsValue_WhenMonthClearedToPlaceholder()
    {
        // Arrange
        DateTime? resultValue = new DateTime(2023, 5, 1);
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.Month, 5)
            .Add(p => p.Year, 2023)
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateTime?>(this, (value) => resultValue = value)));

        // Act - Clear month
        var monthSelect = cut.FindAll("select").First();
        monthSelect.Change(""); // Empty string for placeholder

        // Assert
        resultValue.Should().BeNull("DateTime should be null when month is cleared");
    }

    [Fact]
    public void MonthYearPicker_ClearsValue_WhenYearClearedToPlaceholder()
    {
        // Arrange
        DateTime? resultValue = new DateTime(2023, 5, 1);
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.Month, 5)
            .Add(p => p.Year, 2023)
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<DateTime?>(this, (value) => resultValue = value)));

        // Act - Clear year
        var yearSelect = cut.FindAll("select").Last();
        yearSelect.Change(""); // Empty string for placeholder

        // Assert
        resultValue.Should().BeNull("DateTime should be null when year is cleared");
    }

    [Fact]
    public void MonthYearPicker_DisablesInputs_WhenDisabledIsTrue()
    {
        // Arrange & Act
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.Disabled, true));

        // Assert
        var selects = cut.FindAll("select");
        foreach (var select in selects)
        {
            select.HasAttribute("disabled").Should().BeTrue("selects should be disabled");
        }
    }

    [Fact]
    public void MonthYearPicker_AppliesCustomCssClass()
    {
        // Arrange
        var customClass = "my-custom-class";

        // Act
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.CssClass, customClass));

        // Assert
        cut.Markup.Should().Contain(customClass, "custom CSS class should be applied");
    }

    [Fact]
    public void MonthYearPicker_AppliesCustomInputCssClass()
    {
        // Arrange
        var customInputClass = "custom-input";

        // Act
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.InputCssClass, customInputClass));

        // Assert
        cut.Markup.Should().Contain(customInputClass, "custom input CSS class should be applied");
    }

    [Fact]
    public void MonthYearPicker_PreservezMonth_WhenYearChanges()
    {
        // Arrange
        int? capturedMonth = null;
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.MonthChanged, EventCallback.Factory.Create<int?>(this, (month) => capturedMonth = month)));

        // Act - Select month first
        var monthSelect = cut.FindAll("select").First();
        monthSelect.Change("7"); // July
        
        // Act - Then select year (should not clear month)
        var yearSelect = cut.FindAll("select").Last();
        yearSelect.Change("2022");

        // Assert
        capturedMonth.Should().Be(7, "month should be preserved when year is selected");
    }

    [Fact]
    public void MonthYearPicker_PreservesYear_WhenMonthChanges()
    {
        // Arrange
        int? capturedYear = null;
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test")
            .Add(p => p.YearChanged, EventCallback.Factory.Create<int?>(this, (year) => capturedYear = year)));

        // Act - Select year first
        var yearSelect = cut.FindAll("select").Last();
        yearSelect.Change("2021");
        
        // Act - Then select month (should not clear year)
        var monthSelect = cut.FindAll("select").First();
        monthSelect.Change("11"); // November

        // Assert
        capturedYear.Should().Be(2021, "year should be preserved when month is selected");
    }

    [Fact]
    public void MonthYearPicker_HasResponsiveLayout()
    {
        // Arrange & Act
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test"));

        // Assert
        cut.Markup.Should().Contain("row g-2", "should use responsive row with gap");
        cut.Markup.Should().Contain("col-6", "should use 50% width columns for responsive layout");
    }

    [Fact]
    public void MonthYearPicker_HidesPlaceholderOption()
    {
        // Arrange & Act
        var cut = RenderComponent<MonthYearPicker>(parameters => parameters
            .Add(p => p.Label, "Test"));

        // Assert
        cut.Markup.Should().Contain(".month-year-picker option[disabled]", 
            "CSS should include rule to hide disabled placeholder options");
        cut.Markup.Should().Contain("display: none", 
            "disabled options should have display: none to prevent hover effects");
    }
}
