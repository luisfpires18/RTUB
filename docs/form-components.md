# Form Components Documentation

## Overview

Modern, reusable form components following the DetailsModal and Profile design patterns from the RTUB application. These components provide a consistent, beautiful UI with icon support and proper styling.

## Design Philosophy

The form components follow the same visual design language as:
- **DetailsModal** - Elevated sections with headers and borders
- **Profile Page** - Modern card-based sections with icons
- **InfoSection** - Section containers with visual grouping

### Key Features
- ✅ **Icons with labels** - Bootstrap Icons with `text-primary` styling
- ✅ **Modern card-based sections** - Elevated visual grouping
- ✅ **Consistent spacing** - Following established patterns
- ✅ **Responsive design** - Mobile-friendly layouts
- ✅ **Accessibility** - Proper label associations
- ✅ **Page-scoped CSS** - Component styling in global CSS

## Components

### 1. FormSection

Container for grouping form fields with modern design.

**Usage:**
```razor
<FormSection Title="Informações Pessoais" Icon="person-fill">
    <!-- Form fields go here -->
</FormSection>
```

**Parameters:**
- `Title` (string) - Section title
- `Icon` (string, optional) - Bootstrap icon name (without "bi-" prefix)
- `CssClass` (string, optional) - Additional CSS classes

**Visual Design:**
- Background: `rgba(30, 30, 30, 0.5)` with border
- Header: Purple gradient with divider
- Elevated appearance with shadow

### 2. FormTextField

Text input field with optional icon.

**Usage:**
```razor
<FormTextField Label="Nome Completo"
               Icon="person"
               @bind-Value="fullName"
               Placeholder="Inserir nome completo..."
               Required="true"
               HelpText="O seu nome completo" />
```

**Parameters:**
- `Label` (string) - Field label
- `Icon` (string, optional) - Bootstrap icon name
- `Value` (string) - Current value
- `ValueChanged` (EventCallback<string>) - Value change callback
- `Placeholder` (string, optional) - Placeholder text
- `Required` (bool) - Show required indicator (*)
- `Disabled` (bool) - Disable input
- `ReadOnly` (bool) - Make read-only
- `HelpText` (string, optional) - Help text below input
- `CssClass`, `LabelCssClass`, `InputCssClass` (string, optional) - Additional CSS classes

### 3. FormTextArea

Textarea field with optional icon.

**Usage:**
```razor
<FormTextArea Label="Descrição"
              Icon="text-paragraph"
              @bind-Value="description"
              Rows="4"
              Placeholder="Descreva..." />
```

**Parameters:**
Same as FormTextField, plus:
- `Rows` (int) - Number of textarea rows (default: 3)

### 4. FormSelect

Select dropdown with optional icon.

**Usage:**
```razor
<FormSelect Label="Tipo de Evento"
            Icon="tag"
            @bind-Value="eventType"
            DefaultOption="Selecione o tipo...">
    <option value="concert">Concerto</option>
    <option value="rehearsal">Ensaio</option>
    <option value="social">Convívio</option>
</FormSelect>
```

**Parameters:**
Same as FormTextField, plus:
- `DefaultOption` (string, optional) - Default option text
- `ChildContent` (RenderFragment) - Option elements

## Complete Example

### Example 1: Simple Form with FormSection

```razor
@page "/example"
@using RTUB.Shared

<div class="container py-4">
    <div class="row justify-content-center">
        <div class="col-lg-10 col-xl-9">
            
            <!-- Section 1: Personal Info -->
            <FormSection Title="Informações Pessoais" Icon="person-fill">
                <div class="row">
                    <div class="col-md-6">
                        <FormTextField Label="Nome Completo"
                                      Icon="person"
                                      @bind-Value="fullName"
                                      Placeholder="Inserir nome completo..."
                                      Required="true" />
                    </div>
                    <div class="col-md-6">
                        <FormTextField Label="Email"
                                      Icon="envelope"
                                      @bind-Value="email"
                                      Placeholder="exemplo@rtub.pt"
                                      Required="true" />
                    </div>
                </div>
            </FormSection>

            <!-- Section 2: Event Details -->
            <FormSection Title="Detalhes do Evento" Icon="calendar-event">
                <FormTextField Label="Nome do Evento"
                              Icon="kanban"
                              @bind-Value="eventName"
                              Placeholder="Nome da atuação..."
                              Required="true" />

                <FormTextArea Label="Descrição"
                             Icon="text-paragraph"
                             @bind-Value="eventDescription"
                             Rows="4"
                             Placeholder="Descreva o evento..." />

                <FormSelect Label="Tipo de Evento"
                           Icon="tag"
                           @bind-Value="eventType"
                           DefaultOption="Selecione o tipo...">
                    <option value="concert">Concerto</option>
                    <option value="rehearsal">Ensaio</option>
                </FormSelect>
            </FormSection>

        </div>
    </div>
</div>

@code {
    private string? fullName;
    private string? email;
    private string? eventName;
    private string? eventDescription;
    private string? eventType;
}
```

### Example 2: Modal Form (Logistics Board)

Real-world example from Logistics.razor showing form components in a modal:

```razor
<div class="modal-body">
    <FormSection>
        <FormTextField Label="Nome do Quadro"
                      Icon="kanban"
                      @bind-Value="boardName"
                      Placeholder="Nome do quadro"
                      Required="true" />
        
        <FormTextArea Label="Descrição"
                     Icon="text-paragraph"
                     @bind-Value="boardDescription"
                     Rows="3"
                     Placeholder="Descrição do quadro (opcional)" />
        
        <div class="mb-3">
            <label class="form-label fw-semibold">
                <i class="bi bi-calendar-event text-primary me-2"></i>Evento Associado (opcional)
            </label>
            <input type="text" class="form-control" placeholder="Pesquisar evento..." 
                   @bind="eventSearchTerm" @bind:event="oninput" />
            @* Event search results... *@
        </div>
    </FormSection>
</div>
```

### Example 3: Using with EditForm Validation

For forms requiring validation, use InputText/InputDate/InputSelect with icon labels:

```razor
<EditForm Model="editingEvent" OnValidSubmit="SaveEvent">
    <DataAnnotationsValidator />
    
    <div class="mb-3">
        <label class="form-label fw-semibold">
            <i class="bi bi-kanban text-primary me-2"></i>Nome da Atuação
        </label>
        <InputText class="form-control" @bind-Value="editingEvent!.Name" />
    </div>

    <div class="row">
        <div class="col-md-6 mb-3">
            <label class="form-label fw-semibold">
                <i class="bi bi-calendar3 text-primary me-2"></i>Data
            </label>
            <InputDate class="form-control" @bind-Value="editingEvent!.Date" />
        </div>
        <div class="col-md-6 mb-3">
            <label class="form-label fw-semibold">
                <i class="bi bi-clock text-primary me-2"></i>Hora
            </label>
            <input type="time" class="form-control" @bind="eventTime" />
        </div>
    </div>

    <div class="mb-3">
        <label class="form-label fw-semibold">
            <i class="bi bi-text-paragraph text-primary me-2"></i>Descrição
        </label>
        <InputTextArea class="form-control" rows="4" @bind-Value="editingEvent!.Description" />
    </div>
</EditForm>
```

## Icons Reference

Common icons to use with form fields:

### Personal Information
- `person` - Name fields
- `star` - Nickname/Tuna name
- `envelope` - Email
- `telephone` - Phone
- `geo-alt` - Location/Address
- `calendar3` - Birth date

### Events & Activities
- `calendar-event` - Event date
- `kanban` - Board/Project name
- `tag` - Category/Type
- `clock` - Time
- `geo-alt-fill` - Location

### Content
- `text-paragraph` - Description/Text
- `card-text` - Title/Heading
- `file-text` - Notes
- `pen` - Author/Writer

### Music
- `music-note` - Song title
- `music-note-beamed` - Music related
- `file-music` - Music file
- `soundwave` - Instrument

### Media
- `image` - Image upload
- `camera` - Photo
- `film` - Video
- `link-45deg` - URL

## Styling

The components automatically inherit the app's dark theme with purple accents:

- **Primary Color**: Purple (`var(--bs-primary)`)
- **Background**: Dark (`#1c1c1d`)
- **Text**: White
- **Borders**: Subtle purple tint
- **Focus**: Purple glow effect

## Migration Guide

To migrate existing forms to use these components:

### Before:
```razor
<div class="mb-3">
    <label class="form-label">Nome</label>
    <input type="text" class="form-control" @bind="name" />
</div>
```

### After:
```razor
<FormTextField Label="Nome"
              Icon="person"
              @bind-Value="name" />
```

### Before (with icon - Logistics pattern):
```razor
<div class="mb-3">
    <label class="form-label fw-semibold">
        <i class="bi bi-kanban text-primary me-2"></i>Nome do Quadro
    </label>
    <input type="text" class="form-control" @bind="boardName" placeholder="Nome do quadro" />
</div>
```

### After:
```razor
<FormTextField Label="Nome do Quadro"
              Icon="kanban"
              @bind-Value="boardName"
              Placeholder="Nome do quadro" />
```

## Benefits

1. **Consistency** - All forms have the same beautiful look
2. **Less Code** - Reduces markup by ~60-70%
3. **Maintainability** - Single source of truth for styling
4. **Accessibility** - Proper label-input associations
5. **Icons** - Easy to add visual context
6. **Responsive** - Mobile-friendly by default

## Migration Notes

### Real-World Migration Experience

**Pages Successfully Migrated:**
- ✅ Logistics.razor - Board create/edit modal
- ✅ LogisticsBoard.razor - List and Card create modals
- ✅ Events.razor - Event create/edit form (with validation)
- ✅ Members.razor - Member create/edit form
- ✅ Shop.razor - Product create/edit form
- ✅ Inventory.razor - Instrument create/edit form

### Key Patterns Discovered

#### Pattern 1: Simple Forms (No Validation)
Use FormTextField, FormTextArea, and FormSelect components directly:
```razor
<FormSection>
    <FormTextField Label="Nome" Icon="person" @bind-Value="name" Required="true" />
    <FormTextArea Label="Descrição" Icon="text-paragraph" @bind-Value="description" Rows="3" />
</FormSection>
```

#### Pattern 2: Forms with EditForm Validation
Keep InputText/InputDate/InputSelect for validation, but add icons to labels:
```razor
<EditForm Model="model" OnValidSubmit="Save">
    <div class="mb-3">
        <label class="form-label fw-semibold">
            <i class="bi bi-person text-primary me-2"></i>Nome
        </label>
        <InputText class="form-control" @bind-Value="model.Name" />
    </div>
</EditForm>
```

#### Pattern 3: Mixed Forms
Combine both patterns - use FormSection for grouping, custom labels for special fields:
```razor
<FormSection>
    <FormTextField Label="Título" Icon="card-text" @bind-Value="title" />
    
    <!-- Custom field with search functionality -->
    <div class="mb-3">
        <label class="form-label fw-semibold">
            <i class="bi bi-person text-primary me-2"></i>Atribuir a
        </label>
        <input type="text" class="form-control" @bind="userSearch" />
        @* Search results... *@
    </div>
</FormSection>
```

### Icon Selection Guide

**Discovered Icon Patterns:**

**Data Entry:**
- `kanban` - Project/Board names
- `card-text` - Title/Heading fields
- `text-paragraph` - Description/Text areas
- `tag` - Category/Type selectors

**Dates & Time:**
- `calendar3` - General dates
- `calendar-event` - Event dates
- `calendar-check` - Completion/Due dates
- `calendar-plus` - Start dates
- `clock` - Time fields

**People & Contact:**
- `person` - Name fields
- `star` - Nickname/Display name
- `envelope` - Email
- `telephone` - Phone

**Location & Items:**
- `geo-alt` - Location/Address
- `box` - Product/Item name
- `boxes` - Stock/Quantity
- `music-note-beamed` - Instrument/Music
- `bookmark` - Brand/Label
- `hash` - Serial number/ID

**Files & Media:**
- `image` - Image upload
- `file-text` - Notes/Documents
- `paperclip` - Attachments

**Financial:**
- `currency-euro` - Price/Cost

### Lessons Learned

1. **FormSection is optional** - You can use form components without wrapping in FormSection if you prefer inline styling.

2. **Validation compatibility** - For forms requiring DataAnnotationsValidator, stick with Microsoft.AspNetCore.Components.Forms components (InputText, etc.) and just add icons to labels.

3. **Icon consistency** - Use `fw-semibold` class on labels for visual consistency with icons.

4. **Color scheme** - Icons use `text-primary` class to match the purple theme throughout the app.

5. **Flexibility** - Mix and match patterns as needed - not all fields need to use form components if they have special functionality.

### Migration Checklist

When migrating a form:

- [ ] Identify form type (simple or with validation)
- [ ] Choose appropriate pattern
- [ ] Select meaningful icons for each field
- [ ] Wrap simple forms in FormSection for grouping
- [ ] Add `fw-semibold` to labels with icons
- [ ] Maintain all `@bind-Value` and event handlers
- [ ] Test functionality after migration
- [ ] Verify mobile responsiveness

### Performance Notes

- Form components are lightweight with minimal overhead
- No JavaScript dependencies
- Server-side rendering compatible
- Icons loaded from Bootstrap Icons (already in use)

### Future Enhancements

Potential improvements identified during migration:

1. **FormDateField** - Dedicated component for date inputs with icon
2. **FormNumberField** - Dedicated component for number inputs
3. **FormCheckbox** - Checkbox with icon and label
4. **FormFileUpload** - File upload with icon and preview
5. **Validation support** - Add built-in validation message display
