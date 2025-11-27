# ADR 002: CSS Architecture and BEM Naming Convention

**Date:** 2025-11-27  
**Status:** Accepted  
**Context:** CSS architecture standardization for agent-friendly development

---

## Context and Problem Statement

The RTUB project has grown to include multiple complex UI components, particularly in the messaging/chat system. As the codebase expanded, several issues emerged:

- CSS class name conflicts between components
- Difficulty understanding class purpose without context
- Challenges for AI agents and developers to navigate and modify styles
- Inconsistent naming patterns across different parts of the application
- Maintenance overhead when debugging styling issues

A structured, predictable naming convention was needed to address these challenges and enable efficient collaboration between human developers and AI coding agents.

## Decision Drivers

- Need for clear, self-documenting CSS class names
- Prevention of CSS conflicts between components
- Support for AI/agent-friendly code structure
- Easier debugging and maintenance
- Consistent patterns across the codebase
- Scalability for future component additions

## Considered Options

1. **BEM-like naming with project prefix** - Custom convention based on BEM with `rtub-` prefix
2. **Standard BEM** - Block__Element--Modifier without project prefix
3. **CSS Modules** - Scoped CSS with auto-generated class names
4. **Utility-first (Tailwind-like)** - Composition of utility classes
5. **Atomic CSS** - Single-purpose classes

## Decision Outcome

**Chosen option:** "BEM-like naming with project prefix" because it:

- Provides globally unique class names that prevent conflicts
- Creates self-documenting code that agents can easily understand
- Maintains human readability while being machine-parseable
- Allows for easy component isolation and debugging
- Follows established industry patterns with project-specific customization

---

## Naming Convention Specification

### Pattern Structure

```
rtub-[component]__[element]--[modifier]
```

| Part | Description | Example |
|------|-------------|---------|
| `rtub-` | Project prefix (prevents global conflicts) | `rtub-` |
| `[component]` | Block/component name | `messages`, `navbar`, `card` |
| `__` | Element separator | `__panel`, `__header` |
| `[element]` | Sub-element within component | `panel`, `item`, `bubble` |
| `--` | Modifier separator | `--mobile`, `--sent` |
| `[modifier]` | Variation or state | `mobile`, `sent`, `large` |

### State Classes

Use the `is-[state]` pattern for dynamic states that can be toggled:

| Pattern | Usage | Examples |
|---------|-------|----------|
| `is-active` | Currently active/selected item | `.rtub-messages__item.is-active` |
| `is-selected` | User-selected state | `.rtub-messages__item.is-selected` |
| `is-sending` | In-progress action | `.rtub-messages__composer.is-sending` |
| `is-loading` | Loading state | `.rtub-messages__thread.is-loading` |
| `is-visible` | Visibility toggle | `.rtub-messages__panel.is-visible` |
| `is-disabled` | Disabled state | `.rtub-messages__composer-send.is-disabled` |

### CSS Custom Properties

Use the `--rtub-[component]-*` prefix for component-specific custom properties:

```css
/* Component-scoped custom properties */
--rtub-msg-content-offset    /* Message content offset for layout */
--rtub-msg-navbar-height     /* Height of navbar */
--rtub-msg-avatar-size       /* Avatar size */
--rtub-msg-avatar-margin     /* Avatar margin */
--rtub-msg-primary-hover     /* Hover state for primary color */
--rtub-msg-header-height     /* Mobile header height */
--rtub-msg-footer-height     /* Mobile footer height */
```

---

## Implementation Examples

### Messaging Component Reference

The messaging/chat component (`Inbox.razor.css`) serves as the reference implementation:

| Component | Class Name |
|-----------|-----------|
| Root Container | `.rtub-messages` |
| Panel | `.rtub-messages__panel` |
| Panel Header | `.rtub-messages__panel-header` |
| Mobile Header | `.rtub-messages__panel-header--mobile` |
| List Item | `.rtub-messages__item` |
| Active State | `.is-active` |
| Thread Container | `.rtub-messages__thread` |
| Thread Header | `.rtub-messages__thread-header` |
| Thread Body | `.rtub-messages__thread-body` |
| Message Bubble | `.rtub-messages__bubble` |
| Sent Bubble | `.rtub-messages__bubble--sent` |
| Received Bubble | `.rtub-messages__bubble--received` |
| Composer | `.rtub-messages__composer` |
| Composer Input | `.rtub-messages__composer-input` |
| Composer Send Button | `.rtub-messages__composer-send` |

### HTML Structure Example

```html
<div class="rtub-messages">
    <!-- Conversations Panel -->
    <div class="rtub-messages__panel">
        <div class="rtub-messages__panel-header">
            <h2>Conversations</h2>
        </div>
        <div class="rtub-messages__panel-header--mobile">
            <!-- Mobile-specific header -->
        </div>
        <div class="rtub-messages__item is-active">
            <!-- Active conversation item -->
        </div>
    </div>
    
    <!-- Thread Panel -->
    <div class="rtub-messages__thread">
        <div class="rtub-messages__thread-header">
            <!-- Thread header with user info -->
        </div>
        <div class="rtub-messages__thread-body">
            <div class="rtub-messages__bubble rtub-messages__bubble--sent">
                <!-- Sent message -->
            </div>
            <div class="rtub-messages__bubble rtub-messages__bubble--received">
                <!-- Received message -->
            </div>
        </div>
        <div class="rtub-messages__composer">
            <input class="rtub-messages__composer-input" />
            <button class="rtub-messages__composer-send">Send</button>
        </div>
    </div>
</div>
```

### CSS Custom Properties Example

```css
.rtub-messages {
    /* Component-scoped custom properties */
    --rtub-msg-content-offset: 60px;
    --rtub-msg-navbar-height: 56px;
    --rtub-msg-avatar-size: 40px;
    --rtub-msg-avatar-margin: 8px;
    --rtub-msg-primary-hover: rgba(0, 123, 255, 0.1);
    --rtub-msg-header-height: 50px;
    --rtub-msg-footer-height: 60px;
}

.rtub-messages__panel {
    width: 300px;
    height: calc(100vh - var(--rtub-msg-navbar-height));
}

.rtub-messages__bubble--sent {
    background-color: var(--rtub-msg-primary-hover);
    align-self: flex-end;
}
```

---

## File Organization

### CSS Directory Structure

```
src/RTUB.Web/wwwroot/css/
├── 1-base/                    # Variables and foundations
│   ├── variables.css          # Global CSS custom properties
│   ├── global.css             # Reset and base styles
│   ├── utilities.css          # Utility classes
│   └── mobile.css             # Mobile-first breakpoints
├── 2-layout/                  # Structural components
│   ├── navbar.css             # Navigation bar
│   ├── sidebar.css            # Sidebar navigation
│   ├── footer.css             # Footer component
│   ├── grid.css               # Grid system
│   └── main-content.css       # Main content area
├── 3-components/              # Reusable UI patterns
│   ├── buttons.css            # Button variants
│   ├── forms.css              # Form elements
│   ├── cards.css              # Card components
│   ├── modals.css             # Modal dialogs
│   └── ...                    # Other components
├── 4-pages/                   # Page-specific styles
│   └── ...                    # Page-specific overrides
└── site.css                   # Entry point (imports all modules)
```

### Section Organization Within CSS Files

CSS files should be organized in the following section order:

1. **CSS Custom Properties** - Component-scoped variables at the top
2. **Root Container** - Base component styles
3. **Panel (Conversations List)** - List/sidebar panel styles
4. **Panel Header (Desktop)** - Desktop header variants
5. **Panel Header (Mobile)** - Mobile-specific headers
6. **Panel Items (Conversation Items)** - List item styles
7. **Thread Panel** - Main content panel
8. **Thread Header** - Content header styles
9. **Thread Body** - Content body styles
10. **Message Bubbles** - Message-specific styles
11. **Message Grouping** - Grouped message styles
12. **Thread Footer (Composer)** - Input/action area styles
13. **Modal Styles** - Component modal styles
14. **Responsive Rules** - Media queries at the end

---

## Guidelines for Future Development

### Adding a New Component

1. **Choose a descriptive component name** (e.g., `calendar`, `profile`, `notifications`)

2. **Create the root class with prefix**:
   ```css
   .rtub-calendar { }
   ```

3. **Add elements using double underscore**:
   ```css
   .rtub-calendar__header { }
   .rtub-calendar__day { }
   .rtub-calendar__event { }
   ```

4. **Add modifiers using double dash**:
   ```css
   .rtub-calendar__day--today { }
   .rtub-calendar__day--selected { }
   .rtub-calendar__event--recurring { }
   ```

5. **Use state classes for dynamic states**:
   ```css
   .rtub-calendar__day.is-highlighted { }
   .rtub-calendar__event.is-editing { }
   ```

6. **Define custom properties at component root**:
   ```css
   .rtub-calendar {
       --rtub-cal-cell-size: 40px;
       --rtub-cal-header-height: 50px;
       --rtub-cal-border-color: #e0e0e0;
   }
   ```

### Naming Best Practices

| Do | Don't |
|----|-------|
| `rtub-messages__item` | `msg-item` |
| `rtub-messages__bubble--sent` | `sent-bubble` |
| `is-active` | `active` |
| `--rtub-msg-avatar-size` | `--avatar-size` |
| `rtub-calendar__day--today` | `today` |

### Element Depth Guideline

Avoid nesting beyond two levels. If you need more specificity:

```css
/* Good: Flat structure */
.rtub-messages__panel-header { }
.rtub-messages__panel-header-title { }
.rtub-messages__panel-header-action { }

/* Avoid: Deep nesting */
.rtub-messages__panel__header__title { }  /* Too deep */
```

---

## Positive Consequences

- **Agent-friendly code**: AI coding agents can easily parse and understand the CSS structure
- **Self-documenting**: Class names clearly indicate component, element, and variant
- **Zero conflicts**: Project prefix ensures no clashes with third-party libraries
- **Easy debugging**: Browser DevTools clearly show component hierarchy
- **Scalable**: Pattern works for any component size or complexity
- **Maintainable**: Clear ownership of styles within component boundaries
- **Searchable**: Consistent patterns enable effective codebase searches

## Negative Consequences

- **Longer class names**: Names are more verbose than short class names
- **Learning curve**: Developers must learn the naming convention
- **Refactoring effort**: Existing CSS may need updates to follow the pattern
- **Potential for typos**: Longer names increase typo risk (mitigated by IDE autocomplete)

---

## References

- [BEM Methodology](https://en.bem.info/methodology/)
- [CSS Architecture for Design Systems](https://bradfrost.com/blog/post/css-architecture-for-design-systems/)
- [Naming CSS Stuff Is Really Hard](https://seesparkbox.com/foundry/naming_css_stuff_is_really_hard)
- Messaging component implementation: `src/RTUB.Web/Components/Pages/Inbox.razor.css`

---

## Related Decisions

- [ADR 001: ShouldRender Optimization Pattern](001-shouldrender-optimization.md)
- ADR 003: Service Worker Caching Strategy (pending)

---

_This ADR documents the architectural decision made on 2025-11-27._
