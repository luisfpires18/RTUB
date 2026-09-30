# Frontend Best Practices

This document outlines frontend development best practices for the RTUB project, focusing on Blazor/Razor components, CSS, and PWA development.

## Table of Contents
- [Component Design](#component-design)
- [Layout & Styling](#layout--styling)
- [State Management](#state-management)
- [Performance](#performance)
- [Accessibility](#accessibility)
- [PWA Considerations](#pwa-considerations)

## Component Design

### Reusable Components

✅ **Always:**
- Check `src/RTUB.Shared/Components` for existing components before creating new ones
- Create components in `RTUB.Shared` if used in multiple places
- Use component parameters for customization
- Follow single responsibility principle

```razor
@* ✅ Good: Reusable component *@
<MemberCard AvatarUrl="@member.ProfilePictureSrc"
            Nickname="@member.Nickname"
            FullName="@($"{member.FirstName} {member.LastName}")" />
```

❌ **Never:**
- Copy-paste component code
- Create components with inline styles
- Put business logic in components (use services)

### Component Structure

```razor
@page "/example"
@rendermode InteractiveServer
@using Microsoft.AspNetCore.Authorization
@attribute [Authorize]
@inject IMyService MyService

<PageTitle>Example Page</PageTitle>

<div class="container">
    @* Content *@
</div>

@code {
    private List<Item> items = new();
    
    protected override async Task OnInitializedAsync()
    {
        items = await MyService.GetItemsAsync();
    }
}
```

### Modal Components

✅ **Always:**
- Use `Modal` component from `RTUB.Shared`
- Use `ConfirmDialog` for confirmations
- Handle loading states
- Close modals properly

```razor
<Modal Show="@showModal"
       ShowChanged="@((bool show) => showModal = show)"
       Title="Example Modal"
       Size="Modal.ModalSize.Large">
    <BodyContent>
        @* Modal content *@
    </BodyContent>
    <FooterContent>
        <button class="btn btn-primary" @onclick="Save">Save</button>
        <button class="btn btn-secondary" @onclick="Close">Cancel</button>
    </FooterContent>
</Modal>
```

## Layout & Styling

### CSS Organization

✅ **Always:**
- Use scoped CSS files (`Component.razor.css`)
- Use CSS variables for theming
- Follow mobile-first responsive design
- Use Bootstrap utility classes when appropriate

```css
/* Component.razor.css */
.my-component {
    padding: 1rem;
    background-color: var(--rtub-primary);
}

@media (max-width: 768px) {
    .my-component {
        padding: 0.5rem;
    }
}
```

❌ **Never:**
- Use inline styles (`style="..."`)
- Create global CSS without scoping
- Hardcode colors (use CSS variables)

### Responsive Design

✅ **Always:**
- Test on mobile viewports (320px, 375px, 768px)
- Use Bootstrap grid system
- Hide/show elements with `d-none d-md-block`
- Ensure touch targets are at least 44x44px

```razor
<div class="d-flex flex-column flex-md-row align-items-md-center gap-2">
    <div class="w-100">
        <h1>Title</h1>
    </div>
    <div class="d-flex gap-2">
        <button class="btn btn-primary">Action</button>
    </div>
</div>
```

## State Management

### Component State

✅ **Always:**
- Use private fields for component state
- Use `StateHasChanged()` when needed
- Avoid unnecessary re-renders
- Use `@key` for list items

```razor
@foreach (var item in items)
{
    <ItemCard @key="item.Id" Item="@item" />
}
```

### Loading States

✅ **Always:**
- Show loading indicators during async operations
- Disable buttons during operations
- Use `LoadableContent` component when available

```razor
@if (isLoading)
{
    <div class="text-center p-4">
        <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">Loading...</span>
        </div>
    </div>
}
else
{
    @* Content *@
}
```

## Performance

### Optimization

✅ **Always:**
- Use `@rendermode InteractiveServer` for server-side rendering
- Minimize component re-renders
- Use `ShouldRender()` override when appropriate
- Lazy load heavy components

### Asset Loading

✅ **Always:**
- Use `VersionedAsset` component for static assets
- Preload critical resources
- Use CDN for external libraries when appropriate

```razor
<VersionedAsset Path="/css/site.css" Type="VersionedAsset.AssetType.Css" />
```

## Accessibility

### ARIA Attributes

✅ **Always:**
- Add `aria-label` to icon buttons
- Use semantic HTML elements
- Include `alt` text for images
- Use `role` attributes when needed

```razor
<button class="btn btn-primary" aria-label="Save changes">
    <i class="bi bi-save"></i>
</button>
```

### Keyboard Navigation

✅ **Always:**
- Ensure all interactive elements are keyboard accessible
- Use proper focus management
- Provide skip links for navigation

## PWA Considerations

### Mobile Optimization

✅ **Always:**
- Use viewport meta tag
- Test on actual mobile devices
- Optimize images for mobile
- Use touch-friendly UI elements

```razor
@* In App.razor *@
<meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=5.0, user-scalable=yes" />
```

### Offline Support

✅ **Always:**
- Handle offline scenarios gracefully
- Show appropriate error messages
- Cache critical assets in service worker

## Code Quality

### Code Review Checklist

- [ ] Uses reusable components from RTUB.Shared
- [ ] Follows responsive design principles
- [ ] Has proper loading states
- [ ] Includes accessibility attributes
- [ ] Uses scoped CSS
- [ ] Handles errors appropriately
- [ ] Follows naming conventions
- [ ] Mobile-friendly

## References

- [Blazor Documentation](https://docs.microsoft.com/en-us/aspnet/core/blazor/)
- [Bootstrap Documentation](https://getbootstrap.com/docs/5.3/)
- [Web Accessibility Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)