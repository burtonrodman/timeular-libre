# OS-specific UX and Architecture Plan

The initial focus will be on a **single Windows application** that encapsulates all functionality needed for handling Timeular cube events and user interaction. The UX and architecture should support expanding to other platforms in future, but we'll start with Windows.

## Key Goals

1. **Single app per OS** (Windows first)
2. **Taskbar icon** for easy access and status indication
3. **Native window for event details**
   * Opens automatically when the cube sends an event
   * Allows user to enter or adjust details before logging
   * Includes a settings gear for device options
4. **Keyboard shortcut** (global hotkey) that launches the native window for manual/custom event entry
5. **Central log API**
   * .NET Web API project
   * Backed by SQL Server database
   * Use Entity Framework Core with code-first migrations
   * Log entries stored locally and sent to Azure DevOps (or DevOps) for telemetry

## UX Flow

- App runs in background with a taskbar icon
- When cube event received, a notification or the native window appears
- User fills in or edits event details; saves
- Entry is written to central log via API call
- Settings gear opens configuration dialog (e.g., cube device, hotspots, shortcuts)
- Shortcut allows event entry without cube interaction

## Architecture Notes

- **Windows UI**: WinForms or WPF depending on complexity (likely WPF for flexibility)
- **Global hotkey**: use Windows API (RegisterHotKey) or a library like `NHotkey`
- **Logging API**: simple .NET 10 Web API
  * EF Core models for events, device settings, users, etc.
  * Migrations managed locally
  * API hosted alongside app (self-hosted) or separately in service project
- **DevOps integration**: HTTP client writes to Azure DevOps work items or a telemetry service

## Next Steps

1. Sketch UI for native window and settings
2. Prototype taskbar icon and hotkey handling (✔️ basic WPF skeleton implemented)
3. Create central log Web API with EF Core and migration setup (✔️ project created, DbContext, migration added)
4. Wire cube listener to open window and post log entries (✔️ desktop app subscribes to flips)
5. Add shared models and remote logging support to EventLogger
6. Update service to optionally forward events to central API

*Projects added:* `Timeular.Log` (Web API), `Timeular.Desktop` (WPF UI).  
`timeular-libre.AppHost` and `timeular-libre.ServiceDefaults` remain to support Aspire orchestration; they now include the log API and desktop client.  The legacy `Timeular.Service` project has been retired and removed from the solution (its functionality moved into the UI/log API).  `Timeular.Web` and `TimeularLibre` were previously cleaned up.
*Core library extended* with remote sink support and desktop integration.


## Diagnostics

- Desktop app now writes console messages when the tray icon is initialized and whenever a cube flip is received.  This helps verify the UI is running under Aspire.


---

This document will evolve as we refine requirements and add cross-platform considerations.