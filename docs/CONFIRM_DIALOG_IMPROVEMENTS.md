# ConfirmDialog Component Improvements

## Overview
Updated ConfirmDialog component to match the enhanced styling of ConfirmDialog2, providing consistent red header styling for dangerous actions across all confirmation dialogs.

## Changes Made

### Enhanced ConfirmDialog Component
Updated `/src/RTUB.Shared/Components/Modals/ConfirmDialog.razor` with improved styling:

1. **Red Header for Danger Actions**
   - When `ConfirmButtonClass="btn-danger"`, the modal header displays with red styling
   - Header background: `bg-danger bg-opacity-10`
   - Title text: `text-danger`
   - Warning icon: `bi-exclamation-triangle`

2. **Improved Visual Hierarchy**
   - Info icon with message display
   - Warning messages highlighted with exclamation icon
   - Icons in footer buttons for better UX

3. **Consistent Styling**
   - Matches ConfirmDialog2 appearance
   - Maintains all existing parameters and functionality
   - Backward compatible with existing usage

## Visual Differences

### Before
- Basic modal header (no special styling for danger actions)
- Simple text message
- Plain buttons

### After
- **Danger Actions**: Red header background and title
- **Warning icon** in header for dangerous operations
- **Info icon** alongside message
- **Warning messages** highlighted in red with icon
- **Icons in buttons** for better visual feedback

## Affected Pages

Pages using ConfirmDialog that now have enhanced styling:
- **UserRoles.razor** - Role change confirmations
- **EventDiscussion.razor** - Discussion confirmations
- **Shop.razor** - Shop confirmations
- **Requests.razor** - Request confirmations

## Usage

No changes required in existing code. The component automatically applies red header styling when using `btn-danger`:

```razor
<!-- Danger confirmation (red header) -->
<ConfirmDialog Show="@showDeleteModal"
               Title="Confirmar Eliminação"
               Message="Tem a certeza?"
               ConfirmButtonClass="btn-danger"
               OnConfirm="HandleDelete" />

<!-- Standard confirmation (normal header) -->
<ConfirmDialog Show="@showConfirmModal"
               Title="Confirmar Ação"
               Message="Deseja continuar?"
               ConfirmButtonClass="btn-primary"
               OnConfirm="HandleConfirm" />
```

## Benefits

1. **Visual Consistency**: All confirm dialogs now have the same enhanced styling
2. **Better UX**: Red headers immediately communicate danger/delete actions
3. **Unified Experience**: No need to migrate from ConfirmDialog to ConfirmDialog2
4. **Backward Compatible**: Existing code works without changes

## Future Considerations

- ConfirmDialog2 can potentially be deprecated since ConfirmDialog now has all the same features
- Both components currently have identical functionality and appearance
- Consider consolidating to a single component in future refactoring
