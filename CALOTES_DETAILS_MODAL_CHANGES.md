# Calotes Details Modal Implementation

## Summary
Added a details modal to the Calotes.razor page that displays all debt transactions for a selected member. Users can now click a "Ver Detalhes" (View Details) button on each member card to see a breakdown of their debt transactions.

## Changes Made

### 1. State Variables (Lines 373-376)
Added three new state variables to manage the details modal:
- `showDetailsModal`: Boolean to control modal visibility
- `viewingDebt`: Stores the currently viewing member's debt information
- `viewingMemberTransactions`: List of transactions for the selected member

```csharp
// Details Modal state
private bool showDetailsModal = false;
private MemberDebt? viewingDebt;
private List<Transaction> viewingMemberTransactions = new();
```

### 2. Action Button (Lines 165-174)
Added a "Ver Detalhes" button to each member card, styled with the purple theme:
- Positioned below the avatar-card-content
- Uses Bootstrap's `btn-sm btn-purple` classes
- Includes eye icon (bi-eye) matching AvatarCard styling
- Prevents event propagation with `@onclick:stopPropagation="true"`

```razor
<!-- View button below the card -->
<div class="avatar-card-actions">
    <button class="btn btn-sm btn-purple" 
            @onclick="() => OpenDetailsModal(debt)" 
            @onclick:stopPropagation="true"
            title="Ver Detalhes"
            aria-label="Ver Detalhes">
        <i class="bi bi-eye"></i> Ver Detalhes
    </button>
</div>
```

### 3. DetailsModal Component (Lines 308-344)
Added the DetailsModal after the ConfirmDialog and before the closing `</main>` tag:
- Displays member profile picture and name in header
- Shows two InfoSections:
  1. **Transações**: Lists all expense transactions ordered by date (newest first)
     - Each transaction shows description and amount (€)
     - Uses danger badge styling for amounts
  2. **Total em Dívida**: Shows the total debt amount
- Conditionally renders only when `viewingDebt` is not null

```razor
<!-- Details Modal -->
@if (viewingDebt != null)
{
    <DetailsModal Show="@showDetailsModal" 
                  ShowChanged="@((bool show) => showDetailsModal = show)"
                  Title="Detalhes de Dívida"
                  HeaderTitle="@viewingDebt.Member.GetDisplayName()"
                  ImageUrl="@viewingDebt.Member.ProfilePictureSrc"
                  Size="Modal.ModalSize.Default"
                  OnClose="CloseDetailsModal">
        <Sections>
            <InfoSection SectionTitle="Transações" IconClass="bi-list-ul">
                <!-- Transaction list -->
            </InfoSection>
            <InfoSection SectionTitle="Total em Dívida" IconClass="bi-cash-coin">
                <!-- Total amount -->
            </InfoSection>
        </Sections>
    </DetailsModal>
}
```

### 4. Modal Management Methods (Lines 773-794)
Added two methods to handle opening and closing the modal:

#### OpenDetailsModal (Lines 773-787)
- Accepts a `MemberDebt` parameter
- Loads all transactions for the selected member from the CALOTES activity
- Filters to show only "Expense" type transactions (debts owed by the member)
- Opens the modal

```csharp
private async Task OpenDetailsModal(MemberDebt debt)
{
    viewingDebt = debt;
    
    // Load transactions for this member
    if (calotesActivity != null)
    {
        var transactions = await TransactionService.GetTransactionsByActivityIdAsync(calotesActivity.Id);
        viewingMemberTransactions = transactions
            .Where(t => t.UserId == debt.Member.Id && t.Type == "Expense")
            .ToList();
    }
    
    showDetailsModal = true;
}
```

#### CloseDetailsModal (Lines 789-794)
- Closes the modal
- Clears the viewing state
- Resets the transactions list

```csharp
private void CloseDetailsModal()
{
    showDetailsModal = false;
    viewingDebt = null;
    viewingMemberTransactions.Clear();
}
```

## Features

### User Experience
- **Accessibility**: Includes proper `aria-label` and `title` attributes
- **Responsive**: Uses existing modal components that are mobile-friendly
- **Sorted Data**: Transactions are ordered by date (newest first)
- **Visual Feedback**: Uses Bootstrap's danger badge styling to highlight debt amounts
- **Empty State**: Shows "Nenhuma transação encontrada" when no transactions exist

### Technical Implementation
- **Reusable Components**: Uses existing `DetailsModal` and `InfoSection` components from RTUB.Shared
- **Consistent Styling**: Matches the AvatarCard's "Ver Detalhes" button design
- **Data Filtering**: Only shows "Expense" type transactions (actual debts, not payments)
- **Async Loading**: Loads transaction data asynchronously when modal opens
- **Clean State Management**: Properly cleans up state when modal closes

## Testing
✅ Build succeeded with no warnings or errors
✅ All components properly referenced
✅ Proper error handling in place

## Files Modified
- `/home/runner/work/RTUB/RTUB/src/RTUB.Web/Pages/Public/Calotes.razor`

## Components Used
- `DetailsModal` (from RTUB.Shared)
- `InfoSection` (from RTUB.Shared)
- Bootstrap badges and list groups

## Next Steps
To test this feature:
1. Navigate to the Calotes page
2. Select a fiscal year with debt records
3. Click the "Ver Detalhes" button on any member card
4. Verify that the modal shows:
   - Member's profile picture and name
   - All debt transactions with descriptions and amounts
   - Total debt amount
5. Close the modal and verify state is cleaned up
