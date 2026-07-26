# TapeTracker

An offline-first .NET MAUI application for tailors to record customer
measurements, generate order QR codes / PDFs, and manage bookings — all
stored locally in SQLite with zero cloud dependency.

Runs on Windows, Android, iOS, and macOS from a single codebase.

---

## Repository layout

```
TapeTracker/
├── .github/                       GitHub metadata (Copilot instructions, workflows)
├── .editorconfig                  Coding-style rules (C#, XAML, JSON, MD)
├── .gitignore                     Standard .NET / MAUI ignore list
├── FEATURES.md                    Feature roadmap
├── README.md                      This file
├── TapeTracker.sln                  Solution file
└── src/
    └── TapeTracker/                 MAUI app project
        ├── App.xaml               Application root
        ├── AppShell.xaml          Shell navigation
        ├── MauiProgram.cs         DI / service registration
        ├── GlobalXmlns.cs         Global XAML namespace mappings
        ├── TapeTracker.csproj
        ├── Converters/            Value converters (bool → color, etc.)
        ├── Data/                  EF Core DbContext & migrations
        ├── Models/                Domain entities (Customer, Order, ...)
        ├── Platforms/             Platform-specific code (Android/iOS/Windows/Mac)
        ├── Properties/            Assembly info
        ├── Resources/             Fonts, images, styles, splash, app icon
        ├── Services/              Business services (Pin, Theme, Pdf, OCR, ...)
        ├── ViewModels/            MVVM view-models (CommunityToolkit.Mvvm)
        └── Views/                 XAML pages
```

---

## Prerequisites

- **.NET SDK 9.0** or later
- **.NET MAUI workload** installed:
  ```powershell
  dotnet workload install maui
  ```
- **Windows 10 build 19041+** to run the Windows target
- **Visual Studio 2022 17.8+** (optional, but recommended for full designer support)

---

## Getting started

### Restore & build

```powershell
dotnet restore TapeTracker.sln
dotnet build   TapeTracker.sln -c Debug
```

### Run on Windows

```powershell
dotnet build src/TapeTracker.csproj -f net9.0-windows10.0.19041.0 -c Debug
.\src\bin\Debug\net9.0-windows10.0.19041.0\win10-x64\TapeTracker.exe
```

### Run on Android

Connect a device or start an emulator, then:

```powershell
dotnet build src/TapeTracker.csproj -f net9.0-android -c Debug -t:Run
```

### Run on iOS / macOS

Requires a Mac with Xcode installed:

```bash
dotnet build src/TapeTracker.csproj -f net9.0-ios -t:Run
dotnet build src/TapeTracker.csproj -f net9.0-maccatalyst -t:Run
```

---

## Architecture

- **MVVM** — [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
  with `[ObservableProperty]` (public partial properties, WinRT-AOT friendly)
  and `[RelayCommand]`.
- **Dependency Injection** — built-in `Microsoft.Extensions.DependencyInjection`
  wired up in `MauiProgram.cs`.
- **Persistence** — SQLite via EF Core; DbContext lives in `Data/`.
- **Navigation** — Shell-first (`AppShell.xaml`) with route registration for
  detail pages.
- **Localization** — custom `LocalizationService` with language files under
  `Resources/Strings`.
- **Theming** — light/dark support via `ThemeService`; palette defined in
  `Resources/Styles/Colors.xaml`.
- **PDF / QR** — SkiaSharp for generation, ZXing.Net.MAUI for scanning.
- **OCR** — Windows Media OCR on Windows, ML Kit on Android.

See [`FEATURES.md`](./FEATURES.md) for the full feature list and roadmap.

---

## Contributing

1. Ensure `dotnet build TapeTracker.sln` produces `0 Warning(s) 0 Error(s)`
   before opening a PR — warning suppression is intentional in `.csproj`.
2. Match style rules from `.editorconfig` (Visual Studio, Rider, and
   `dotnet format` all respect it).
3. Keep view-models thin — logic that isn't UI-state belongs in a service.
4. New MVVM properties should use `public partial` `[ObservableProperty]`
   pattern to keep the WinRT-AOT build clean.
