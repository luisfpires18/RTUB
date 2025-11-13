using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using System.Globalization;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing browser push notifications
/// Handles notification logic for sending browser notifications to subscribed users
/// </summary>
public class BrowserNotificationService : IBrowserNotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public BrowserNotificationService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>
    /// Send a browser notification for a new event to subscribed users
    /// Returns information about who should receive the notification
    /// The actual notification is sent via SignalR from the Web layer
    /// </summary>
    public async Task<(bool success, int count)> SendNewEventNotificationAsync(
        int eventId, 
        string eventName, 
        DateTime eventDate)
    {
        try
        {
            // Get all users who are subscribed to notifications
            var subscribedUsers = await _userManager.Users
                .Where(u => u.Subscribed && u.EmailConfirmed)
                .ToListAsync();

            if (!subscribedUsers.Any())
            {
                return (true, 0);
            }

            // Return success with count of subscribed users
            // The actual SignalR notification sending happens in the Web layer
            return (true, subscribedUsers.Count);
        }
        catch (Exception ex)
        {
            // Log the exception (in a real scenario, use ILogger)
            Console.WriteLine($"Error in SendNewEventNotificationAsync: {ex.Message}");
            return (false, 0);
        }
    }
}
