using Microsoft.AspNetCore.Components;
using RTUB.Application.Helpers;
using RTUB.Shared.Base;

namespace RTUB.Shared;

/// <summary>
/// Base class for pages that manage modals and CRUD operations.
/// Extends CrudTablePageBase with modal state management using MultiModalState.
/// </summary>
/// <typeparam name="TEntity">The entity type to manage</typeparam>
public abstract class ManagedModalPageBase<TEntity> : CrudTablePageBase<TEntity>
{
    /// <summary>
    /// Modal state manager for handling multiple modals
    /// </summary>
    protected MultiModalState<string> Modals { get; } = new();

    /// <summary>
    /// Entity currently being edited (null if creating new)
    /// </summary>
    protected TEntity? EditingEntity { get; set; }

    /// <summary>
    /// Entity currently being deleted
    /// </summary>
    protected TEntity? DeletingEntity { get; set; }

    /// <summary>
    /// Entity currently being viewed
    /// </summary>
    protected TEntity? ViewingEntity { get; set; }

    /// <summary>
    /// Whether we're in create mode (true) or edit mode (false)
    /// </summary>
    protected bool IsCreateMode { get; set; }

    /// <summary>
    /// Whether a save operation is in progress
    /// </summary>
    protected bool IsSaving { get; set; }

    /// <summary>
    /// Opens the create modal
    /// </summary>
    protected virtual void OpenCreateModal()
    {
        IsCreateMode = true;
        EditingEntity = CreateNewEntity();
        Modals.Open("edit");
    }

    /// <summary>
    /// Opens the edit modal for the given entity
    /// </summary>
    protected virtual void OpenEditModal(TEntity entity)
    {
        IsCreateMode = false;
        EditingEntity = CloneForEdit(entity);
        Modals.Open("edit");
    }

    /// <summary>
    /// Opens the delete confirmation modal
    /// </summary>
    protected virtual void OpenDeleteModal(TEntity entity)
    {
        DeletingEntity = entity;
        Modals.Open("delete");
    }

    /// <summary>
    /// Opens the view details modal
    /// </summary>
    protected virtual void OpenViewModal(TEntity entity)
    {
        ViewingEntity = entity;
        Modals.Open("view");
    }

    /// <summary>
    /// Closes all modals and clears entity references
    /// </summary>
    protected virtual void CloseAllModals()
    {
        Modals.CloseAll();
        EditingEntity = default;
        DeletingEntity = default;
        ViewingEntity = default;
        IsSaving = false;
    }

    /// <summary>
    /// Creates a new entity instance for create mode.
    /// Must be implemented by derived classes.
    /// </summary>
    protected abstract TEntity CreateNewEntity();

    /// <summary>
    /// Clones an entity for editing (to avoid modifying the original until saved).
    /// Must be implemented by derived classes.
    /// </summary>
    protected abstract TEntity CloneForEdit(TEntity entity);
}
