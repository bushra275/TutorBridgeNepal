# TutorBridge Nepal

TutorBridge Nepal is a web platform that connects students with local tutors across Nepal. Built as a Final Year Project using ASP.NET Core MVC.

## Features

- **Role-based accounts** — Students, Tutors, and Admins each get their own dashboard and permissions
- **Tutor discovery** — students can browse and search for tutors by subject, district, and rate
- **Booking system** — students can book available time slots with tutors
- **Tutor profiles** — experience, subjects taught, hourly rate, and ratings
- **Admin verification** — admins can verify tutor accounts before they go live
- **Secure authentication** — powered by ASP.NET Core Identity, including two-factor authentication support
- **Password recovery** — self-service "forgot password" flow

## Tech Stack

- **Framework:** ASP.NET Core MVC (.NET 8)
- **Database:** SQL Server (LocalDB for development), Entity Framework Core
- **Auth:** ASP.NET Core Identity
- **Frontend:** Razor Views, Bootstrap, jQuery

## Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- SQL Server LocalDB (comes with Visual Studio) or a full SQL Server instance
- Visual Studio 2022 (recommended) or VS Code

### Setup

1. Clone the repository
   ```
   git clone https://github.com/yourusername/TutorBridgeNepal.git
   ```
2. Open `TutorBridgeNepal.slnx` in Visual Studio, or `cd` into the `TutorBridgeNepal` folder
3. Update the connection string in `appsettings.Development.json` if needed
4. Apply database migrations
   ```
   dotnet ef database update
   ```
5. Run the project
   ```
   dotnet run
   ```
6. Open the app at `https://localhost:7192` (or the URL shown in your terminal)

### Default seeded accounts

The project seeds a demo admin account and several sample tutor accounts on first run (see `Data/DbSeeder.cs`). These use simple placeholder passwords intended for local development only — change them before deploying anywhere publicly accessible.

## Project Structure

```
TutorBridgeNepal/
├── Controllers/     # MVC controllers (Account, Student, Tutor, Admin, etc.)
├── Models/          # Entity models
├── ViewModels/      # View-specific models
├── Views/           # Razor views
├── Data/            # DbContext and database seeding
├── Migrations/      # EF Core migrations
└── wwwroot/         # Static assets (CSS, JS, libraries)
```

## Status

This project is under active development as part of a Final Year Project (FYP).

## License

This project is for academic purposes as part of a Final Year Project.
