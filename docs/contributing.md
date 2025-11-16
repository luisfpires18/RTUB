# Contributing Guide

This guide provides guidelines for developers and agents working on RTUB. Follow these conventions to maintain code quality, consistency, and avoid breaking existing functionality.

## Table of Contents

- [Development Environment Setup](#development-environment-setup)
- [Coding Style](#coding-style)
- [Adding New Components to RTUB.Shared](#adding-new-components-to-rtubshared)
- [Adding New Razor Pages](#adding-new-razor-pages)
- [Layout and CSS Rules](#layout-and-css-rules)
- [Adding New Tests](#adding-new-tests)
- [Documentation Requirements](#documentation-requirements)
- [Git Workflow](#git-workflow)
- [Common Tasks](#common-tasks)

---

## Development Environment Setup

### Prerequisites
- **.NET 8 SDK** or later
- **IDE:** Visual Studio 2022, VS Code, or Rider
- **Git**
- **Optional:** Node.js (for front-end tooling, if needed)

### Getting Started
1. Clone the repository:
   ```bash
   git clone https://github.com/luisfpires18/RTUB.git
   cd RTUB
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Run database migrations (automatic on first run):
   ```bash
   cd src/RTUB.Web
   dotnet run
   ```

4. Access the app at `https://localhost:5001` or `http://localhost:5000`

### Configuration
- Copy `appsettings.Development.json.example` to `appsettings.Development.json`
- Add your local credentials (SMTP, admin user, IDrive/S3)
- **Never commit sensitive data** (the file is git-ignored)

---

## Coding Style

Follow standard C# and Blazor conventions:

### C# Conventions
- **PascalCase** for public members, classes, methods, properties
  ```csharp
  public class EventService { }
  public string EventName { get; set; }
  public void CreateEvent() { }
  ```

- **camelCase** for private fields, local variables, parameters
  ```csharp
  private int eventCount;
  public void ProcessEvent(int eventId) { }
  ```

- **Async suffix** for asynchronous methods
  ```csharp
  public async Task<Event> GetEventAsync(int id) { }
  ```

- **Use `var` judiciously** - prefer explicit types for clarity unless type is obvious
  ```csharp
  var events = await _eventService.GetAllEventsAsync(); // OK
  Event myEvent = await _eventService.GetEventByIdAsync(1); // Also OK
  ```

### Razor Component Conventions
- **PascalCase** for component parameters
  ```razor
  [Parameter] public string Title { get; set; }
  [Parameter] public EventCallback OnClick { get; set; }
  ```

- **RenderFragment** for content parameters
  ```razor
  [Parameter] public RenderFragment? ChildContent { get; set; }
  [Parameter] public RenderFragment? HeaderContent { get; set; }
  ```

- **EventCallback** for parent-child communication
  ```razor
  [Parameter] public EventCallback<int> OnItemSelected { get; set; }
  
  // Usage:
  await OnItemSelected.InvokeAsync(selectedId);
  ```

### Naming Conventions
- **Components:** `ComponentName.razor` (PascalCase, descriptive)
- **Pages:** `PageName.razor` (PascalCase, often matches route)
- **CSS files:** `ComponentName.razor.css` (page-scoped CSS)
- **Services:** `IServiceName` (interface), `ServiceName` (implementation)
- **Entities:** `EntityName` (singular, PascalCase)

### Code Organization
- **One class per file** (exceptions: nested classes, small helpers)
- **Group using statements** at the top
- **Use `#region`** sparingly - prefer smaller classes over regions
- **Methods:** Keep focused and short (< 50 lines ideally)

### Comments and Documentation
- **XML comments** for public APIs:
  ```csharp
  /// <summary>
  /// Gets an event by its unique identifier.
  /// </summary>
  /// <param name="id">The event ID.</param>
  /// <returns>The event, or null if not found.</returns>
  public async Task<Event?> GetEventByIdAsync(int id)
  ```

- **Inline comments** for complex logic only:
  ```csharp
  // Calculate fiscal year (September to August)
  var fiscalYearStart = currentMonth >= 9 ? currentYear : currentYear - 1;
  ```

- **Avoid obvious comments:**
  ```csharp
  // BAD: Increment counter
  counter++;
  
  // GOOD: Track number of failed login attempts for rate limiting
  failedAttempts++;
  ```

---

## Adding New Components to RTUB.Shared

### Step 1: Choose the Right Folder
Place components in the appropriate subfolder:
- **Badges:** Status/category indicators
- **Cards:** Entity display components
- **Common:** General utilities (EmptyState, ErrorDisplay, LoadingSpinner)
- **Forms:** Input components
- **Modals:** Dialog components
- **Tables:** Table-related components
- **UI:** Interactive UI elements
- **Profile/Ranking/Discussion/Uploads:** Domain-specific components

### Step 2: Create the Component File
```bash
# Example: Create a new card component
touch src/RTUB.Shared/Components/Cards/MyNewCard.razor
```

### Step 3: Define the Component
```razor
@namespace RTUB.Shared
@*
    MyNewCard Component
    Purpose: Display [entity] with [features]
*@

<div class="my-new-card">
    <h3>@Title</h3>
    <p>@Description</p>
    <button class="btn btn-primary" @onclick="OnClick">
        @ButtonText
    </button>
</div>

@code {
    /// <summary>
    /// Card title
    /// </summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Card description
    /// </summary>
    [Parameter]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Button text
    /// </summary>
    [Parameter]
    public string ButtonText { get; set; } = "Click me";

    /// <summary>
    /// Click event callback
    /// </summary>
    [Parameter]
    public EventCallback OnClick { get; set; }
}
```

### Step 4: Add Page-Scoped CSS (Optional)
```bash
touch src/RTUB.Shared/Components/Cards/MyNewCard.razor.css
```

```css
/* MyNewCard.razor.css */
.my-new-card {
    border: 1px solid var(--bs-border-color);
    border-radius: 0.5rem;
    padding: 1rem;
    margin-bottom: 1rem;
    transition: box-shadow 0.2s;
}

.my-new-card:hover {
    box-shadow: 0 0.5rem 1rem rgba(0, 0, 0, 0.15);
}
```

**CSS Guidelines:**
- Use **CSS variables** for colors (e.g., `var(--bs-primary)`, `var(--bs-border-color)`)
- Prefer **rem** for spacing, **em** for font sizes
- Avoid hardcoded pixel values
- Keep specificity low (avoid deeply nested selectors)

### Step 5: Use the Component
```razor
@* In a page *@
@using RTUB.Shared

<MyNewCard Title="My Title" 
           Description="My description" 
           ButtonText="Click here" 
           OnClick="HandleClick" />
```

### Step 6: Document the Component
Add the component to `/docs/components.md`:
```markdown
### MyNewCard
**Purpose:** Display [entity] with [features].

**Parameters:**
- `Title` (string) - Card title
- `Description` (string) - Card description
- `ButtonText` (string) - Button label
- `OnClick` (EventCallback) - Click handler

**Usage:** [Specify pages where used]
```

### Step 7: Add Tests (Optional but Recommended)
```bash
# Create test file
touch tests/RTUB.Shared.Tests/Components/Cards/MyNewCardTests.cs
```

```csharp
using Bunit;
using Xunit;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components.Cards;

public class MyNewCardTests : TestContext
{
    [Fact]
    public void MyNewCard_RendersTitle()
    {
        // Arrange & Act
        var cut = RenderComponent<MyNewCard>(parameters => parameters
            .Add(p => p.Title, "Test Title"));

        // Assert
        cut.Find("h3").TextContent.Should().Be("Test Title");
    }

    [Fact]
    public async Task MyNewCard_ClickInvokesCallback()
    {
        // Arrange
        var clicked = false;
        var cut = RenderComponent<MyNewCard>(parameters => parameters
            .Add(p => p.OnClick, () => clicked = true));

        // Act
        await cut.Find("button").ClickAsync(new MouseEventArgs());

        // Assert
        clicked.Should().BeTrue();
    }
}
```

---

## Adding New Razor Pages

### Step 1: Choose the Right Folder
- **Public:** `/src/RTUB.Web/Pages/Public/` (no auth required)
- **Identity:** `/src/RTUB.Web/Pages/Identity/` (login, registration)
- **Member:** `/src/RTUB.Web/Pages/Member/` (authenticated users)
- **Music:** `/src/RTUB.Web/Pages/Music/` (album/song pages)
- **Owner:** `/src/RTUB.Web/Pages/Owner/` (owner-only)

### Step 2: Create the Page File
```bash
touch src/RTUB.Web/Pages/Member/MyNewPage.razor
```

### Step 3: Define the Page
```razor
@page "/my-new-page"
@rendermode InteractiveServer
@attribute [Authorize(Roles = "Admin,Owner")]
@using Microsoft.AspNetCore.Authorization
@using Microsoft.AspNetCore.Components.Authorization
@using RTUB.Application.Interfaces
@using RTUB.Shared
@inject IMyService MyService
@inject AuthenticationStateProvider AuthenticationStateProvider

<PageTitle>My New Page</PageTitle>

<h1 class="mb-2"><i class="bi bi-star"></i> My New Page</h1>
<p class="lead mb-4 text-light-theme">Short description of the page.</p>

<!-- Header Controls -->
<div class="mb-3">
    <AuthorizeView Roles="Admin">
        <Authorized>
            <button class="btn btn-success" @onclick="OpenCreateModal">
                <i class="bi bi-plus-lg"></i> Add New
            </button>
        </Authorized>
    </AuthorizeView>
</div>

<!-- Content -->
@if (items == null)
{
    <LoadingSpinner />
}
else if (!items.Any())
{
    <EmptyState Title="No items yet" Icon="bi-info-circle" />
}
else
{
    @foreach (var item in items)
    {
        <MyNewCard Title="@item.Title" 
                   Description="@item.Description" 
                   OnClick="() => ViewDetails(item.Id)" />
    }
}

<!-- Modals -->
<CrudModalManager Show="showCreateModal" 
                  Title="Add New Item"
                  OnSave="SaveItem"
                  OnCancel="CloseCreateModal">
    <EntityContent>
        <FormTextField Label="Title" @bind-Value="newItem.Title" Required="true" />
        <FormTextArea Label="Description" @bind-Value="newItem.Description" />
    </EntityContent>
</CrudModalManager>

@code {
    private List<MyEntity>? items;
    private bool showCreateModal = false;
    private MyEntity newItem = new();
    private bool isUserAdmin = false;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        isUserAdmin = user.IsInRole("Admin") || user.IsInRole("Owner");

        await LoadItemsAsync();
    }

    private async Task LoadItemsAsync()
    {
        items = await MyService.GetAllItemsAsync();
    }

    private void OpenCreateModal()
    {
        newItem = new MyEntity();
        showCreateModal = true;
    }

    private void CloseCreateModal()
    {
        showCreateModal = false;
    }

    private async Task SaveItem()
    {
        await MyService.CreateItemAsync(newItem);
        await LoadItemsAsync();
        CloseCreateModal();
    }

    private void ViewDetails(int id)
    {
        // Navigate or open modal
    }
}
```

### Step 4: Add Page-Scoped CSS (Optional)
```bash
touch src/RTUB.Web/Pages/Member/MyNewPage.razor.css
```

### Step 5: Update Navigation (If Needed)
If the page should appear in the navigation menu, update `/src/RTUB.Web/Shared/NavMenu.razor`:

```razor
<AuthorizeView Roles="Admin,Owner">
    <Authorized>
        <div class="nav-item px-3">
            <NavLink class="nav-link" href="my-new-page">
                <i class="bi bi-star me-2"></i> My New Page
            </NavLink>
        </div>
    </Authorized>
</AuthorizeView>
```

### Step 6: Document the Page
Add the page to `/docs/pages.md`:
```markdown
### My New Page
**Route:** `/my-new-page`

**Attribute:** `[Authorize(Roles = "Admin,Owner")]`

**Description:** Manage [entities] with [features].

**Main Components:**
- `MyNewCard`
- `CrudModalManager`
- `LoadingSpinner`

**Permissions:** Admin, Owner only

**Notes:** [Any special considerations]
```

---

## Layout and CSS Rules

### Global Styles
- **Bootstrap 5** is the primary CSS framework
- Global styles in `/src/RTUB.Web/wwwroot/css/site.css`
- Theme variables in `/src/RTUB.Web/wwwroot/css/variables.css` (if exists)

### Page-Scoped CSS
- Use `ComponentName.razor.css` for component-specific styles
- Blazor automatically scopes CSS to the component (generates unique attributes)
- **Prefer page-scoped CSS** over global styles to avoid conflicts

### CSS Variable Usage
Use Bootstrap and custom CSS variables:
```css
/* Good */
color: var(--bs-primary);
background-color: var(--bs-light);
border-color: var(--bs-border-color);

/* Avoid hardcoded colors */
color: #0d6efd; /* Bad - use var(--bs-primary) instead */
```

### Responsive Design
- Use Bootstrap grid classes: `row`, `col-*`, `col-md-*`, `col-lg-*`
- Use Bootstrap responsive utilities: `d-none`, `d-md-block`, etc.
- Test on multiple screen sizes (mobile, tablet, desktop)

### Accessibility
- Use semantic HTML (`<button>`, `<nav>`, `<main>`, etc.)
- Add `aria-label` for icons without text:
  ```html
  <button aria-label="Delete item">
      <i class="bi bi-trash"></i>
  </button>
  ```
- Ensure sufficient color contrast (use browser dev tools to check)

### Icons
- Use **Bootstrap Icons** (`bi bi-*`)
- Place icons inside buttons/links with spacing:
  ```html
  <button class="btn btn-primary">
      <i class="bi bi-plus-lg me-2"></i> Add New
  </button>
  ```

---

## Adding New Tests

RTUB has test projects for each layer:
- **RTUB.Core.Tests** - Entity and domain logic tests
- **RTUB.Application.Tests** - Service and business logic tests
- **RTUB.Shared.Tests** - Razor component tests (using Bunit)
- **RTUB.Web.Tests** - Page integration tests
- **RTUB.Integration.Tests** - End-to-end tests

### Unit Test Structure
```csharp
using Xunit;
using FluentAssertions; // Recommended assertion library

namespace RTUB.Application.Tests.Services;

public class MyServiceTests
{
    [Fact]
    public async Task GetItemById_ValidId_ReturnsItem()
    {
        // Arrange
        var service = new MyService(/* dependencies */);
        var expectedId = 1;

        // Act
        var result = await service.GetItemByIdAsync(expectedId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(expectedId);
    }

    [Fact]
    public async Task GetItemById_InvalidId_ReturnsNull()
    {
        // Arrange
        var service = new MyService(/* dependencies */);

        // Act
        var result = await service.GetItemByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }
}
```

### Component Test Structure (Bunit)
```csharp
using Bunit;
using Xunit;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

public class MyComponentTests : TestContext
{
    [Fact]
    public void Component_RendersCorrectly()
    {
        // Arrange & Act
        var cut = RenderComponent<MyComponent>(parameters => parameters
            .Add(p => p.Title, "Test Title"));

        // Assert
        cut.Markup.Should().Contain("Test Title");
    }

    [Fact]
    public async Task Component_ButtonClick_InvokesCallback()
    {
        // Arrange
        var clicked = false;
        var cut = RenderComponent<MyComponent>(parameters => parameters
            .Add(p => p.OnClick, () => clicked = true));

        // Act
        await cut.Find("button").ClickAsync(new MouseEventArgs());

        // Assert
        clicked.Should().BeTrue();
    }
}
```

### Running Tests
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/RTUB.Application.Tests

# Run with coverage
dotnet test /p:CollectCoverage=true
```

### Test Naming Convention
- **Method_Scenario_ExpectedResult** (e.g., `GetItemById_InvalidId_ReturnsNull`)
- Be descriptive and specific
- Test methods should read like documentation

---

## Documentation Requirements

### When Adding New Features
1. **Code comments:** Add XML comments to public APIs
2. **Component documentation:** Update `/docs/components.md` for new components
3. **Page documentation:** Update `/docs/pages.md` for new pages
4. **Business rules:** Update `/docs/auth-and-rules.md` if adding new rules or permissions
5. **Contributing guide:** Update this file if adding new patterns or conventions

### Documentation Style
- **English** for documentation, but preserve Portuguese domain terms
- **First use of Portuguese term:** Provide English explanation
  - Example: "Magister (President of the board)"
- **Concise and practical:** Focus on what developers need to know
- **Code examples:** Include working code snippets

---

## Git Workflow

### Branch Naming
- `feature/feature-name` - New features
- `bugfix/bug-description` - Bug fixes
- `hotfix/critical-fix` - Critical production fixes
- `docs/documentation-update` - Documentation changes

### Commit Messages
Follow conventional commits:
```
type(scope): short description

Longer explanation if needed

Fixes #123
```

**Types:**
- `feat` - New feature
- `fix` - Bug fix
- `docs` - Documentation changes
- `style` - Code style changes (formatting, no logic change)
- `refactor` - Code refactoring
- `test` - Adding or updating tests
- `chore` - Maintenance tasks

**Examples:**
```
feat(events): add enrollment statistics view
fix(auth): resolve email confirmation token expiration
docs(components): add documentation for MyNewCard
```

### Pull Request Process
1. Create a branch from `main` (or `develop` if using GitFlow)
2. Make changes and commit frequently
3. Push branch to remote
4. Open Pull Request with:
   - **Title:** Clear and descriptive
   - **Description:** What changed and why
   - **Screenshots:** For UI changes
   - **Checklist:** Completed tasks
5. Request review from maintainers
6. Address feedback and update PR
7. Merge after approval

---

## Common Tasks

### Add a New Service
1. **Create interface** in `RTUB.Application/Interfaces/IMyService.cs`:
   ```csharp
   public interface IMyService
   {
       Task<List<MyEntity>> GetAllAsync();
       Task<MyEntity?> GetByIdAsync(int id);
       Task<MyEntity> CreateAsync(MyEntity entity);
       Task UpdateAsync(MyEntity entity);
       Task DeleteAsync(int id);
   }
   ```

2. **Implement service** in `RTUB.Application/Services/MyService.cs`:
   ```csharp
   public class MyService : IMyService
   {
       private readonly ApplicationDbContext _context;
       private readonly IAuditLogService _auditLog;

       public MyService(ApplicationDbContext context, IAuditLogService auditLog)
       {
           _context = context;
           _auditLog = auditLog;
       }

       // Implementation...
   }
   ```

3. **Register in DI** in `Program.cs`:
   ```csharp
   builder.Services.AddScoped<IMyService, MyService>();
   ```

### Add a New Entity
1. **Create entity** in `RTUB.Core/Entities/MyEntity.cs`:
   ```csharp
   public class MyEntity : BaseEntity
   {
       public string Title { get; set; } = string.Empty;
       public string Description { get; set; } = string.Empty;
   }
   ```

2. **Add DbSet** in `ApplicationDbContext.cs`:
   ```csharp
   public DbSet<MyEntity> MyEntities { get; set; }
   ```

3. **Create migration:**
   ```bash
   cd src/RTUB.Application
   dotnet ef migrations add AddMyEntity
   dotnet ef database update
   ```

### Add Authorization to a Feature
```razor
@* Page-level *@
@attribute [Authorize(Roles = "Admin,Owner")]

@* Component-level *@
<AuthorizeView Roles="Admin">
    <Authorized>
        <button @onclick="DeleteItem">Delete</button>
    </Authorized>
</AuthorizeView>

@* Code-level *@
@code {
    private bool isAdmin = false;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        isAdmin = authState.User.IsInRole("Admin") || authState.User.IsInRole("Owner");
    }
}
```

### Add Audit Logging
```csharp
await _auditLogService.LogAsync(
    userId: currentUserId,
    actionType: "Create",
    entityType: "MyEntity",
    entityId: entity.Id.ToString(),
    details: $"Created entity: {entity.Title}"
);
```

---

## Best Practices Summary

1. **Follow existing patterns** - consistency is key
2. **Use shared components** - avoid duplication
3. **Page-scoped CSS** - keep styles isolated
4. **Document as you go** - update /docs when adding features
5. **Test your changes** - add tests for new logic
6. **Check permissions** - ensure proper authorization
7. **Log important actions** - use IAuditLogService
8. **Handle errors gracefully** - use ErrorDisplay or Alert components
9. **Keep it simple** - avoid over-engineering
10. **Ask for help** - when in doubt, ask maintainers

---

## See Also

- [Components Guide](components.md) - Component reference
- [Pages Reference](pages.md) - Page structure and permissions
- [Authentication & Business Rules](auth-and-rules.md) - Authorization details
