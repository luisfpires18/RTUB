// Kanban Board Drag and Drop functionality
let dotNetRef = null;
let draggedCard = null;
let draggedCardOriginalParent = null;
let draggedCardOriginalNextSibling = null;

window.initializeKanbanDragDrop = function (dotNetReference) {
    dotNetRef = dotNetReference;
    setupDragAndDrop();
};

function setupDragAndDrop() {
    // Add event listeners to all cards
    document.querySelectorAll('.kanban-card').forEach(card => {
        card.addEventListener('dragstart', handleDragStart);
        card.addEventListener('dragend', handleDragEnd);
    });

    // Add event listeners to all card containers
    document.querySelectorAll('.kanban-cards').forEach(container => {
        container.addEventListener('dragover', handleDragOver);
        container.addEventListener('drop', handleDrop);
        container.addEventListener('dragenter', handleDragEnter);
        container.addEventListener('dragleave', handleDragLeave);
    });
}

function handleDragStart(e) {
    draggedCard = this;
    draggedCardOriginalParent = this.parentElement;
    draggedCardOriginalNextSibling = this.nextElementSibling;
    
    this.classList.add('dragging');
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/html', this.innerHTML);
}

function handleDragEnd(e) {
    this.classList.remove('dragging');
    
    // Remove all drag-over classes
    document.querySelectorAll('.kanban-cards').forEach(container => {
        container.classList.remove('drag-over');
    });

    // Check if the card was actually moved
    if (draggedCard && draggedCard.parentElement !== draggedCardOriginalParent) {
        const cardId = parseInt(draggedCard.dataset.cardId);
        const newListId = parseInt(draggedCard.parentElement.dataset.listId);
        const cards = Array.from(draggedCard.parentElement.children);
        const newPosition = cards.indexOf(draggedCard);

        // Call the Blazor component method to update the database
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnCardMoved', cardId, newListId, newPosition)
                .catch(error => {
                    console.error('Error moving card:', error);
                    // Revert the move on error
                    if (draggedCardOriginalNextSibling) {
                        draggedCardOriginalParent.insertBefore(draggedCard, draggedCardOriginalNextSibling);
                    } else {
                        draggedCardOriginalParent.appendChild(draggedCard);
                    }
                });
        }
    }

    draggedCard = null;
    draggedCardOriginalParent = null;
    draggedCardOriginalNextSibling = null;
}

function handleDragOver(e) {
    if (e.preventDefault) {
        e.preventDefault();
    }
    e.dataTransfer.dropEffect = 'move';

    const afterElement = getDragAfterElement(this, e.clientY);
    if (afterElement == null) {
        this.appendChild(draggedCard);
    } else {
        this.insertBefore(draggedCard, afterElement);
    }

    return false;
}

function handleDrop(e) {
    if (e.stopPropagation) {
        e.stopPropagation();
    }
    this.classList.remove('drag-over');
    return false;
}

function handleDragEnter(e) {
    this.classList.add('drag-over');
}

function handleDragLeave(e) {
    // Only remove the class if we're leaving the container itself, not a child
    if (e.target === this) {
        this.classList.remove('drag-over');
    }
}

function getDragAfterElement(container, y) {
    const draggableElements = [...container.querySelectorAll('.kanban-card:not(.dragging)')];

    return draggableElements.reduce((closest, child) => {
        const box = child.getBoundingClientRect();
        const offset = y - box.top - box.height / 2;

        if (offset < 0 && offset > closest.offset) {
            return { offset: offset, element: child };
        } else {
            return closest;
        }
    }, { offset: Number.NEGATIVE_INFINITY }).element;
}

// Re-initialize when the page updates (for Blazor re-renders)
if (typeof MutationObserver !== 'undefined') {
    const observer = new MutationObserver((mutations) => {
        mutations.forEach((mutation) => {
            if (mutation.addedNodes.length > 0) {
                setupDragAndDrop();
            }
        });
    });

    // Observe the document body for changes
    const targetNode = document.body;
    if (targetNode) {
        observer.observe(targetNode, { childList: true, subtree: true });
    }
}
