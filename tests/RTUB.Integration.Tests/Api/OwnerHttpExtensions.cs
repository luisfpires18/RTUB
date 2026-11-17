using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;
using Xunit;

namespace RTUB.Integration.Tests.Api;

internal static class OwnerHttpExtensions
{
    /// <summary>The body whatever the status (a refusal's problem JSON included).</summary>
    public static async Task<string> GetStringAsyncAllowingErrors(this HttpClient client, string path) =>
        await (await client.GetAsync(path)).Content.ReadAsStringAsync();
}
