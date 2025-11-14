# Admin Button Styles Guide

## Overview
Generic admin button styles for consistent edit/delete actions across all card components.

## Generic Admin Buttons

### Usage
Use these classes for admin edit/delete buttons across all card components (EventCard, BoardCard, LabelCard, RehearsalCard, SongCard, etc.):

```html
<!-- Edit button -->
<button class="btn btn-sm admin-btn-edit" @onclick="EditAction" title="Editar">
    <i class="bi bi-pencil"></i>
</button>

<!-- Delete button -->
<button class="btn btn-sm admin-btn-delete" @onclick="DeleteAction" title="Eliminar">
    <i class="bi bi-trash"></i>
</button>
```

### Styling
- **Edit button**: White background with black icon
- **Delete button**: Red background with white icon
- **Hover effect**: 
  - Button scales to 1.05x
  - Icon zooms to 1.15x
  - No background color change (prevents unwanted purple hover)
- **Consistent shadow**: Subtle shadow for depth

## Music-specific Buttons

For Album and Shop cards, use the music-specific button classes:

```html
<!-- Music edit button -->
<button class="btn btn-sm music-btn-edit" @onclick="EditAction" title="Editar">
    <i class="bi bi-pencil music-icon-edit"></i>
</button>

<!-- Music delete button -->
<button class="btn btn-sm music-btn-delete" @onclick="DeleteAction" title="Eliminar">
    <i class="bi bi-trash music-icon-delete"></i>
</button>
```

These have the same hover behavior as generic admin buttons.

## Migration

To update existing cards to use the new generic admin buttons:

**Before:**
```html
<button class="btn btn-sm btn-light" ...>
    <i class="bi bi-pencil"></i>
</button>
<button class="btn btn-sm btn-danger" ...>
    <i class="bi bi-trash"></i>
</button>
```

**After:**
```html
<button class="btn btn-sm admin-btn-edit" ...>
    <i class="bi bi-pencil"></i>
</button>
<button class="btn btn-sm admin-btn-delete" ...>
    <i class="bi bi-trash"></i>
</button>
```

## Benefits

1. **Consistency**: All cards have identical edit/delete button styling
2. **Better UX**: Icon zoom effect provides clear hover feedback
3. **No purple hover**: Prevents unwanted purple background on hover
4. **Maintainability**: Single source of truth for admin button styles
5. **Accessibility**: Clear visual distinction between edit (white) and delete (red)

## Files

- Generic admin buttons: `/src/RTUB.Web/wwwroot/css/3-components/buttons.css`
- Music-specific buttons: `/src/RTUB.Web/wwwroot/css/4-pages/music.css`
