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
