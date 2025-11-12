using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Attributes;

/// <summary>
/// Validates that a date property is greater than or equal to another date property
/// </summary>
public class DateGreaterThanAttribute : ValidationAttribute
{
    private readonly string _comparisonProperty;

    public DateGreaterThanAttribute(string comparisonProperty)
    {
        _comparisonProperty = comparisonProperty;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // If value is null, it's valid (use [Required] for null checks)
        if (value == null)
            return ValidationResult.Success;

        var currentValue = (DateTime)value;

        var property = validationContext.ObjectType.GetProperty(_comparisonProperty);
        if (property == null)
            throw new ArgumentException($"Propriedade {_comparisonProperty} não encontrada");

        var comparisonValue = property.GetValue(validationContext.ObjectInstance);
        if (comparisonValue == null)
            return ValidationResult.Success;

        var comparisonDate = (DateTime)comparisonValue;

        if (currentValue < comparisonDate)
        {
            return new ValidationResult(
                ErrorMessage ?? $"A data de fim deve ser maior ou igual a {_comparisonProperty}",
                new[] { validationContext.MemberName ?? string.Empty });
        }

        return ValidationResult.Success;
    }
}