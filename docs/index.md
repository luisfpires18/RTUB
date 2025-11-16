# RTUB Documentation

Welcome to the RTUB (Real Tuna Universitária de Bragança) technical documentation.

## Quick Links

- **[Components Guide](components.md)** - Documentation for all reusable components in RTUB.Shared
- **[Pages Reference](pages.md)** - Complete list of all Razor pages with routes, descriptions, and permissions
- **[Authentication & Business Rules](auth-and-rules.md)** - Understanding roles, categories, positions, and business logic
- **[Contributing Guide](contributing.md)** - Guidelines for developers and agents working on RTUB

## Overview

RTUB is a **Blazor Web Application** (Interactive Server) built with .NET 8+ that manages the Real Tuna Universitária de Bragança, a traditional Portuguese university music group ("tuna" in Portuguese).

### Architecture

The solution follows **Clean Architecture** principles:

- **RTUB.Core** - Domain entities, enums, and core business rules
- **RTUB.Application** - Application services, interfaces, and business logic
- **RTUB.Shared** - Reusable Razor components, utilities, and shared resources
- **RTUB.Web** - Blazor Web App with pages, API controllers, and configuration

### Key Features

- Member and event management
- Music repertoire and albums
- Rehearsal tracking with attendance
- Financial management (transactions, budgets, reports)
- Inventory (instruments, products, trophies)
- Role-based authorization (Owner, Admin, Member, Visitor)
- Media gallery with album and slideshow support
- Leaderboard/ranking system (XP and levels)
- Request submission and approval workflow
- Email notifications
- Audit logging
- PDF report generation

### Technology Stack

- **Blazor Interactive Server** (ASP.NET Core)
- **Entity Framework Core** with SQLite
- **ASP.NET Core Identity** for authentication
- **Bootstrap** for UI styling
- **QuestPDF** for PDF generation
- **SignalR** for real-time updates

## Getting Started

For setup instructions, see the main [README.md](../README.md) in the repository root.

For development guidelines, see [Contributing Guide](contributing.md).
