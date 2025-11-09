# Email Templates - Developer Guide

## Overview

Email templates in RTUB are implemented using Razor views (.cshtml files) to separate email content from business logic. This follows ASP.NET Core best practices and makes email templates easy to maintain.

## Architecture

- **Templates Location**: `/src/RTUB.Web/EmailTemplates/`
- **Models Location**: `/src/RTUB.Web/EmailTemplates/Models/`
- **Interface**: `IEmailTemplateRenderer` (in `RTUB.Application.Interfaces`)
- **Implementation**: `RazorEmailTemplateRenderer` (in `RTUB.Web.Services`)
- **View Rendering**: `EmailTemplateService` (in `RTUB.Web.Services`)

## How to Add a New Email Template

### 1. Create the Model Class

Create a new model class in `/src/RTUB.Web/EmailTemplates/Models/`:

```csharp
namespace RTUB.Web.EmailTemplates.Models;

public class YourEmailModel
{
    public string PropertyName { get; set; } = string.Empty;
    // Add more properties as needed
}
```

### 2. Create the Razor Template

Create a new `.cshtml` file in `/src/RTUB.Web/EmailTemplates/`:

```razor
@model RTUB.Web.EmailTemplates.Models.YourEmailModel

Hello!

This is your email content with: @Model.PropertyName

Best regards,
RTUB
```

### 3. Add Method to IEmailTemplateRenderer

Update `/src/RTUB.Application/Interfaces/IEmailTemplateRenderer.cs`:

```csharp
Task<string> RenderYourEmailAsync(string param1, string param2);
```

### 4. Implement the Method

Update `/src/RTUB.Web/Services/RazorEmailTemplateRenderer.cs`:

```csharp
public async Task<string> RenderYourEmailAsync(string param1, string param2)
{
    var model = new YourEmailModel
    {
        PropertyName = param1,
        // Map other properties
    };

    return await _templateService.RenderTemplateAsync("YourEmail", model);
}
```

### 5. Use in Your Service

Call the template renderer from your service:

```csharp
var body = await _templateRenderer.RenderYourEmailAsync(param1, param2);
await _emailSender.SendEmailAsync(email, subject, body);
```

## Existing Email Templates

1. **NewRequestNotification.cshtml** - Performance request notifications
2. **WelcomeEmail.cshtml** - New member welcome with credentials
3. **EventNotification.cshtml** - New event notifications for members
4. **PasswordReset.cshtml** - Password reset link (ASP.NET Identity)

## Testing

When testing services that use email templates, mock the `IEmailTemplateRenderer`:

```csharp
var mockTemplateRenderer = new Mock<IEmailTemplateRenderer>();
mockTemplateRenderer.Setup(x => x.RenderYourEmailAsync(
    It.IsAny<string>(), It.IsAny<string>()))
    .ReturnsAsync("Test email body");
```

## Best Practices

1. **Keep templates simple** - Focus on content, not complex logic
2. **Use strongly-typed models** - Provides compile-time safety
3. **Plain text emails** - Current templates use plain text; add HTML carefully
4. **Test templates** - Verify rendering in different email clients
5. **Localization** - Consider i18n if supporting multiple languages

## Email Services

- **EmailNotificationService** - Business logic for sending notifications
- **EmailSender** - SMTP implementation for ASP.NET Identity
- **EmailTemplateService** - Renders Razor views to strings
- **RazorEmailTemplateRenderer** - Bridge between Application and Web layers
