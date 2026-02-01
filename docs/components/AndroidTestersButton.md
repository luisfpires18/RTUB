# AndroidTestersButton Component

## Overview
The `AndroidTestersButton` component is a reusable UI component that displays all Android testers and provides functionality to send custom push notifications to them.

## Location
- **Component**: `/src/RTUB.Shared/Components/UI/AndroidTestersButton.razor`
- **Styles**: `/src/RTUB.Shared/Components/UI/AndroidTestersButton.razor.css`

## Features
1. **Display Button**: Shows a white button with a bell icon and "Testadores Android" text
2. **Modal Display**: Opens a modal showing all users with `IsAndroidTester = true`
3. **Search Functionality**: Includes a search bar to filter testers by name or username
4. **Pagination**: Supports paginated display of testers (12, 24, 36, 48, 60 per page)
5. **Custom Push Notifications**: 
   - Textarea for custom message input (max 500 characters)
   - Send button to dispatch notifications to all Android testers
   - Success/error feedback messages

## Usage

### Basic Usage
```razor
<AndroidTestersButton />
```

### Example Integration
The component has been integrated into the Notifications page (`/src/RTUB.Web/Pages/Operations/Notifications.razor`) as a demonstration.

## Dependencies
- **UserManager<ApplicationUser>**: To query users with `IsAndroidTester` flag
- **IPushNotificationService**: To send push notifications
- **NavigationManager**: To build notification URLs
- **Shared Components**:
  - `Modal`: For the popup dialog
  - `SearchBar`: For filtering testers
  - `TablePagination`: For paginated display
  - `MemberCardLite`: For displaying individual tester cards
  - `EmptyState`: For empty states

## Technical Details

### Data Model
The component uses an internal `AndroidTester` class that includes:
- UserId
- UserName
- Nickname
- FirstName
- LastName
- AvatarUrl
- LastLoginDate

### Notification Structure
When sending notifications, the component creates a `SendPushNotificationDto` with:
- **Title**: "RTUB - Testadores Android"
- **Body**: Custom message from textarea
- **Icon**: "/images/logo-512x512.png"
- **Url**: Base URL of the application
- **Tag**: "android-testers-notification"

### Styling
- Button uses `btn btn-white` classes (white button theme)
- Bell icon from Bootstrap Icons (`bi bi-bell`)
- Modal size set to `Large`
- Scoped CSS for component-specific styling

## Authorization
⚠️ **Important**: This component should only be used in pages with appropriate authorization (e.g., Admin role), as it has access to user data and can send push notifications.

## Example Implementation in Notifications Page
```razor
<div class="d-flex flex-column flex-lg-row align-items-lg-center justify-content-between gap-3 mb-3">
    <div>
        <h1 class="page-title mb-2"><i class="bi bi-bell"></i> Enviar Notificação Push</h1>
        <p class="lead page-header-subtitle mb-0 text-light-theme">Enviar notificações push...</p>
    </div>
    <div class="page-header-actions justify-content-lg-end">
        <AndroidTestersButton />
    </div>
</div>
```

## Future Enhancements
- Add filtering by last login date
- Include user statistics (app version, last activity)
- Support for notification templates
- Notification history/logs
