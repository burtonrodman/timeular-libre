## Plan: New Timeular Architecture

**TL;DR**  
The existing console app will be refactored into a **core library** plus two new front‑ends:

1. **Windows service (`Timeular.Service`)** – runs in the background, keeps the cube connected, fires events and, on each flip, opens the system browser to a *fixed* cloud URL with the action name.  
2. **Web interface (`Timeular.Web`)** – an ASP .NET Core site hosted remotely. The service’s browser launch lands here; the page lets the user either record the action or query Azure DevOps work‑items.

The core library holds all BLE/device logic, configuration models and event logging; it exposes events so that any host (service, CLI, tests) can react. Configuration can be pulled from a remote API.

---

### Steps

1. **Add new projects**  
   - `Timeular.Core` (class library)  
   - `Timeular.Service` (worker service template with `Microsoft.Extensions.Hosting.WindowsServices`)  
   - `Timeular.Web` (ASP .NET Core MVC/Razor or Minimal API project)

2. **Refactor existing code into `Timeular.Core`**  
   - Move `TimeularConfig`, `EventLog`, `EventLogger` into the library.  
   - Extend `TimeularConfig` with `Dictionary<int,string> SideActions` and `string WebInterfaceUrl`.  
   - Introduce interfaces and types:  
     - `ICubeListener` / `IFlipService` with `Task StartAsync(CancellationToken)` and `event EventHandler<FlipEventArgs> FlipOccurred`.  
     - `FlipEventArgs` containing side number/label.  
     - `IConfigProvider` (local file + `HttpConfigProvider` that GETs JSON from configured endpoint).  
     - `IActionLauncher` with default implementation that `Process.Start`es the `WebInterfaceUrl?action={Uri.EscapeDataString(action)}`.  
   - Move the Bluetooth connection logic from `Program.cs` into an implementation of `ICubeListener`, preserving the Windows‑specific and InTheHand paths.  
   - Wire configuration loading and event logging into the library; expose a clean API.

3. **Implement `Timeular.Service`**  
   - Build a generic host, register `ICubeListener`, chosen `IConfigProvider` (HTTP by default), `IActionLauncher`, and `EventLogger` in DI.  
   - In `Worker` (background service):  
     1. Fetch or refresh config at startup / on timer.  
     2. Start the cube listener; subscribe to `FlipOccurred`.  
     3. On flips call `IActionLauncher.Launch(config.WebInterfaceUrl, flip.Label)`.  
     4. Log events using `EventLogger`.  
   - Configure service via `appsettings.json` / environment variables (e.g. remote config URL, polling interval).  
   - Provide a simple CLI mode for local debugging (reuse existing `Program.cs` logic).

4. **Create the cloud‑hosted web UI (`Timeular.Web`)**  
   - Route `GET /` (or `/action`) to a view that reads `?action=` query string and displays it.  
   - Offer two buttons:  
     - “Record action” → POST to `/api/log` (stub; may write to a database or emit telemetry).  
     - “Log to DevOps…” → navigates to a DevOps work‑item picker.  
   - Add pages/controllers for DevOps:  
     - Allow user to enter/select an organization/project (persist in cookie/session).  
     - Authenticate via OAuth or Personal Access Token (best‑practice guidance in README).  
     - Query the Azure DevOps REST API to list work items and display them.  
   - Keep configuration in `appsettings.json` (e.g. OAuth client ID/secret).  
   - Provide a simple data store (in‑memory or file) for recorded actions, if required.

5. **Testing & tooling**  
   - Add unit tests for `Timeular.Core` (flip detection, config providers, launcher).  
   - Add integration tests for `Timeular.Service` (mock listener, verify browser launching logic).  
   - Add basic UI tests for `Timeular.Web` (could be minimal due to scope).

6. **Documentation**  
   - Update `README.md` with architecture overview, instructions for:  
     - building/running the service,  
     - configuring the remote API/URL,  
     - deploying the web app,  
     - Azure DevOps setup.  
   - Describe the new configuration schema and remote‑config endpoint contract.

---

### Verification

- **Local**  
  - Build solution; run service in console mode, flip a cube (or simulate) and verify browser opens with `http://example.com/?action=…`.  
  - Launch web app locally, navigate to `/?action=test`, ensure UI appears and DevOps list can be fetched (with dummy credentials).

- **Deployment**  
  - Host `Timeular.Web` on a cloud service (e.g. Azure App Service) and point service config URL to it.  
  - Install `Timeular.Service` on Windows and confirm it starts as a managed service.

- **Tests**  
  - Execute unit/integration tests via `dotnet test`.

---

### Decisions

- **Separate components** – service and UI run independently; no taskbar icon.  
- **Fixed URL** – service always launches a preconfigured (but configurable) web endpoint.  
- **Cloud‑hosted UI** – remote API supplies service configuration and handles user interaction.  
- **DevOps integration** – user chooses org/project via the UI; authentication uses standard Azure DevOps REST APIs.

This plan provides a clean separation of concerns, reusable core logic, and a path toward scaling the web interface with authentication and DevOps integration. Let me know if you’d like to adjust any parts before implementation begins.
