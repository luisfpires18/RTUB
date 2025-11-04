using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the CrudModalManager component (generic CRUD modal manager)
/// </summary>
public class CrudModalManagerTests : TestContext
{
    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void CrudModalManager_DoesNotShowEditModal_Initially()
    {
        // Arrange & Act
        var cut = RenderComponent<CrudModalManager<TestEntity>>();

        // Assert
        cut.Markup.Should().NotContain("Criar Novo", "edit modal should not be shown initially");
        cut.Markup.Should().NotContain("Editar", "edit modal should not be shown initially");
    }

    [Fact]
    public void CrudModalManager_ShowsEditModal_WhenShowEditModalIsTrue()
    {
        // Arrange & Act
        var cut = RenderComponent<CrudModalManager<TestEntity>>(parameters => parameters
            .Add(p => p.ShowEditModal, true)
            .Add(p => p.IsCreateMode, false)
            .Add(p => p.EditingEntity, new TestEntity { Id = 1, Name = "Test" }));

        // Assert
        cut.Markup.Should().Contain("Editar", "edit modal should be shown with edit title");
    }

    [Fact]
    public void CrudModalManager_ShowsCreateTitle_WhenInCreateMode()
    {
        // Arrange & Act
        var cut = RenderComponent<CrudModalManager<TestEntity>>(parameters => parameters
            .Add(p => p.ShowEditModal, true)
            .Add(p => p.IsCreateMode, true));

        // Assert
        cut.Markup.Should().Contain("Criar Novo", "should show create title in create mode");
    }

    [Fact]
    public void CrudModalManager_ShowsEditTitle_WhenNotInCreateMode()
    {
        // Arrange & Act
        var cut = RenderComponent<CrudModalManager<TestEntity>>(parameters => parameters
            .Add(p => p.ShowEditModal, true)
            .Add(p => p.IsCreateMode, false));

        // Assert
        cut.Markup.Should().Contain("Editar", "should show edit title when not in create mode");
    }

    [Fact]
    public void CrudModalManager_ShowsCustomCreateTitle()
    {
        // Arrange & Act
        var cut = RenderComponent<CrudModalManager<TestEntity>>(parameters => parameters
            .Add(p => p.ShowEditModal, true)
            .Add(p => p.IsCreateMode, true)
            .Add(p => p.CreateTitle, "Add New Item"));

        // Assert
        cut.Markup.Should().Contain("Add New Item", "should show custom create title");
    }

    [Fact]
    public void CrudModalManager_ShowsCustomEditTitle()
    {
        // Arrange & Act
        var cut = RenderComponent<CrudModalManager<TestEntity>>(parameters => parameters
            .Add(p => p.ShowEditModal, true)
            .Add(p => p.IsCreateMode, false)
            .Add(p => p.EditTitle, "Update Item"));

        // Assert
        cut.Markup.Should().Contain("Update Item", "should show custom edit title");
    }

    [Fact]
    public void CrudModalManager_ShowsDefaultFooterButtons_WhenNoCustomFooter()
    {
        // Arrange & Act
        var cut = RenderComponent<CrudModalManager<TestEntity>>(parameters => parameters
            .Add(p => p.ShowEditModal, true));

        // Assert
        cut.Markup.Should().Contain("Cancelar", "should show cancel button");
        cut.Markup.Should().Contain("Guardar", "should show save button");
        cut.Markup.Should().Contain("btn-secondary", "cancel button should have secondary style");
        cut.Markup.Should().Contain("btn-success", "save button should have success style");
    }

    [Fact]
    public void CrudModalManager_DoesNotShowDeleteModal_Initially()
    {
        // Arrange & Act
        var cut = RenderComponent<CrudModalManager<TestEntity>>();

        // Assert
        cut.Markup.Should().NotContain("Confirmar Eliminação", "delete modal should not be shown initially");
    }

    [Fact]
    public void CrudModalManager_ShowsDeleteModal_WhenShowDeleteModalIsTrue()
    {
        // Arrange & Act
        var cut = RenderComponent<CrudModalManager<TestEntity>>(parameters => parameters
            .Add(p => p.ShowDeleteModal, true)
            .Add(p => p.DeletingEntity, new TestEntity { Id = 1, Name = "Test" }));

        // Assert
        cut.Markup.Should().Contain("Confirmar Eliminação", "delete modal should be shown");
    }

    [Fact]
    public void CrudModalManager_ShowsCustomDeleteTitle()
    {
        // Arrange & Act
        var cut = RenderComponent<CrudModalManager<TestEntity>>(parameters => parameters
            .Add(p => p.ShowDeleteModal, true)
            .Add(p => p.DeleteTitle, "Confirm Deletion"));

        // Assert
        cut.Markup.Should().Contain("Confirm Deletion", "should show custom delete title");
    }

    [Fact]
    public void CrudModalManager_ShowsDefaultDeleteMessage_WhenNoCustomContent()
    {
        // Arrange & Act
        var cut = RenderComponent<CrudModalManager<TestEntity>>(parameters => parameters
            .Add(p => p.ShowDeleteModal, true));

        // Assert
        cut.Markup.Should().Contain("Tem a certeza que quer eliminar este item?", "should show default delete message");
        cut.Markup.Should().Contain("Esta ação não pode ser revertida", "should show warning message");
    }

    [Fact]
    public void CrudModalManager_ShowsDeleteButtons()
    {
        // Arrange & Act
        var cut = RenderComponent<CrudModalManager<TestEntity>>(parameters => parameters
            .Add(p => p.ShowDeleteModal, true));

        // Assert
        cut.Markup.Should().Contain("Cancelar", "should show cancel button in delete modal");
        cut.Markup.Should().Contain("Eliminar", "should show delete button");
        cut.Markup.Should().Contain("btn-danger", "delete button should have danger style");
    }

    [Fact]
    public void CrudModalManager_InvokesOnSave_WhenSaveButtonClicked()
    {
        // Arrange
        bool callbackInvoked = false;
        var cut = RenderComponent<CrudModalManager<TestEntity>>(parameters => parameters
            .Add(p => p.ShowEditModal, true)
            .Add(p => p.OnSave, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var saveButton = cut.FindAll("button").First(b => b.InnerHtml.Contains("Guardar"));
        saveButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnSave callback should be invoked");
    }

    [Fact]
    public void CrudModalManager_InvokesOnDelete_WhenDeleteButtonClicked()
    {
        // Arrange
        bool callbackInvoked = false;
        var cut = RenderComponent<CrudModalManager<TestEntity>>(parameters => parameters
            .Add(p => p.ShowDeleteModal, true)
            .Add(p => p.OnDelete, EventCallback.Factory.Create(this, () => callbackInvoked = true)));

        // Act
        var deleteButton = cut.FindAll("button").First(b => b.InnerHtml.Contains("Eliminar") && b.ClassList.Contains("btn-danger"));
        deleteButton.Click();

        // Assert
        callbackInvoked.Should().BeTrue("OnDelete callback should be invoked");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CrudModalManager_ShowsCorrectTitle_BasedOnMode(bool isCreateMode)
    {
        // Arrange & Act
        var cut = RenderComponent<CrudModalManager<TestEntity>>(parameters => parameters
            .Add(p => p.ShowEditModal, true)
            .Add(p => p.IsCreateMode, isCreateMode));

        // Assert
        if (isCreateMode)
        {
            cut.Markup.Should().Contain("Criar Novo", "should show create title when in create mode");
        }
        else
        {
            cut.Markup.Should().Contain("Editar", "should show edit title when not in create mode");
        }
    }
}
