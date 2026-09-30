using System.ComponentModel.DataAnnotations;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components.Forms;

/// <summary>
/// FormField contract (UI refactor 036, docs/design/RTUB_UI_REFACTOR.md section 23.2): the label
/// names the control, help and error text describe it, validation messages come from the form's
/// EditContext and the required marker from [Required] - presentation only.
/// </summary>
public class FormFieldTests : BunitContext
{
    [Fact]
    public void Label_IsAssociatedWithTheControl()
    {
        var cut = Render<FormField>(p => p
            .Add(x => x.Label, "Localização")
            .Add(x => x.ChildContent, field => Input(field)));

        var input = cut.Find("input");
        cut.Find("label").GetAttribute("for").Should().Be(input.Id).And.NotBeNullOrEmpty();
    }

    [Fact]
    public void ExplicitId_IsKept()
    {
        var cut = Render<FormField>(p => p
            .Add(x => x.Label, "Nome")
            .Add(x => x.Id, "board-name")
            .Add(x => x.ChildContent, field => Input(field)));

        cut.Find("input").Id.Should().Be("board-name");
        cut.Find("label").GetAttribute("for").Should().Be("board-name");
    }

    [Fact]
    public void Help_DescribesTheControl()
    {
        var cut = Render<FormField>(p => p
            .Add(x => x.Label, "Tema")
            .Add(x => x.Help, "Aparece no cartão do ensaio.")
            .Add(x => x.ChildContent, field => Input(field)));

        var describedBy = cut.Find("input").GetAttribute("aria-describedby");
        describedBy.Should().NotBeNullOrEmpty();
        cut.Find($"#{describedBy}").TextContent.Should().Be("Aparece no cartão do ensaio.");
    }

    [Fact]
    public void WithoutHelpOrErrors_ControlHasNoDescription()
    {
        var cut = Render<FormField>(p => p
            .Add(x => x.Label, "Tema")
            .Add(x => x.ChildContent, field => Input(field)));

        cut.Find("input").HasAttribute("aria-describedby").Should().BeFalse();
    }

    [Fact]
    public void RequiredProperty_ShowsMarkerAndNamesIt_WithoutAddingNativeValidation()
    {
        var cut = Render<FormHost>(p => p.Add(x => x.Model, new SampleModel()));

        var label = cut.Find("label[for='location']");
        label.TextContent.Should().Contain("Localização").And.Contain("(obrigatório)");
        label.QuerySelector(".form-field__required")!.GetAttribute("aria-hidden").Should().Be("true");
        cut.Find("#location").HasAttribute("required").Should().BeFalse("FormField never adds validation");

        cut.Find("label[for='theme']").TextContent.Should().NotContain("obrigatório", "optional property");
    }

    [Fact]
    public void ExplicitRequired_OverridesTheModel()
    {
        var cut = Render<FormField>(p => p
            .Add(x => x.Label, "Motivo")
            .Add(x => x.Required, true)
            .Add(x => x.ChildContent, field => Input(field)));

        cut.Find("label").TextContent.Should().Contain("(obrigatório)");
    }

    [Fact]
    public void ValidationMessages_ShowUnderTheFieldAndDescribeIt()
    {
        var cut = Render<FormHost>(p => p.Add(x => x.Model, new SampleModel()));

        cut.FindAll(".form-field__error").Should().BeEmpty("no validation has run yet");

        cut.Find("form").Submit();

        var input = cut.Find("#location");
        var error = cut.Find("#location-error");
        error.TextContent.Should().Contain("A localização é obrigatória.");
        input.GetAttribute("aria-describedby").Should().Contain("location-error");
        input.GetAttribute("aria-invalid").Should().Be("true");
        cut.FindAll("#theme-error").Should().BeEmpty("only the invalid field shows an error");
    }

    [Fact]
    public void FixingTheValue_RemovesTheError()
    {
        var model = new SampleModel();
        var cut = Render<FormHost>(p => p.Add(x => x.Model, model));
        cut.Find("form").Submit();

        cut.Find("#location").Change("Sede");

        cut.FindAll("#location-error").Should().BeEmpty();
        cut.Find("#location").HasAttribute("aria-describedby").Should().BeFalse();
    }

    private static RenderFragment Input(FormFieldContext field) => builder =>
    {
        builder.OpenElement(0, "input");
        builder.AddMultipleAttributes(1, field.Attributes);
        builder.AddAttribute(2, "class", "form-control");
        builder.CloseElement();
    };

    public sealed class SampleModel
    {
        [Required(ErrorMessage = "A localização é obrigatória.")]
        public string? Location { get; set; }

        public string? Theme { get; set; }
    }

    /// <summary>An EditForm with two FormFields, as a page would write it.</summary>
    private sealed class FormHost : ComponentBase
    {
        [Parameter] public SampleModel Model { get; set; } = new();

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<EditForm>(0);
            builder.AddAttribute(1, nameof(EditForm.Model), Model);
            builder.AddAttribute(2, nameof(EditForm.ChildContent), (RenderFragment<EditContext>)(_ => form =>
            {
                form.OpenComponent<DataAnnotationsValidator>(0);
                form.CloseComponent();
                form.OpenRegion(1);
                AddField(form, "location", "Localização", () => Model.Location, v => Model.Location = v);
                form.CloseRegion();
                form.OpenRegion(2);
                AddField(form, "theme", "Tema", () => Model.Theme, v => Model.Theme = v);
                form.CloseRegion();
            }));
            builder.CloseComponent();
        }

        private void AddField(RenderTreeBuilder form, string id, string label,
            System.Linq.Expressions.Expression<Func<string?>> property, Action<string?> set)
        {
            form.OpenComponent<FormField>(0);
            form.AddAttribute(1, nameof(FormField.Label), label);
            form.AddAttribute(2, nameof(FormField.Id), id);
            form.AddAttribute(3, nameof(FormField.For), (System.Linq.Expressions.Expression<Func<object?>>)
                System.Linq.Expressions.Expression.Lambda<Func<object?>>(property.Body));
            form.AddAttribute(4, nameof(FormField.ChildContent), (RenderFragment<FormFieldContext>)(field => input =>
            {
                input.OpenComponent<InputText>(0);
                input.AddMultipleAttributes(1, field.Attributes);
                input.AddAttribute(2, nameof(InputText.Value), property.Compile()());
                input.AddAttribute(3, nameof(InputText.ValueChanged), EventCallback.Factory.Create<string?>(this, set));
                input.AddAttribute(4, nameof(InputText.ValueExpression), property);
                input.CloseComponent();
            }));
            form.CloseComponent();
        }
    }
}
