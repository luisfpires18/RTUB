// MeetingsKit: payload builders and read/write sweeps used by the meetings tests.
using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;
using static RTUB.Integration.Tests.Api.MeetingsKit;

namespace RTUB.Integration.Tests.Api;
internal partial class MeetingsKit
{
    // ---------- bodies ----------

    public static string Tag() => Guid.NewGuid().ToString("N")[..12];

    /// <summary>A form date-time ("yyyy-MM-ddTHH:mm"), <paramref name="days"/> from today.</summary>
    public static string Date(int days, int hour = 21) =>
        DateTime.Today.AddDays(days).AddHours(hour).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    public static object NewMeeting(string type, string? title = null, int days = 30, string? statement = "Ordem de trabalhos",
        string? location = "Sede da RTUB", string? representativeId = null) => new
        {
            type,
            title = title ?? $"Reunião {Tag()}",
            date = Date(days),
            location,
            statement,
            tunoRepresentativeId = representativeId,
        };

    public static object NewRequest(string type, string? title = null, string? description = "Assuntos a tratar") => new
    {
        type,
        title = title ?? $"Pedido {Tag()}",
        proposedDate = Date(15),
        location = "Sede da RTUB",
        description,
    };

    public static object NewAta(bool assembly, string? firstSecretaryId = null, string? secondSecretaryId = null) => new
    {
        ataNumber = "1/2026",
        actualStartTime = Date(-1, 21),
        actualEndTime = Date(-1, 23),
        location = "Sede da RTUB",
        quorumBasis = assembly ? "HoraAgendada" : null,
        firstSecretaryId,
        secondSecretaryId,
        agendaPoints = new[]
        {
            new { title = "Aprovação das contas", discussion = "Contas do ano.", decision = "Aprovadas.", votesFor = 12, votesAgainst = 0, votesAbstain = 1, result = "Aprovado" },
        },
        closingText = "Nada mais havendo a tratar, encerrou-se a sessão.",
    };

    // ---------- every call ----------

    /// <summary>Every GET, with the status it answered; each must say Cache-Control: no-store.</summary>
    public static async Task<Dictionary<string, HttpStatusCode>> ReadsAsync(HttpClient client, int id)
    {
        var statuses = new Dictionary<string, HttpStatusCode>();
        foreach (var path in new[]
                 {
                     "/api/meetings", "/api/meetings?fy=all", $"/api/meetings/{id}", "/api/meetings/form", $"/api/meetings/{id}/cancel",
                     $"/api/meetings/{id}/email", $"/api/meetings/{id}/push", $"/api/meetings/{id}/participants",
                     $"/api/meetings/{id}/participants/candidates?q=a", $"/api/meetings/{id}/ata", $"/api/meetings/{id}/ata/edit",
                     $"/api/meetings/{id}/ata/pdf", "/api/meetings/requests",
                 })
        {
            var response = await client.GetAsync(path);
            response.Headers.CacheControl!.NoStore.Should().BeTrue("{0} is never cached", path);
            statuses[path] = response.StatusCode;
        }

        return statuses;
    }

    /// <summary>Every write on one meeting, each with a well-formed body.</summary>
    public static Task<Dictionary<string, HttpStatusCode>> MeetingWritesAsync(HttpClient client, int id) =>
        SendAllAsync(new (string, Func<Task<HttpResponseMessage>>)[]
        {
            ($"PUT /{id}", () => client.PutAsJsonAsync($"/api/meetings/{id}", NewMeeting(Ago))),
            ($"DELETE /{id}", () => client.DeleteAsync($"/api/meetings/{id}")),
            ($"POST /{id}/cancel", () => client.PostAsJsonAsync($"/api/meetings/{id}/cancel", new { reason = "Motivo", notifyByEmail = false })),
            ($"POST /{id}/uncancel", () => client.PostAsync($"/api/meetings/{id}/uncancel", null)),
            ($"POST /{id}/email/preview", () => client.PostAsJsonAsync($"/api/meetings/{id}/email/preview", new { body = "Corpo" })),
            ($"POST /{id}/email", () => client.PostAsJsonAsync($"/api/meetings/{id}/email", new { subject = "Assunto", body = "Corpo" })),
            ($"POST /{id}/push", () => client.PostAsJsonAsync($"/api/meetings/{id}/push", new { message = "Mensagem" })),
            ($"PUT /{id}/participation", () => client.PutAsJsonAsync($"/api/meetings/{id}/participation", new { willAttend = true })),
            ($"POST /{id}/participants", () => client.PostAsJsonAsync($"/api/meetings/{id}/participants", new { userId = "ninguem" })),
            ($"DELETE /{id}/participants/1", () => client.DeleteAsync($"/api/meetings/{id}/participants/1")),
            ($"PUT /{id}/ata", () => client.PutAsJsonAsync($"/api/meetings/{id}/ata", NewAta(assembly: true))),
            ($"POST /{id}/ata/publish", () => client.PostAsync($"/api/meetings/{id}/ata/publish", null)),
            ($"POST /{id}/ata/confirmation", () => client.PostAsJsonAsync($"/api/meetings/{id}/ata/confirmation", new { confirm = true })),
        });

    /// <summary>Creating a meeting, proposing one, and every write on one request.</summary>
    public static Task<Dictionary<string, HttpStatusCode>> OtherWritesAsync(HttpClient client, int requestId) =>
        SendAllAsync(new (string, Func<Task<HttpResponseMessage>>)[]
        {
            ("POST /", () => client.PostAsJsonAsync("/api/meetings", NewMeeting(Ago))),
            ("POST /requests", () => client.PostAsJsonAsync("/api/meetings/requests", NewRequest(Cv))),
            ($"POST /requests/{requestId}/accept", () => client.PostAsync($"/api/meetings/requests/{requestId}/accept", null)),
            ($"POST /requests/{requestId}/reject", () => client.PostAsync($"/api/meetings/requests/{requestId}/reject", null)),
            ($"DELETE /requests/{requestId}", () => client.DeleteAsync($"/api/meetings/requests/{requestId}")),
            ($"POST /requests/{requestId}/reminder", () => client.PostAsync($"/api/meetings/requests/{requestId}/reminder", null)),
        });

    /// <summary>Every call answered <paramref name="expected"/>; the message names the ones that did not.</summary>
    public static void AllAnswer(Dictionary<string, HttpStatusCode> statuses, HttpStatusCode expected)
    {
        statuses.Should().NotBeEmpty();
        statuses.Where(s => s.Value != expected).Select(s => $"{s.Key} answered {(int)s.Value}")
            .Should().BeEmpty("every call answers {0}", (int)expected);
    }

    private static async Task<Dictionary<string, HttpStatusCode>> SendAllAsync((string Name, Func<Task<HttpResponseMessage>> Send)[] calls)
    {
        var statuses = new Dictionary<string, HttpStatusCode>();
        foreach (var (name, send) in calls)
        {
            statuses[name] = (await send()).StatusCode;
        }

        return statuses;
    }
}
