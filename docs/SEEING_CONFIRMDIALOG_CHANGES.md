# How to See ConfirmDialog Red Header Styling

## The Enhanced Styling is Already Applied

The ConfirmDialog component has been updated with red header styling for danger actions. Here's what you need to know:

## When Will You See the Red Header?

The red header **only appears** when using `ConfirmButtonClass="btn-danger"`. This is by design - it's a visual indicator for dangerous/destructive actions like deletions.

### Pages with Red Headers (btn-danger)

These pages will show the red header when you trigger their delete confirmations:

1. **EventDiscussion.razor**
   - Delete Post confirmation
   - Delete Comment confirmation
   - ✅ Uses `ConfirmButtonClass="btn-danger"`

2. **Shop.razor**
   - Cancel Reservation confirmation
   - Admin Delete Reservation confirmation
   - ✅ Uses `ConfirmButtonClass="btn-danger"`

3. **Requests.razor**
   - Reject Request confirmation
   - ✅ Uses `ConfirmButtonClass="btn-danger"`

### Pages with Normal Headers (not btn-danger)

These pages will NOT show red headers (they use `btn-primary` or `btn-success`):

1. **UserRoles.razor**
   - Role Change confirmation
   - Birthday Email confirmation
   - ❌ Uses `ConfirmButtonClass="btn-primary"` (normal blue header)

2. **Requests.razor**
   - Approve Request confirmation
   - Create Event confirmation
   - ❌ Uses `ConfirmButtonClass="btn-success"` or `btn-primary` (normal headers)

## How to See the Changes

### If You Can't See the Red Header:

1. **Rebuild the application**
   ```bash
   dotnet clean
   dotnet build
   dotnet run
   ```

2. **Clear browser cache and hard refresh**
   - Chrome/Edge: `Ctrl + Shift + R` (Windows) or `Cmd + Shift + R` (Mac)
   - Firefox: `Ctrl + F5` (Windows) or `Cmd + Shift + R` (Mac)

3. **Try in incognito/private mode** to rule out caching issues

4. **Check browser developer tools**
   - Open Developer Tools (F12)
   - Go to Network tab
   - Check "Disable cache" checkbox
   - Refresh the page

## What the Red Header Looks Like

When using `ConfirmButtonClass="btn-danger"`, you'll see:

### Header:
- ⚠️ **Warning icon** (`bi-exclamation-triangle`) before the title
- 🔴 **Red background** (`bg-danger bg-opacity-10`)
- 🔴 **Red title text** (`text-danger`)

### Body:
- ℹ️ **Info icon** next to the message
- ⚠️ **Warning message** (if provided) in red with exclamation icon

### Footer:
- ❌ **Cancel button** with X icon
- 🗑️ **Delete/Confirm button** with trash icon (for danger actions)

## Testing the Red Header

To test if it's working:

1. Go to **EventDiscussion** page
2. Create a post
3. Click the delete button on the post
4. **You should see** the red header with warning icon

OR

1. Go to **Requests** page (as Admin)
2. Find a request to reject
3. Click "Rejeitar"
4. **You should see** the red header with warning icon

## Component Code

The logic in ConfirmDialog.razor (lines 13-14):
```razor
<div class="modal-header border-bottom border-secondary @(ConfirmButtonClass == "btn-danger" ? "bg-danger bg-opacity-10" : "")">
    <h5 class="modal-title @(ConfirmButtonClass == "btn-danger" ? "text-danger" : "")">
        @if (ConfirmButtonClass == "btn-danger")
        {
            <i class="bi bi-exclamation-triangle me-2"></i>
        }
        @Title
    </h5>
```

This automatically adds the red styling when `ConfirmButtonClass="btn-danger"`.

## Still Not Seeing It?

If you've rebuilt, cleared cache, and still can't see it:

1. Verify you're testing on a page that uses `btn-danger` (see list above)
2. Check the browser console for any JavaScript errors
3. Verify Bootstrap Icons are loading (the warning icon needs `bi-exclamation-triangle`)
4. Check that Bootstrap CSS is loaded (needed for `bg-danger` and `text-danger` classes)

The component code is correct and working - it's just a matter of getting the browser to use the new version!
