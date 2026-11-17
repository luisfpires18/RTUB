// Logistics: card checklist items.
using System.Security.Claims;
using System.Text.Json;
using RTUB.Application.DTOs;

namespace RTUB.Application.Services;

public sealed partial class LogisticsKanbanService
{
    public Task<EventResult<LogisticsCardDetailDto>> AddChecklistItemAsync(int id, LogisticsChecklistItemInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(id, user, assignedMayChange: true, (db, card, access) =>
        {
            var items = Tasks(card.ChecklistJson);
            if (TaskText(input.Task) is not { } task)
            {
                return Bad("task", "A tarefa precisa de texto (máx. 200 caracteres).");
            }

            if (items.Count >= MaxChecklistItems)
            {
                return Bad("task", $"No máximo {MaxChecklistItems} tarefas.");
            }

            items.Add(new StoredTask(0, task, false));
            card.SetChecklist(JsonSerializer.Serialize(items));
            Log(db, card.Id, access, "checklist", $"Tarefa adicionada: {Cut(task)}");
            return Fine();
        });

    public Task<EventResult<LogisticsCardDetailDto>> RenameChecklistItemAsync(int id, int index, LogisticsChecklistItemInput input, ClaimsPrincipal user) =>
        ChangeCardAsync(id, user, assignedMayChange: true, (db, card, access) =>
        {
            var items = Tasks(card.ChecklistJson);
            if (index < 0 || index >= items.Count || items[index].Task != input.Expected)
            {
                return Bad("items", ChecklistMoved);
            }

            if (TaskText(input.Task) is not { } task)
            {
                return Bad("task", "A tarefa precisa de texto (máx. 200 caracteres).");
            }

            if (task != items[index].Task)
            {
                Log(db, card.Id, access, "checklist", $"Tarefa editada: «{Cut(items[index].Task)}» → «{Cut(task)}»");
                items[index] = items[index] with { Task = task };
                card.SetChecklist(JsonSerializer.Serialize(items));
            }

            return Fine();
        });

    public Task<EventResult<LogisticsCardDetailDto>> RemoveChecklistItemAsync(int id, int index, string? expected, ClaimsPrincipal user) =>
        ChangeCardAsync(id, user, assignedMayChange: true, (db, card, access) =>
        {
            var items = Tasks(card.ChecklistJson);
            if (index < 0 || index >= items.Count || items[index].Task != expected)
            {
                return Bad("items", ChecklistMoved);
            }

            Log(db, card.Id, access, "checklist", $"Tarefa removida: {Cut(items[index].Task)}");
            items.RemoveAt(index);
            card.SetChecklist(JsonSerializer.Serialize(items));
            return Fine();
        });

    // ---------------------------------------------------------------- discussion
}