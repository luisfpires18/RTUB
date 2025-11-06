# RTUB - Real Tuna Universitária de Bragança

A modern **Blazor Web Application** for managing and promoting the Real Tuna Universitária de Bragança, a traditional Portuguese university music group.

## 📋 Table of Contents

- [About](#about)
- [Architecture](#architecture)
- [Technologies](#technologies)
- [Features](#features)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Development](#development)
- [Testing](#testing)
- [Deployment](#deployment)
- [Contributing](#contributing)
- [License](#license)

## 🎵 About

RTUB (Real Tuna Universitária de Bragança) is a comprehensive web platform built with **Blazor** that serves as the digital hub for the university's traditional tuna music group. This interactive application provides tools for managing members, events, performances, repertoire, rehearsals, inventory, and financial operations while engaging with the community through a modern, responsive interface.

## 🏗️ Architecture

The project follows a **Clean Architecture** pattern with clear separation of concerns:

```
RTUB/
├── src/
│   ├── RTUB.Core/          # Domain entities and business logic
│   ├── RTUB.Application/   # Application services and use cases
│   ├── RTUB.Shared/        # Shared utilities and common code
│   └── RTUB.Web/           # Blazor Web App (Interactive Server)
└── tests/                  # Unit and integration tests
```

### Architecture Layers

- **RTUB.Core**: Contains domain models, entities, enums, and core business rules
- **RTUB.Application**: Implements application services, DTOs, interfaces, and business logic orchestration
- **RTUB.Shared**: Houses shared resources, constants, helpers, and utilities used across projects
- **RTUB.Web**: **Blazor Web App** with Interactive Server components providing the user interface

## 🛠️ Technologies

### Core Framework
- **Blazor Web App (Interactive Server)** - Modern web UI framework with C#
- **.NET 8+** - Latest .NET platform
- **ASP.NET Core** - Web framework foundation
- **Entity Framework Core** - ORM for database operations
- **SQLite** - Lightweight database for data persistence

### Language Composition
- **C#** - Backend development, Blazor components, and business logic
- **HTML** - Razor markup in Blazor components
- **CSS** - Styling and visual design
- **JavaScript** - Minimal JS for specific client-side features

### Key Technologies & Libraries

#### Frontend
- **Blazor Interactive Server** - Real-time UI updates via SignalR
- **Razor Components** - Component-based UI architecture
- **Bootstrap** - Responsive CSS framework
- **SignalR** - Real-time communication between server and client

#### Backend & Services
- **ASP.NET Core Identity** - Authentication and authorization
- **QuestPDF** - PDF report generation
- **Response Compression** (Brotli/Gzip) - Performance optimization
- **Response Caching** - Server-side caching for better performance
- **Memory Cache** - In-memory caching to reduce database queries

#### Database & Migrations
- **SQLite** - Primary database
- **Entity Framework Core Migrations** - Database version control
- **Automatic Migration & Seeding** - Database initialization on startup

## ✨ Features

### Core Functionalities

#### 🎭 Event Management
- Create, schedule, and manage performances and events
- Track event details, locations, and attendance
- Event repertoire assignment
- Event status tracking

#### 👥 Member Management
- Member registration and profile management
- Role assignment (Admin, Member, Visitor)
- User profiles with photos and contact information
- Last login tracking

#### 🎼 Music & Repertoire
- Song library management
- Event repertoire planning
- Music categorization and organization

#### 📅 Rehearsal Management
- Schedule rehearsals
- Track rehearsal attendance
- Monitor member participation

#### 📸 Media & Gallery
- Album management with photo galleries
- Image upload and optimization
- Slideshow capabilities for public display

#### 📊 Financial Management
- Transaction tracking (income/expenses)
- Fiscal year management
- Financial reports and summaries
- Budget monitoring

#### 🎺 Inventory Management
- Musical instrument tracking
- Product/merchandise inventory
- Trophy and award management
- Equipment status monitoring

#### 📋 Request System
- Member request submission
- Request approval workflow
- Status tracking

#### 📧 Communication
- Email notification system
- Automated email for account management
- Event notifications

#### 📄 Reports
- PDF report generation (QuestPDF)
- Custom report templates
- Event summaries and financial reports

#### 🔐 Security & Authentication
- ASP.NET Core Identity integration
- Role-based authorization (Admin/Member/Visitor)
- Email confirmation required
- Secure password requirements
- Anti-forgery protection
- Cascading authentication state

#### 📱 Labels & Organization
- Custom labeling system
- Categorization and filtering

#### 🔍 Audit Logging
- Track user actions and changes
- System activity monitoring
- Compliance and accountability

### User Roles

- **Owner**: Full system access, audit logging and user roles tracking
- **Administrator**: Full entity management, configuration
- **Member**: Access to member features, event participation, rehearsals
- **Visitor**: Public access to general information, events, and media gallery

## 📁 Project Structure

```
RTUB/
├── src/
│   ├── RTUB.Core/
│   │   ├── Entities/          # Domain models
│   │   └── Enums/             # Enumerations
│   │
│   ├── RTUB.Application/
│   │   ├── Data/              # DbContext and data access
│   │   ├── Interfaces/        # Service contracts
│   │   └── Services/          # Business logic implementation
│   │
│   ├── RTUB.Shared/
│   │   └── [Common utilities and helpers]
│   │
│   └── RTUB.Web/
│       ├── Components/        # Blazor components
│       ├── Pages/             # Blazor pages/routes
│       ├── Controllers/       # API controllers
│       ├── Shared/            # Shared Blazor components
│       ├── wwwroot/           # Static files (CSS, JS, images)
│       ├── App.razor          # Root component
│       ├── _Imports.razor     # Global using statements
│       ├── Program.cs         # Application entry point
│       └── appsettings.json   # Configuration
│
└── tests/                     # Test projects
```

## 🚀 Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or later
- Visual Studio 2022 / Visual Studio Code / Rider
- Git

### Installation

1. **Clone the repository:**
```bash
git clone https://github.com/luisfpires18/RTUB.git
cd RTUB
```

2. **Restore NuGet packages:**
```bash
dotnet restore
```

3. **Configure the application:**
   - Copy `appsettings.Development.json.example` to `appsettings.Development.json` (if not present)
   - Update your local `appsettings.Development.json` with your development credentials
   - The file is ignored by git to keep your secrets safe

4. **Run the application:**
```bash
cd src/RTUB.Web
dotnet run
```

5. **Access the application:**
   - Navigate to `https://localhost:5001` or `http://localhost:5000`
   - The database will be automatically migrated and seeded on first run

### Configuration

#### Local Development

For local development, sensitive configuration values should be stored in `appsettings.Development.json` (which is git-ignored):

```json
{
  "AdminUser": {
    "Username": "your-username",
    "Email": "your-email@example.com",
    "Password": "YourPassword123!"
  },
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "SenderEmail": "sender@example.com",
    "SenderName": "RTUB"
  },
  "IDrive": {
    "Endpoint": "s3.endpoint.example.com",
    "Bucket": "your-bucket",
    "AccessKey": "your-access-key",
    "SecretKey": "your-secret-key"
  }
}
```

#### Production Deployment

For production, **do not include credentials in JSON files**. Instead, use environment variables:

**SMTP Configuration:**
- `EmailSettings__SmtpUsername` - SMTP username
- `EmailSettings__SmtpPassword` - SMTP password
- `EmailSettings__SenderEmail` - Sender email address

**Admin User Configuration:**
- `AdminUser__Username` - Default admin username
- `AdminUser__Email` - Default admin email
- `AdminUser__Password` - Default admin password

**IDrive/S3 Configuration:**
- `IDrive__AccessKey` - S3-compatible storage access key
- `IDrive__SecretKey` - S3-compatible storage secret key
- `IDrive__Endpoint` - S3-compatible storage endpoint
- `IDrive__Bucket` - Storage bucket name

The configuration system follows the standard ASP.NET Core hierarchy:
1. `appsettings.json` (base settings)
2. `appsettings.{Environment}.json` (environment-specific settings)
3. Environment variables (highest priority - overrides all)

### Default Configuration

The application uses SQLite by default with connection string:
```
Data Source=app.db
```

The database is automatically:
- Created on first run
- Migrated to the latest schema
- Seeded with initial data

## 💻 Development

### Building the Solution

```bash
# Build all projects
dotnet build

# Build in Release mode
dotnet build -c Release

# Build specific project
dotnet build src/RTUB.Web
```

### Running in Development Mode

```bash
cd src/RTUB.Web
dotnet watch run
```

This enables hot reload for Blazor components and C# code during development.

### Development Features

- **Detailed Errors**: Enabled in development mode
- **Hot Reload**: Automatic UI updates on code changes
- **SignalR Debugging**: Detailed SignalR errors in development
- **HTTPS Redirection**: Automatically configured

### Code Style

The project follows standard C# coding conventions:
- PascalCase for public members
- camelCase for private fields
- Async suffix for asynchronous methods
- XML documentation for public APIs
- Clean code principles

## 🧪 Testing

Run all tests:
```bash
dotnet test
```

Run tests with coverage:
```bash
dotnet test /p:CollectCoverage=true
```

Run specific test project:
```bash
dotnet test tests/[YourTestProject]
```

## 🚀 Deployment

### Azure Deployment

The project includes Azure deployment configuration (`.deployment` file).

#### Prerequisites
- Azure App Service
- SQL Server or continue using SQLite

#### Deployment Steps

1. **Publish the application:**
```bash
dotnet publish src/RTUB.Web -c Release -o ./publish
```

2. **Configure Azure App Service:**
   - Set connection strings in Configuration
   - Configure environment variables
   - Enable HTTPS only

3. **Deploy:**
   - Use Azure CLI, GitHub Actions, or Visual Studio publish
   - The application will auto-migrate the database on startup

### Production Configuration

Update `appsettings.Production.json`:
```json
{
  "ConnectionStrings": {
    "SqliteConnection": "Data Source=/path/to/production/app.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  }
}
```

### Performance Optimization

The application includes:
- **Response Compression** (Brotli/Gzip)
- **Response Caching** for static files (30 days in production)
- **Memory Caching** for frequently accessed data
- **SignalR optimization** for Blazor Interactive Server
- **Static file caching** with cache headers

## 🤝 Contributing

Contributions are welcome! Please follow these steps:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

### Contribution Guidelines

- Follow the existing code style
- Write meaningful commit messages
- Add tests for new features
- Update documentation as needed
- Ensure all tests pass before submitting PR

## 📝 License

This project is developed for the Real Tuna Universitária de Bragança.

## 👥 Contact

**Real Tuna Universitária de Bragança**

Project Maintainer: [@luisfpires18](https://github.com/luisfpires18)

Project Link: [https://github.com/luisfpires18/RTUB](https://github.com/luisfpires18/RTUB)

Project Live: [https://rtub.azurewebsites.net/](https://rtub.azurewebsites.net/)

---

**Built with Blazor** 🚀 | Made for the Real Tuna Universitária de Bragança 🎵
