# Architecture

Detailed architectural reference for Enviro. For build commands and day-to-day conventions see [AGENTS.md](AGENTS.md).

---

## 1. Overview

Enviro is a single-process, single-window Windows Forms desktop application that edits environment variables for the current user and for the local machine. Its defining architectural idea is a **two-phase, in-memory edit model**:

1. **Edit phase** — all reads and mutations happen against in-memory `EnvModel` objects held in `BindingList<EnvModel>` collections. Nothing is written to Windows while the user creates, edits, renames or deletes variables.
2. **Apply phase** — a single explicit action (`Apply`) walks the tracked state of every model and pushes `Added`, `Modified` and `Deleted` entries to the operating system through `Environment.SetEnvironmentVariable`, then resets the model list to a clean baseline.

Everything else — the grids, the dialogs, the context menus, the validation helpers — exists to make that state-change workflow visible and safe. There is no database, no background synchronization, no file format other than a tiny `config.json`.

The application is deliberately Windows-only. It depends on the Windows registry semantics behind `Environment.SetEnvironmentVariable`, on `WindowsIdentity`/`WindowsPrincipal`, and on the Windows API Code Pack for folder/file pickers.

---

## 2. Technology stack and runtime shape

| Concern | Choice |
|---|---|
| Runtime | .NET 10 (`net10.0-windows`) |
| UI framework | Windows Forms (`UseWindowsForms`) |
| Language settings | Nullable reference types enabled, implicit usings enabled |
| Composition | `Microsoft.Extensions.DependencyInjection` |
| Shell dialogs | `WindowsAPICodePack-Core` / `WindowsAPICodePack-Shell` (`CommonOpenFileDialog`) |
| Serialization | `System.Text.Json` for `config.json` |
| Update check | `HttpClient` + GitHub REST API (`/repos/{owner}/{repo}/releases/latest`) |
| Build artifact | Self-contained single-file `win-x64` executable, assembly name `Enviro` |
| Solution | `enviro.slnx` (single project `enviro.csproj`) |

The process is a classic WinForms app: one STA UI thread, a message loop started by `Application.Run(mainForm)`, and no other threads are created by the application itself. The only asynchronous paths are the update check and the apply operation, both of which are short-lived and return to the UI thread through the WinForms synchronization context.

High-DPI handling is set once in `Program.Main` with `Application.SetHighDpiMode(HighDpiMode.SystemAware)`. Visual styles and compatible text rendering are enabled immediately afterwards.

Colour mode is also chosen once, in `Program.Main` before the first window exists: `Application.SetColorMode` is called with the persisted `ThemePreference` (`System` follows Windows, `Light` asks for `SystemColorMode.Classic`, `Dark` for `SystemColorMode.Dark`). The API is still experimental, so the `WFO5001` warning is suppressed with a local `#pragma` rather than a project-file change. WinForms applies the mode per window as each one is created, which is why a changed preference is only seen after a restart. Every themed colour comes from `Static/ThemePalette` (§10); the standard controls (menu, toolbar, status strip, tab pages, message boxes) are painted by the mode itself.

---

## 3. Project layout

```
enviro/
├── enviro.slnx                    Solution (one project)
├── global.json                    Pins .NET SDK 10.0.0 with latestMajor roll-forward
├── docs/
│   └── screenshot.png             README screenshot
└── src/enviro/                    The single project
    ├── enviro.csproj              Version + package refs live here
    ├── Program.cs                 Composition root / application entry point
    ├── Models/
│   ├── EnvModel.cs                Observable environment-variable record
│   ├── ConfigModel.cs             Observable persisted settings + the ThemePreference enum
│   ├── EnvironmentalVariableTypes.cs     User/Machine enum
│   └── EnvironmentalVariableState.cs     Unchanged/Added/Modified/Deleted
    ├── Services/
│   ├── EnvService.cs              The two scopes (Service) + one scope's rows and operations
│   ├── EnvAdapter.cs              Applies tracked states to Windows
│   ├── ApplyCoordinator.cs        Apply orchestration: pending count, adapter run, outcome text
│   ├── ConfigService.cs           config.json load/save
│   ├── VariableFileService.cs     Import/export document: snapshot, review, read, save
│   ├── CorruptedPathService.cs    Corrupted-path validation + its debounced background scans
│   ├── UpdateService.cs           GitHub release check
│   ├── AppLog.cs                  The single failure log (%APPDATA%\Enviro\enviro.log)
│   └── MetadataRepository.cs      Assembly-derived title/author/version/URLs
    ├── Factories/
│   ├── TabFactory.cs              TabPage creation
│   ├── EnvGridFactory.cs          DataGridView construction + wiring
│   ├── ContextMenuFactory.cs      Per-row ContextMenuStrip
│   ├── RowActionHelper.cs         Shared scope-guarded create/edit/delete
│   └── EntryFormFactory.cs        Create/Edit form construction
    ├── Forms/
│   ├── MainForm.cs/.Designer.cs/.resx
│   ├── AbstractEntryForm.cs/.Designer.cs/.resx   Shared name/path dialog base
│   ├── CreateForm.cs, EditForm.cs               Concrete entry dialogs
│   ├── PathEntriesControl.cs/.Designer.cs       PATH segment view inside the entry dialogs
│   ├── VariableFileForm.cs/.Designer.cs/.resx   Import/export entry review window
│   ├── UpdateForm.cs/.Designer.cs/.resx
│   └── AboutForm.cs/.Designer.cs/.resx
    ├── Static/
│   ├── AdminChecker.cs            Elevation detection + Machine-tab guard
│   ├── DialogHelper.cs            MessageBox wrappers
│   ├── ControlHelper.cs           Grid/tab introspection
│   ├── ClipboardHelper.cs         Copy model properties
│   ├── GridStyleHelper.cs         State colours + column sorting (refill via the scope)
│   ├── StateCounter.cs            Row counts by state/corruption for the status strip
│   ├── StatusPresenter.cs         The status strip's strings, from the service + config
│   ├── ThemePalette.cs            The light and dark palettes; the only place a colour is named
│   ├── ThemeArtwork.cs            Recolours the monochrome logo with the palette's ink
│   ├── ValidationHelper.cs        Input rules + corrupted-path detection
│   ├── WindowGeometry.cs          --window-bounds parsing + the screen check
│   ├── MainCommand.cs             The shell's command table (labels, shortcuts, actions, enablement)
│   └── EnvTypeConverter.cs        Enum → EnvironmentVariableTarget
    ├── Resources/                 favicon.ico, the logo image (embedded by the resx)
    └── Properties/                Resources, launch settings
```

`bin/`, `obj/`, `.vs/`, `tests/`, `enviro.csproj.user` and `config.json` are not tracked.

---

## 4. Composition root and dependency injection

`Program.Main` is the only composition root. It creates a `ServiceCollection`, registers everything, builds the provider, resolves `MainForm`, wires the exit handler, and starts the message loop. One object is created *before* the container and then registered as-is: the `AppLog`, because the unhandled-exception handlers installed in `Main` must not depend on DI (§11).

### Registrations

| Registration | Lifetime | Role |
|---|---|---|
| `IEnvService` → `EnvService` | Singleton | Authoritative in-memory variable store |
| `IContextMenuFactory` → `ContextMenuFactory` | Singleton | Context-menu construction |
| `IEnvGridFactory` → `EnvGridFactory` | Singleton | Grid construction |
| `ITabFactory` → `TabFactory` | Singleton | Tab construction |
| `IUpdateService` → `UpdateService` | Singleton | Update check |
| `IEnvAdapter` → `EnvAdapter` | Singleton | Apply-to-OS adapter |
| `ApplyCoordinator` (concrete) | Singleton | Apply orchestration: pending count, adapter run, outcome text; holds no UI |
| `IConfigService` → `ConfigService` | Singleton | Settings load/save |
| `IVariableFileService` → `VariableFileService` | Singleton | Import/export document: snapshot, review, read, save |
| `ICorruptedPathService` → `CorruptedPathService` | Singleton | Corrupted-path validation and its background scans |
| `IAppLog` → `AppLog` | Singleton | The instance `Main` built before the container; the only log funnel |
| `MetadataRepository` (concrete) | Singleton | Assembly metadata |
| `IActionFactory<EnvModel>` → `EntryFormFactory` | Singleton | Entry dialog construction |
| `Func<AboutForm>` | Singleton | Builds a transient `AboutForm`, so `MainForm` resolves nothing from the container |
| `MainForm` | Transient | Root window |
| `AboutForm` | Transient | About dialog |

Notes:

- The singleton service graph is intentionally shared: `EnvService` is created once, which is what allows grids, dialogs and the adapter to all reach the same `IEnvScope` objects and their `BindingList` rows.
- Factories are stateless apart from injected singletons, with one exception: `EnvGridFactory` holds the window's grid→filter mapping (`Dictionary<DataGridView, TextBox>`, written by `AttachFilter` and read by `FilterOf`/`ApplyFilter`). Their singleton lifetime is still safe because `MainForm` is created once per process and its grids live as long as the window, so the map cannot outlive what it points at.
- Dialogs other than `AboutForm` are **not** resolved from the container. `EntryFormFactory` receives its `IEnvService` and `ICorruptedPathService` through its constructor and calls `new CreateForm(...)` / `new EditForm(...)` directly; it holds no provider, so the dependency it gives a dialog is a compile-time one. `UpdateForm` is constructed with the fetched `UpdateResponse`.
- `AboutForm` stays transient, but `MainForm` receives a `Func<AboutForm>` factory delegate registered as `sp => () => sp.GetRequiredService<AboutForm>()`. The factory is the shell's only way to the container: `MainForm` holds the delegate and never a provider, so the service-locator dependency is gone while every About click still opens a fresh dialog. Its constructor takes the container's `MetadataRepository` and `IAppLog` singletons.
- `Program` resolves `IConfigService` explicitly before `MainForm` for two reasons: it applies the persisted theme before the first window exists (§5), and it attaches the application-exit hook:

```csharp
Application.ApplicationExit += (s, e) =>
{
    if (configManager.IsDirty) configManager.Save();
};
```

Because `ConfigService` is a singleton, this is the same instance injected into `MainForm` and `EnvGridFactory`.

### Dependency direction

```
Program
  └── Forms (MainForm, CreateForm, EditForm, AboutForm, UpdateForm)
        ├── Factories (Tab, Grid, ContextMenu, EntryForm)
        │     ├── Services (EnvService, ConfigService, ...)
        │     └── Static helpers
        ├── Services (EnvService, EnvAdapter, ApplyCoordinator, UpdateService, MetadataRepository)
        └── Models
Services ──► Models, Static
Models   ──► enums only
Static   ──► Models, the scope interface (+ WinForms/Win32 APIs)
```

There is no repository/ORM layer, no event bus, and no mediator. Communication is direct calls plus WinForms data binding and `INotifyPropertyChanged`.

---

## 5. Startup sequence

1. `Main` sets DPI mode, visual styles and compatible text rendering, then builds the `AppLog` and installs the unhandled-exception handlers (§11) — before anything can fail and before the container exists.
2. The DI container is built.
3. `ConfigService` is resolved. Its constructor calls `Init()`, which loads `%APPDATA%\Enviro\config.json`:
   - a missing, unreadable or invalid file yields the defaults (`ConfigModel { EnableCorruptedValidation = true }`) and marks the config dirty, so the next save rewrites it;
   - an absent file is therefore **not** written at startup, only on exit when dirty;
   - when the new file is absent and a legacy `config.json` sits beside the executable, that file is read once, its settings are used, and the config is marked dirty so the next save writes the new location — after that the legacy file is ignored;
   - `PropertyChanged` is subscribed on every path, so a setting change is always marked dirty.
4. `Main` applies the persisted theme. It resolves the already-built `IConfigService` (the instance the exit hook uses) and calls `Application.SetColorMode` with `ConfigModel.Theme` — `SystemColorMode.Classic` for `Light`, `Dark` for `Dark`, `System` otherwise — before `MainForm` is resolved and therefore before any window exists. A window created after this point carries the chosen mode; the choice cannot change for the rest of the process.
5. `MainForm` is resolved. Its constructor:
   - calls `InitializeComponent()` (designer-built menu strip and tab control);
   - applies the window bounds passed by an elevation restart: `Static/WindowGeometry` parses the `--window-bounds` argument and checks that the rectangle fits entirely inside one of `Screen.AllScreens`' working areas, otherwise the window is centred;
   - sets `Name` from the assembly name;
   - creates the `User` and `Machine` tabs via `ITabFactory` and adds them to `AllTab`;
   - adds the View menu's “Check for corrupted variables” checkable item, initialised from `ConfigModel.EnableCorruptedValidation`, and subscribes its toggle handler;
   - adds the Options → **Theme** submenu with the three preferences, checked from `ConfigModel.Theme`, and subscribes its handler.
6. Constructing `MainForm` resolves `IEnvService` → `EnvService` constructor → `Reload()`:
   - `Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User)` is copied into new `EnvModel` instances (`Path` = current value, `OriginalPath` = same value, `State = Unchanged`);
   - the same is done for `Machine`;
   - both scopes are read **before** either list is touched, then each list is refilled in `Name` order with notifications suppressed.
7. Tab construction triggers `EnvGridFactory.Create` for each scope, which binds a `BindingSource` to that scope's rows (`IEnvScope.Rows`) and asks `ICorruptedPathService` for that scope's initial scan (a no-op while validation is off). That request is issued before the message loop exists, so it cannot be relied on.
8. `Application.Run(mainForm)` starts the message loop. `MainForm.OnShown` then requests a scan of both scopes on the UI thread, inside the running loop — this is the request that reliably fills the corrupted overlay at startup.
9. On exit, `ConfigService.Save()` runs only when the config was marked dirty.

Reading machine variables does **not** require elevation; writing them does.

---

## 6. Domain model

### `EnvModel`

The unit of work; an observable row.

| Member | Meaning |
|---|---|
| `Name` | Variable name |
| `OriginalName` | Name at load time / last successful apply; a pending rename is written as delete-then-write from this key |
| `Path` | Current (possibly edited) value |
| `OriginalPath` | Value at load time / last successful apply |
| `State` | Edit state: `Unchanged`, `Added`, `Modified`, `Deleted` |
| `IsCorrupted` | Diagnostic overlay: the value points at a location that does not exist |
| `PropertyChanged` | Raised only when a setter actually changes the backing `field` |

The class uses the C# `field` keyword and raises `PropertyChanged` per property. It contains no behaviour beyond that; all transitions are imposed by the row's `IEnvScope`, the grid factory or the adapter.

### `IEnvScope` — one scope's rows

A scope owns its `EnvironmentalVariableType`, the `BindingList<EnvModel>` its grid binds to, and every operation bound to that scope: `Add`, `Update`, `Upsert(name, value, overwrite)`, `ApplyEdit(model, newName, newValue)`, `Import(entries)`, `Delete`, `Contains(name)`, `GetByName`, `Reset`, `Restore`, plus the two lifecycle operations `MarkApplied` (§7) and `Sort` (§10). `Upsert` and `ApplyEdit` are the whole create and edit rules; `Import` is the whole merge rule (§9.11); the entry dialogs only present them (§9.1, §9.2). `EnvService` exposes the two scopes as `User` and `Machine` and resolves one from a type with `GetScope(type)`.

The rows leave the scope through `IEnvScope.Rows` (`IBindingList`, so it keeps its `ListChanged` notifications and stays the grid's data source). They are service-owned: `EnvService`/`EnvScope` add, remove, reorder and refill them, and no consumer does. `EnvScope.Replace` (the reload refill) is not part of the interface and is only reachable from `EnvService`.

### Enums

- `EnvironmentalVariableType` — `User`, `Machine`. A single variable always belongs to exactly one of them.
- `EnvironmentalVariableState` — `Unchanged`, `Added`, `Modified`, `Deleted`. Corruption is never a state: it is the separate `IsCorrupted` overlay, so the legacy `Corrupted` member is gone.

### State machine

| From | Trigger | To |
|---|---|---|
| any | `Add` | `Added` (and `OriginalName = Name`, `OriginalPath = Path`) |
| `Unchanged` | `Update` | `Modified` |
| `Added` | `Update` | stays `Added` |
| `Deleted` | `Update` | no-op (state and value unchanged) |
| `Unchanged`/`Modified` | `Delete` | `Deleted` |
| `Modified` | `Reset` | `Unchanged` (name restored from `OriginalName`, path from `OriginalPath`) |
| `Deleted` | `Restore` | `Unchanged` (name restored from `OriginalName`) |
| `Added` | `Delete` | removed from the list entirely |
| `Unchanged` | `Upsert` (new name) | `Added` |
| `Deleted` | `Upsert` (same name) | `Modified` (restored, then takes the new value) |
| `Unchanged`/`Modified` | `Upsert` (same name, overwrite) | `Modified` |
| any | `Upsert` (live row, no overwrite) | unchanged — `Refused` returned |
| any live row / new name | `Import` (entry) | the `Upsert(..., overwrite: true)` transition (`Added` / `Modified`); a row already holding the identical value stays untouched and is counted `Unchanged` |
| `Modified` / `Deleted` | `Import` (entry matches the row's baseline name and `OriginalPath`) | `Unchanged` (name and value restored from `OriginalName`/`OriginalPath`; the pending change is cancelled) |
| `Added` | `ApplyEdit` (rename) | stays `Added`, name changes in place |
| `Unchanged`/`Modified` | `ApplyEdit` (rename) | `Modified`, name changes in place (`OriginalName` keeps the key to remove) |
| any | rejected `ApplyEdit` (blank name, collision, unknown row, `Deleted` row) | unchanged — a rejection reason is returned |
| any | corrupted-path scan | `IsCorrupted` set or cleared; `State` untouched |
| any non-deleted | successful row write (`ApplyAsync`) | `Unchanged`, `OriginalName = Name`, `OriginalPath = Path` |

The triggers in the table are the scope operations of §7: each one acts on the scope that owns the row.

Important semantics:

- `IsCorrupted` is a **diagnostic overlay**, not an edit. It never changes `State`, so `HasChanges()` and apply ignore it; a screen full of red rows does not count as pending work.
- `Deleted` models stay in the list and are rendered with strikethrough until applied; the user can restore them.
- `Added` models that are deleted again disappear immediately because nothing external needs undoing.
- Rename always edits the row in place: a persisted row becomes `Modified` (apply removes `OriginalName` and writes `Name`), a not-yet-applied `Added` row stays `Added`, and blank names, unknown rows and case-insensitive collisions are rejected without touching any model. One variable stays one row across a rename. `Rename` is private to `EnvScope`; callers reach it through `ApplyEdit`, which applies the name and the value together or applies neither.
- `Reset` and `Restore` put a row back to its baseline under its original name, so an `Unchanged` row always names a key the registry holds.
- After a successful apply, `State` returns to `Unchanged` (and `OriginalName`/`OriginalPath` are synced) row by row as each row is written; the grid's `ListChanged` handler requests a fresh corrupted-path scan, so a row only stays red while its value is still invalid.

### `ConfigModel`

An `INotifyPropertyChanged` record with two settings, `EnableCorruptedValidation` and `Theme` (a `ThemePreference`: `System`, `Light` or `Dark`, default `System`). `ConfigService` subscribes to `PropertyChanged` and flips `IsDirty`. Persisted as indented JSON at `%APPDATA%\Enviro\config.json`; the theme is written as its name, so the file stays readable:

```json
{
  "EnableCorruptedValidation": true,
  "Theme": "System"
}
```

A file without the `Theme` key reads as `System`, so an older config keeps working. The value is only read at startup (§5); changing it takes effect on the next start, because WinForms fixes the colour mode as each window is created.

Window geometry is deliberately **not** persisted: `RestartAsAdmin` passes the current bounds to the elevated process as a `--window-bounds` command-line argument, so the hand-off lives only in that process tree and cannot survive a reboot or leak into a later ordinary launch (§9.8).

---

## 7. Services in detail

### `EnvService` / `IEnvScope` — the store and its two scopes

`EnvService` owns two `IEnvScope`s, `User` and `Machine`, for the lifetime of the process; together they are the only mutable source of truth. The service itself carries only what spans both scopes:

- **Read**: `User`, `Machine`, `GetScope(type)`, `HasChanges`.
- **Reload**: `Reload` reads both scopes first, then replaces the rows in name order with fresh `Unchanged` models. A read that throws leaves the previous rows in place, and the refill raises one `Reset` notification per scope instead of one event per row.

Each `IEnvScope` owns its `Type`, its rows and every operation bound to that scope:

- **Read**: `Rows` (the `IBindingList` the grid binds to, with `ListChanged`), `Contains(name)`, `GetByName(name)`.
- **Mutate**: `Add`, `Update`, `Upsert`, `ApplyEdit`, `Delete`, `Reset`, `Restore`.
- **Create/edit rules**: `Upsert(name, value, overwrite)` returns `UpsertOutcome.Added` / `Overridden` / `Refused` and `ApplyEdit(model, newName, newValue)` returns the edited row or a rejection reason. Both resolve a whole user action in one call and show nothing; the dialogs keep validation and the one confirm prompt (§9.1, §9.2).
- **Lifecycle**: `MarkApplied(model, appliedState)` for the adapter's per-row cleanup, and `Sort(propertyName, direction)` for the grid's sort refill.

Implementation details that matter:

- `GetScope(type)` is exhaustive over `User`/`Machine` and throws `ArgumentOutOfRangeException` for any other value instead of defaulting to a scope — the same fail-closed mapping as `EnvTypeConverter.ToTarget` (§10) — so a scope is never passed around as a second parameter.
- `LoadVariables` copies one scope's OS dictionary into a fresh name-ordered `List<EnvModel>`; `EnvScope.Replace` (reachable only from `EnvService`) hands that list to the private `Refill`, which clears the bound `BindingList`, fills it with `RaiseListChangedEvents = false`, restores the flag and calls `ResetBindings()` once. The result is a snapshot: later OS changes are not observed until `Reload` runs again. Because the read happens before any list is mutated, a failing read cannot empty the grids, and listeners do their O(n) work once per refresh instead of once per row.
- `Rows` is handed out as `IBindingList`, not as a `BindingList`/copy: the grid keeps receiving `ListChanged` from the service-owned list, while adding, removing, reordering and refilling stay inside the scope. No consumer outside `EnvService` mutates the list.
- `Add` always sets `Added` and syncs `OriginalName`/`OriginalPath` to the given name and value, even if the caller passed a model that was already in another state.
- `Update` never demotes `Added` to `Modified`; an `Added` row stays `Added` through edits. It is a no-op on a `Deleted` row, so editing cannot silently cancel a deletion.
- `Upsert` takes the whole create path: a name this scope does not hold becomes an `Added` row through `Add`; a row whose deletion is still pending is restored and takes the new value; a live row takes the new value. It never prompts, and with `overwrite: false` it returns `Refused` — mutating nothing — instead of touching a row the user has not confirmed overwriting.
- `ApplyEdit` takes the whole edit path: it rejects a `Deleted` row, a blank name, a row this scope does not hold and a case-insensitive collision — with every model untouched and a rejection reason returned — and otherwise renames the row and sets the value in one step. A blank name returned before the collision check has its own reason (“The name cannot be empty”), so a non-UI caller can tell an empty name from a taken one. `Rename` stays the primitive inside it: it returns the model that now holds the new name, or `null` for a blank name, an unknown source or a target already used by another model. An accepted rename edits that single row in place: an `Added` row stays `Added`, any other row becomes `Modified`. Because `ApplyEdit` refuses `Deleted` rows, a rename can no longer replace a pending deletion from the UI side.
- `Import` merges a file's entries without a second rule path: each valid entry is an `Upsert(name, value, overwrite: true)`, so an absent name is added and an existing one is updated (a pending deletion is restored first), while a row already holding the identical value is left alone and counted `Unchanged`. An entry that matches the row's baseline — same name case-insensitively and the value in `OriginalPath` — instead cancels a pending edit or deletion through `Reset`, returning the row to `Unchanged`; it is counted `Unchanged` too, because the workspace ends up with nothing to apply. Entries rejected by `ValidationHelper.IsValidEnvironmentVariableInput` are counted `Skipped`. The batch runs with `RaiseListChangedEvents = false` and raises one `ResetBindings()`, so the cost is one refresh rather than one per row (§9.11).
- `Reset`/`Restore` restore `Name` from `OriginalName` as well as the value, so a rename is undone rather than left as an `Unchanged` row under a name the registry does not have.
- `MarkApplied` is the one place that cleans a written row: a written deletion removes the row from the list, anything else returns to `Unchanged` with `OriginalName`/`OriginalPath` synced. `EnvAdapter` calls it once per row, so a failure leaves the rows after it dirty (§7 `EnvAdapter`).
- `Sort` orders a snapshot by one model property and then refills the bound list through the same private `Refill` as `Replace` — a refresh and a sort click each raise exactly one `ResetBindings` instead of one event per row, and no sortable `BindingList` is involved (PLAN §4). `Refill` stays private to `EnvScope`, like `Replace`; the grid captures and restores its own selection around the call (§10 `GridStyleHelper`).
- Name matching in `Contains`, `GetByName` and the rename collision check is case-insensitive (`OrdinalIgnoreCase`), matching Windows variable-name semantics. A rename edits one row, so no two rows ever share a name and `GetByName` needs no preference for live rows.
- `HasChanges` inspects both scopes for any state other than `Unchanged`.

### `EnvAdapter` — apply to Windows

`IEnvAdapter` exposes a single `Task<ApplyResult> ApplyAsync()`.

`ApplyResult` carries what one run managed: how many rows were written in each scope, and — when a write threw — the scope, the `EnvModel` that failed and the exception message. `Succeeded` is true only when no row failed.

Algorithm:

1. Ask `AdminChecker.CanMutate(IEnvService.Machine.Type)` — the same rule the UI guards read — so the adapter no longer decides elevation on its own.
2. Apply user variables unconditionally.
3. Apply machine variables only when that rule allows them, and only after the user scope completed without a failure.
4. For each scope, in **Deleted** (write `null`) → **Modified** → **Added** order, write one row and immediately clean that row through `IEnvScope.MarkApplied`: a written `Deleted` row leaves the list, any other written row becomes `Unchanged` with `OriginalName`/`OriginalPath` synced. The adapter iterates `IEnvService.User` then `IEnvService.Machine` instead of two typed getters. A row whose `OriginalName` differs from its `Name` (a pending rename, including a case-only one) first has its old key deleted, so the new name replaces the old one; because modified rows are written before added ones, a variable created afterwards under the old name survives. If that new-name write then throws, the old key is written back once with `OriginalPath` (best effort) — the failure is still the run's outcome and the row stays dirty, but the rename no longer loses the variable.

The run stops at the first row that throws. Rows already written in that scope keep their clean state, the failing row and every row after it stay dirty, and the user scope stays clean when the machine scope fails. Apart from that one rename restore, nothing is rolled back, so the registry can be partially updated. The exception itself (not the restore's) is returned in the result rather than discarded, and `ApplyCoordinator` names the variable, its scope and the reason in the message `MainForm` shows. A restore that fails is logged through `IAppLog`.

The skipped machine scope is not reported separately because it cannot happen: every machine mutation is gated by `AdminChecker` (§13), so a non-elevated process never has machine rows in a non-`Unchanged` state when `Apply` runs. If that gating is ever loosened, this contract must grow a way to say "machine changes were left pending".

The method is nominally async but performs synchronous registry writes on the calling thread and returns a completed task; it exists as a task to keep the UI call site future-proof and to allow `.await` without blocking semantics at the call site.

### `ApplyCoordinator` — apply orchestration

`ApplyCoordinator(IEnvService, IEnvAdapter)` is the one place that turns an apply run into something the shell can show; it is a singleton with no UI dependency (no dialog, timer, control or form):

- `PendingChanges()` counts the rows not in `Unchanged` across both scopes. It is the number the explicit Apply command, the Refresh confirmation and the close prompt use.
- `ApplyAsync()` records the pending count, awaits `EnvAdapter.ApplyAsync()` and returns an `ApplyOutcome(bool Succeeded, string Message)`: on success the message is `Applied N changes` (the transient status-strip text), on failure it is the full failure report — failing scope, variable name, exception message, rows written before the failure (`DescribeWritten`) and the rows still pending per scope (`DescribePendingChanges`, e.g. “4 changes are still pending (3 User, 1 Machine)”).
- It never reports anything itself: `MainForm` writes the success message onto the status strip and shows the failure message through `DialogHelper.ShowError(..., "Apply")`, so the flow, the counts and every operator-visible string have one owner and the presentation has another.
- The defensive “nothing to apply” guard stays in the shell's Apply handler, because the same `HasChanges()` predicate already gates the command.

### `ConfigService` — settings persistence

- Path is `%APPDATA%\Enviro\config.json` (`Environment.SpecialFolder.ApplicationData`), so a read-only install folder can no longer block saving. The pre-DS-05 location beside the executable is read at most once: when the new file is absent and a legacy `config.json` exists, `Load` reads the legacy file, the settings are adopted, and the config is marked dirty so the next save writes the new location. Once the new file exists the legacy file is never consulted again — including when the new file is unreadable or invalid, which falls back to the defaults.
- `Config` is non-nullable for consumers: `Init` returns the loaded model or the defaults, never `null`.
- `Load` treats a missing, unreadable (`IOException`/`UnauthorizedAccessException`) or invalid (`JsonException`) file as absent. A `null` JSON body counts as absent too. The fallback marks the config dirty so exit rewrites the file.
- `IsDirty` is set by `PropertyChanged`; `MainForm`’s toggle updates the model, saves immediately (so the choice survives a crash), and the app-exit hook still saves when dirty.
- `Save` creates the `%APPDATA%\Enviro` folder when it is missing (it does not rely on the log having created it) and is atomic: it writes `config.json.tmp`, then `File.Move(..., overwrite: true)`.
- Serialization, IO and access failures are recorded in `IAppLog` and raise `SaveFailed`, which `MainForm` shows as one transient status-strip message. Nothing is modal and exit is never blocked; `MainForm` unsubscribes in `OnFormClosed`, so the exit-time save cannot touch a closed window. The previous file survives and the app still exits cleanly. A failed read is logged the same way; a missing file is not (that is the ordinary first run).

### `VariableFileService` — the import/export document

`IVariableFileService` owns the file exchange for export and import and holds no UI:

- `Snapshot(IEnvService)` builds the `VariableFile` from both scopes' current rows — the in-memory values, ordered by name, with rows whose deletion is still pending skipped, so the file matches the grid. Export never writes the registry and is never elevation-gated.
- `Entries(file)` lists the document for the export review window: one `VariableFileEntry` per variable, grouped by scope in name order, with no comparison (`Status` is null).
- `Compare(file, service, canImportMachine)` lists the document for the import review window. Each entry gets a `VariableFileEntryStatus`: the elevation gate is applied first (every machine entry is `MachineSkipped` without it, the same `AdminChecker.CanMutate` rule Apply uses), then invalid input is `Skipped`, an identical live value is `Unchanged`, a pending deletion or a different value is `Overwrites` (carrying the workspace value the merge would replace in `CurrentValue`), and an absent name is `New`. The merge itself is unchanged — the window only chooses which entries reach it.
- The document is JSON: a `version` plus one `user` / `machine` section keyed by name, serialized with `System.Text.Json` (camelCase, indented). Sections are read case-insensitively; a missing section reads as empty, and names differing only in case are merged into one case-insensitive section, so a hand-edited file cannot fail on its own shape. A null section is omitted when writing, so an export whose selected entries contain no row of one scope leaves that section out.
- `Read(path)` returns the document or one message naming the file: a missing or unreadable file, invalid JSON, an empty body, or any `version` other than `VariableFile.CurrentVersion` (`1`) is logged through `IAppLog` and returned as the message, never thrown at the shell. `Save(path, file)` mirrors the contract — null on success, the message on failure — so the shell has a single failure shape to show (§9.11).
- The merge is not this service's job: it hands the normalised sections to `IEnvScope.Import`, which owns every state transition. The review window filters the selected `VariableFileEntry` list before the shell builds the document (export) or calls `Import` (import).

### `CorruptedPathService` — the corrupted-path overlay

`ICorruptedPathService` owns validation; the grid and the PATH editor both ask it instead of each running its own `Directory.Exists` pass.

- `ValidateSegment(segment)` is the per-segment verdict: the segment is expanded exactly once with `Environment.ExpandEnvironmentVariables`, `ValidationHelper.IsPathToDirectory` decides whether it is a path at all (rooted paths only), and `ValidationHelper.IsMissingPath` decides between `Exists` and `Missing` on that same expanded text — it no longer expands by itself, so one segment is never expanded twice. Non-path text is `NotPath`, so it can never be missing.
- `IsCorrupted(value)` is the whole-value verdict derived from it: `ValidationHelper.SplitPathList` splits the value on `;` and drops the blank segments, and the value is corrupted when any of the remaining segments is `Missing`. The heuristic itself is unchanged (§9.5).
- `RequestScan(scope)` scans one scope and is a no-op while `ConfigModel.EnableCorruptedValidation` is off. It snapshots `(row, value)` on the calling thread, debounces ~150 ms so a burst of list changes collapses into one scan, cancels the previous scan of the same scope, and posts the verdicts back through the synchronization context of the requesting thread. `RequestScanAll()` does the same for both scopes, each with its own debounce and cancellation.
- A posted verdict is applied only where the row is still in that scope and still holds the value that was scanned. A row deleted or edited mid-flight keeps its current overlay, so a slow scan can never mark the wrong value.
- `Cancel()` (window close) drops pending and in-flight work and discards verdicts that were already posted but not yet applied; `ClearOverlay()` does the same and clears `IsCorrupted` on both scopes (validation turned off).
- The overlay is written only on the requesting thread, never on the worker, and it never touches `State`, `OriginalName` or `OriginalPath`.
- Every scan catches its own failures into `IAppLog`, including the cancellation that a newer request, the toggle or a closing window causes.

### `MetadataRepository`

Derives identity from the assembly, not from configuration:

- `Title` = assembly name (`Enviro`).
- `Author` = `AssemblyCompany` (`regularentropy` in the csproj).
- `Copyright` = `AssemblyCopyright` (the notice in the csproj), falling back to `Author` when the assembly carries none.
- `Version` = `Major.Minor.Build` of the assembly version, itself driven by the `<Version>` element in `enviro.csproj` — the csproj is the only place the number is written, and the docs quote no value.
- Repository URLs are composed as `https://github.com/{Author}/{Title}`, the issue tracker as `{RepositoryLink}/issues`, the API URL as `https://api.github.com/repos/{Author}/{Title}/releases/latest`, and `LicenseLink` is the canonical `https://www.gnu.org/licenses/gpl-3.0`.

### `UpdateService`

Sends the request through one `HttpClient` shared by every check (10 s timeout; the user agent is set per request to the app title), GETs the latest-release API URL with the optional `CancellationToken`, and:

- throws `HttpRequestException` (with status code) for non-success responses;
- reads `tag_name`, `body` and `html_url` with `TryGetProperty`; a missing `body` or `html_url` becomes an empty string, and a missing or unparseable `tag_name` means "no update" rather than a crash;
- normalises the tag — trims it and strips one leading `v`/`V` — then parses it with `Version.TryParse` and returns `null` when it is not newer than the running version;
- returns `null` when the response body is not valid JSON;
- otherwise returns `UpdateResponse(Tag, LastReleaseURL, ReleaseNotes, CurrentVersion)` with the normalised tag; `CurrentVersion` is the running `Major.Minor.Build`, carried so the dialog can show the jump without resolving `MetadataRepository` itself.

`MainForm` catches three failure shapes: an inner `SocketException` (offline), `StatusCode == NotFound` (repo unavailable), and `OperationCanceledException` (the request timed out). Every other failure would propagate to the WinForms unhandled-exception path.

---

## 8. UI composition

### `MainForm`

The shell. It owns a `MenuStrip` and a `TabControl` named `AllTab`; the tabs themselves are injected at runtime.

Menu structure (items created in the designer unless noted):

- **File** (`fileToolStripMenuItem`)
  - Export variables… — menu-only command (no shortcut), see §9.11
  - Import variables… — menu-only command (no shortcut), see §9.11
  - Restart as administrator — menu-only command (no shortcut); the same `RestartAsAdmin` the Machine tab's Elevate banner calls, disabled while the process is already elevated (§9.8)
  - Exit (`exitToolStripMenuItem`, click → `Close`, so the unsaved-changes guard still runs)
- **Variables** — built in code and inserted after File; lists the object commands of the table below — **Add**, **Edit**, **Delete** and **Apply** — each with its shortcut text visible
- **View** — built in code and inserted after Variables; lists **Refresh** (`F5`) and **Focus filter** (`Ctrl+F`) from the command table, followed by the checkable **Check for corrupted variables** item, which is a setting created outside the table
- **Options** (`optionsToolStripMenuItem`) — empty in the designer; a **Theme** submenu (`System`, `Light`, `Dark`, one check at a time) is added in code
- **Help** (`helpToolStripMenuItem`)
  - Check for updates → update flow
  - About → `Func<AboutForm>()` builds a fresh transient `AboutForm` and shows it modally

Every object command is defined once, in `Static/MainCommand.cs`: a `MainCommand` holds the label, the shortcut `Keys` it answers to, one `Execute` action and one `CanExecute` predicate, and a `MainCommandTable` holds the eight of them and looks them up by key. The two File commands are menu-only: their shortcut array is empty and they have no toolbar button, so the shortcut router and the five-button toolbar are untouched. The table holds no controls — `Keys` values, delegates and predicates only, `Keys` being the one WinForms type it names — so the toolbar buttons and the menu items are *projections* of it and never its source. A new command is one entry there plus at most one toolbar button; its enablement rule is written once.

`toolStrip1` itself is presentation only and carries no behaviour. Its appearance is deliberately text-first, because the project ships no icon set: every button sets `DisplayStyle = ToolStripItemDisplayStyle.Text`, gets an equal `Padding`, and is left `AutoSize` — `MainForm.resx` therefore holds no per-button `Size`, so a longer localized or DPI-scaled label widens its button instead of clipping. The strip hides its grip (`GripStyle = Hidden`, the strip is not reorderable), renders flat in system colours (`RenderMode = System`), carries a small `Padding`, and enables `ShowItemToolTips` so each button's shortcut hint is actually shown. Two `ToolStripSeparator`s group the five commands as row actions (**Add**, **Edit**, **Delete**) · **Refresh** · **Apply**, which sets the one committing action apart. The separators are not commands and are invisible to `UpdateActionState()`, which only writes the projected command targets. A one-pixel `topDivider` panel (docked `Top`, `SystemColors.ControlDark`, registered between `menuStrip1` and `toolStrip1` in `Controls` so the docking order puts it under the menu and above the commands) is the line between the two strips; it is decoration too, holds no state and is never referenced from code.

`MainForm` holds the injected `IEnvService`, `IActionFactory<EnvModel>`, `IUpdateService`, `ITabFactory`, `IEnvGridFactory`, `IConfigService`, `ICorruptedPathService`, `IVariableFileService`, `IAppLog`, the `ApplyCoordinator` and a `Func<AboutForm>` factory. It is a shell: it owns layout, the event subscriptions, the command table and the calls into the extracted pieces (§3) — the `--window-bounds` parsing and screen check are `Static/WindowGeometry`'s, the status-strip strings are `Static/StatusPresenter`'s, apply orchestration is the coordinator's (§7), and the About dialog comes from the factory delegate, so the form resolves nothing from the container. It still decides *when* an apply runs; it no longer decides what an apply says. Its constructor also assigns `Icon` from `Properties.Resources.favicon` (the same `.ico` the project declares as `<ApplicationIcon>`): a `Form` otherwise shows the WinForms default icon, whatever the executable is branded with, so the title bar, taskbar and Alt-Tab would not match the `.exe`.

A `ToolStrip` under the menu (`toolStrip1`) is the primary command surface and exposes **Add**, **Edit**, **Delete**, **Refresh** and **Apply**; the **Variables** menu lists the object commands **Add**, **Edit**, **Delete** and **Apply**, the **View** menu lists **Refresh** and **Focus filter** above the corrupted-validation toggle, and the **File** menu lists the menu-only **Export variables…** and **Import variables…** above a separator and **Exit**. Both surfaces come from the command table: `CreateCommandSurfaces()` builds the eight commands and calls `Project(command, button, menu, index?)` for each, which adds the menu item (with `ShortcutKeys` set only when the command has a shortcut, so the key is visible) at the requested position and records the existing toolbar button next to it.

`UpdateActionState()` is the single enablement refresh: for every command it evaluates the predicate once and writes the result to the toolbar button and the menu item, then refreshes the status strip. It runs on tab changes, grid selection changes, `ListChanged`, after a refresh, and once after the tabs are built; the validation toggle refreshes only the status strip (no command predicate depends on the setting). The predicates are Add → the active grid's scope is mutable (`AdminChecker.CanMutate`); Edit and Delete → the same plus a selected row and focus outside a text box (`ActiveControl is not TextBox`, so `Delete`/`F2` in the filter box keep their text-editing meaning); Apply → `EnvService.HasChanges()`; Refresh, Export and Import → always; Focus filter → the active tab has a filter box. Nothing else writes a command's `Enabled`, so a button and its menu item cannot disagree.

`ProcessCmdKey` is the only shortcut path: `MainCommandTable.TryExecute(keyData)` runs the command bound to the key, and only when its predicate allows it — an unknown key or an unavailable command falls through to `base.ProcessCmdKey`. Handlers therefore never call `PerformClick()` on each other, and a shortcut cannot outlive the state of its toolbar button. The keys are `Ctrl+N` Add, `F2` / `Ctrl+E` Edit, `Delete` Delete, `F5` Refresh, `Ctrl+S` Apply and `Ctrl+F` Focus filter. A modal dialog runs its own message loop, so no shortcut reaches the form while a dialog is open, and `ApplyAsync` completes synchronously, so there is no in-flight apply for a second `Ctrl+S` to re-enter.

The constructor subscribes to `ListChanged` on both scopes' rows (`IEnvScope.Rows.ListChanged`; they fire for row changes and for `EnvModel.PropertyChanged`, including `State`) and runs the refresh once after the tabs and the command surface are built, so Apply is disabled at launch and after every successful apply.

A `StatusStrip` at the bottom summarises the workspace: pending work as `N added · N modified · N deleted` (or “No pending changes” when clean), the corrupted-row count while validation is on, and the elevation state as `Administrator` / `Standard user`. Its pending label carries the legend for the grid's state markers (`+` added, `~` modified, `–` deleted, `!` corrupted), and item tooltips are enabled. The per-refresh strings come from `Static/StatusPresenter.Describe(service, enableCorruptedValidation)`, a pure function that returns a `StatusSummary`; `UpdateStatusStrip()` only writes those strings onto the labels, and the presenter decides nothing about layout. It runs whenever `UpdateActionState()` runs — tab changes, selection changes, `ListChanged`, which includes each overlay write — and on the validation toggle; when validation is off the corrupted label is hidden. The corrupted count reflects the last completed scan: the debounced background scan (§9.5) raises `ListChanged` when its verdicts land, which is what rewrites this label. The elevation label is the exception: elevation cannot change while the process runs (`AdminChecker.IsAdmin()` is cached), so the constructor writes it once — `Administrator` or `Standard user` — and no refresh touches it. A separate label reports a successful apply as a transient `Applied N changes` message, and also carries a failed settings save (`IConfigService.SaveFailed`), so that failure is visible without a dialog; both are cleared after five seconds by a one-shot `System.Windows.Forms.Timer`.

The two settings — the View menu's corruption toggle and the Options → Theme submenu — are not commands and stay out of the command table: the corruption toggle applies live, while a Theme click writes `ConfigModel.Theme`, saves immediately (like the toggle, so the choice survives a crash), checks exactly that item, and shows one `DialogHelper` notice that the theme is used the next time Enviro starts. Nothing is recoloured live — the colour mode was fixed before the window existed (§5). The Variables and View menus are built in code, so re-opening `MainForm` in the designer does not show them.

### Tabs and grids

`TabFactory.Create(type)` creates a `TabPage` whose `Text` is the enum name (`"User"` / `"Machine"`) and whose `Tag` holds the `EnvironmentalVariableType`, fills it with a top-docked filter `TextBox` and a grid produced by `EnvGridFactory.Create(type)`, and connects the two through `IEnvGridFactory.AttachFilter`. For a non-elevated session the Machine page also gets a top-docked read-only banner with an **Elevate** button, supplied through the optional `elevate` action. The grid, filter box and banner stay direct children of the tab page; the scope is carried by the nearest ancestor whose `Tag` is a scope, so nesting does not matter.

The tab **text** is presentation only. `ControlHelper.GetTabType` walks up from the control to the nearest scope-carrying ancestor (a `Tag` holding an `EnvironmentalVariableType`) and `AdminChecker` consumes that typed scope. A control with no scope-carrying ancestor yields no scope, and every mutation guard denies it: nothing defaults an unknown control to `User`.

`EnvGridFactory.Create` builds a non-editable, full-row-select `DataGridView` and wires:

- **Columns** — a narrow unbound indicator column (fixed 28 px, not sortable, headerless) followed by `Name` and `Value`, filled, auto-sized, sortable. The value column is still bound to `EnvModel.Path`; only its header uses the user-facing term. `CellFormatting` renders the indicator from `GridStyleHelper.GetStateIndicator`: `+` added, `~` modified, `–` deleted, `!` corrupted, blank for unchanged.
- **Density and headers** — `RowTemplate.Height` is the font height plus 12 px (so it follows DPI scaling), column headers use bold text with padding, and the grid's chrome comes from `GridStyleHelper.ApplyGridTheme`: the empty-area background, grid lines, default cell text and the header colours. In dark mode that call also turns off the OS header painter (`EnableHeadersVisualStyles`), because Windows paints headers from the visual style and ignores the dark colour mode; the light palette leaves it on so light mode is unchanged. Alternating row colours are deliberately not used because they would fight the state colours.
- **Auto-generated columns** — enabled in `DEBUG` only, so debug builds show extra model properties.
- **Data binding** — a `BindingSource` over the grid's `IEnvScope.Rows`, so the grid binds to the service-owned list itself; every change reaches it as `ListChanged`.
- **Sorting** — `ColumnHeaderMouseClick` delegates to `GridStyleHelper.SortGrid`, which asks the scope to order and refill its own rows (`IEnvScope.Sort`) and manages the sort glyph. It captures the selected `EnvModel` and the first displayed row before the refill and restores both when they still exist (and are not hidden by a filter), so sorting no longer loses the user's place.
- **Filtering** — the top-docked filter box hides rows whose `Name` and `Value` do not contain the text (case-insensitive `Contains`) by setting `DataGridViewRow.Visible`; the bound list, its order and change tracking are untouched. The grid factory owns the grid→filter mapping (`IEnvGridFactory.FilterOf`), so `Ctrl+F` focuses the same box `ApplyFilter` reads. The filter is re-applied after every `ListChanged` and after sorting; when the selected row is filtered out the selection moves to the first visible row, and with no matches the current cell is cleared. A successful apply clears every filter (`ClearFilters`) so the workspace starts from the full list again.
- **Corrupted detection**:
  - `BindingSource.ListChanged` is subscribed unconditionally and, for every change except the overlay's own `IsCorrupted` write, requests a debounced scan of that grid's scope through `ICorruptedPathService` (one request per event, one scan per burst);
  - at creation, the rows already in the list bound to that grid are reviewed by the same request; the service ignores it while the option is off;
  - the factory never scans: `MarkIfCorrupted` and `IEnvGridFactory.RescanAll()` are gone, and the service owns when a scan runs (§9.5, §9.6).
- **Double-click** — silent for a read-only scope (the tab banner already says so) and otherwise routed through `RowActionHelper.Edit` like the toolbar, the context menu and the shortcuts. The only elevation question it asks is `AdminChecker.CanMutate(grid)`, the same rule as every other guard; it never decides elevation from the grid's constructor argument. The edit opens through `EntryFormFactory.Create(model, scope)` as a modal `EditForm`.
- **Right-click** — hit-tests the row, selects it, and shows a context menu from `ContextMenuFactory`. On the empty area it asks `CreateForEmptyArea` instead, which offers Create only for a mutable scope and nothing for a read-only Machine tab.
- **Formatting** — `GridStyleHelper.ApplyStateFormatting` colours the cell by state and keeps the same colour recognisable while selected: `ThemePalette.Row(state, isCorrupted)` returns the paired normal and selection backgrounds for the active palette, and `ThemePalette.SelectionText` supplies the selection text colour (black in light mode, light in dark mode). Both shades of a state come from one palette entry, so the mode cannot drift them apart, and the corrupted overlay overrides the state background.

### `ContextMenuFactory`

Builds a per-invocation `ContextMenuStrip` for the clicked row. It holds **row-specific** actions only; scope-wide commands stay on the toolbar. The mutating items are added only when
`AdminChecker.CanMutate(grid)` is true — user scope always, machine scope only when elevated:

- **Restore** when the row is `Deleted` / **Reset** when it is `Modified` (both ungated, since they only change in-memory state; both resolve the grid's scope through `ControlHelper.GetTabType`, and a control without one offers neither).
- **Edit** → `EditForm` for the row.
- **Delete** → `RowActionHelper` → `IEnvScope.Delete`.
- a separator, then **Copy name** (`Name`), **Copy value** (`Path`) and — only on a `Modified` row —
  **Copy original path** (`OriginalPath`, i.e. the value still in the registry) → clipboard helpers.

In a non-elevated session the Machine tab therefore offers only the copy actions; there is no rejected click and
no “Please run as administrator” dialog from the menu. There is no **Create** item on a row (creation is
scope-wide, so it lives on the toolbar, the empty-area menu and `Ctrl+N`) and no separate **Path entries…**
item: **Edit** already opens a window with the **Edit entries** toggle for path-like values.

The Edit/Delete handlers and the empty-area Create handler delegate to `RowActionHelper`, the same helper the toolbar and keyboard shortcuts use; it re-checks `AdminChecker.ValidateTab(grid)`, so a mutating action reached by any route is denied with the dialog rather than writing. Copy and Restore/Reset are ungated.

### PATH segment editor

Path-like values (`ValidationHelper.IsPathList(name, value)`: name `PATH` case-insensitive, or a value containing `;`) get an entries view **inside the same entry-dialog window** — both `CreateForm` and `EditForm`. The **Edit entries** button in the button bar is always offered, so a list can be built from nothing (its **Add folder…** appends) rather than only polished after a `;` was typed. `PathEntriesControl` occupies the value row; the same button reads **Edit string** while the list is showing. The dialog keeps the value text box as the single source of truth: entering entries mode loads the current text, every list change writes the joined value back into the hidden text box, and **Save**/**Create** therefore follows the normal path (validation, `Upsert`/`ApplyEdit`, change tracking). **Cancel** closes without touching the model, and the window grows from 180 to 500 logical px in entries mode while the name row keeps hugging its content. The whole mechanism — the control, the toggle, the height switch — lives in `AbstractEntryForm`; the subclasses only supply the save action.

The list rule lives in `Static/ValidationHelper`: `SplitPathList` splits a value on `;` and drops the blank segments, `JoinPathList` puts the entries back together. The control uses both, so the list it lists and the value it writes back cannot disagree with the whole-value verdict the grid uses, and it lists the entries in order with an existence indicator (`Exists`, `Missing`, `Not a path`) computed through `ICorruptedPathService.ValidateSegment`, the same verdict the grid uses; the indicator's colour comes from `ThemePalette.SegmentText`. The control never touches the filesystem: the verdict decides whether the selected entry exists, and `ValidationHelper.IsExistingDirectory` tells **Open in Explorer** whether that location is a folder (opened) or a file (selected in Explorer). It is a synchronous paint path, so the per-segment check stays synchronous there and is never made async; the control keeps a parameterless constructor for the WinForms designer, where a missing service reports `Not a path` and refuses the **Open in Explorer** action. **Add folder…** appends a folder-picker selection, **Remove** drops the selected entry, **Move up** / **Move down** reorder it, and the entry cell is editable inline. There is no separate dialog or row-menu entry: **Edit** opens this window and its **Edit entries** button reaches the segment list.

Every list change writes the joined value back into the value text box, so **Save** is the only commit point: it validates the result and applies it through `IEnvScope.ApplyEdit`, making the row `Modified` (or keeping it `Added`) exactly like a normal edit. **Cancel** closes without touching the model.

### Entry dialogs

Both dialogs are constructed with the grid's `IEnvScope` (resolved by `EntryFormFactory` through `IEnvService.GetScope`), which is where every mutation below goes. `AbstractEntryForm` owns the shared layout, used by both `CreateForm` and `EditForm`: a fixed-dialog window with 16 px padding, a left-aligned label above each field (`Name`, `Value`), a value textbox paired with a **Browse…** button, and a footer that puts the **Edit entries** toggle on the far left and the primary action followed by **Cancel** on the right. The window opens at 180 logical px — tall enough for the two fields plus the footer, with no dead space — and grows to 500 in entries mode through the `TextModeHeight` / `EntriesModeHeight` constants.

- Both textboxes' `TextChanged` handlers call `UpdateActionState()`, which enables the action button only when `ValidationHelper.IsValidEnvironmentVariableInput` passes — name and value non-blank, no `=` or NUL in the name, and no NUL in the value — and, through the virtual `HasPendingChange()` hook, when the input differs from what the form was opened with. The constructor calls it once so the initial button state is correct; `EditForm` overrides the hook so Save stays disabled while nothing has changed, while `CreateForm` accepts any valid input.
- The **Browse…** button opens a two-item menu — **Browse folder…** and **Browse file…** — so both pickers are reached by one unambiguous click (or keyboard: focus the button, open the menu, choose an item). Each item opens `CommonOpenFileDialog` with `IsFolderPicker` set accordingly and writes the result into the value textbox; the folder picker allows multiple selections and the picks are joined into one `;` list through `ValidationHelper.JoinPathList` (a single pick stays a single value). The written value replaces the box's content — the picker composes a fresh list rather than appending.
- Subclasses set the title/button labels and implement `RunAction`.

`CreateForm` — presentation only:

- trims name/value and requires both non-blank and valid (`ValidationHelper.ValidateEnvironmentVariableInput`, which shows the dialog on failure);
- `IEnvScope.Contains` decides whether the name already exists, and only then does the form ask to override with a prompt that names the variable and shows its current value — exactly one prompt, and Cancel returns without touching the store;
- then one `Upsert(name, value, overwrite)` call closes the dialog. The scope, not the form, decides whether that was an addition, a restore of a pending deletion or an overwrite.

`EditForm` — presentation only:

- validates the trimmed name/value before touching anything;
- no change → informational dialog and return (the Save button is already disabled in that state);
- otherwise one `ApplyEdit(model, name, value)` call: on success the dialog closes, on a rejection it shows the returned reason through `DialogHelper` and stays open. Every rule — the `Deleted` row, a blank name, a collision, a row that is not in the scope — lives in that call, and a rejected edit applies no part of it.

### Variable-file review window

`VariableFileForm` is the one modal review both directions share. The caller supplies the title, a `VariableFileDirection` and the entries (`IVariableFileService.Entries` for export, `Compare` for import); the form only chooses entries and never deletes, writes the registry or merges anything. A checkable `DataGridView` shows scope, name, value and — on import only — the workspace's current value and a status column; rows arrive grouped by scope in name order.

- **Export** starts every row checked; clearing a row omits it from the file, so a scope with no checked entry has no section at all.
- **Import** pre-checks `New` and `Unchanged` and leaves `Overwrites` unchecked, so replacing an existing value is opt-in in addition to the merge's own rule. `Skipped` (invalid input) and `MachineSkipped` (no elevation) rows stay visible but their checkbox is read-only, and the status cell is coloured from `ThemePalette.FileStatusBackground` (the new/overwrite pair), with `ThemePalette.MutedText` for the entries that are not carried.
- **Select all** / **Select none** set every selectable row in one step and leave the disabled rows untouched; each button is disabled when the selection is already in that state.
- The footer counts the selection and disables **Export**/**Import** while nothing is checked, naming the reason (“Select at least one variable.” or “No entries can be selected.”), so an empty selection writes nothing.
- **Cancel** returns without touching anything; accepting exposes `SelectedEntries`, which the shell turns into a `VariableFile` (export) or hands to `IEnvScope.Import` per scope (import).
- The accept/cancel split keeps DS-15's reporting: success is one transient status message, failure is one `DialogHelper` error plus one log entry.

### `AboutForm` / `UpdateForm`

Both are simple modal dialogs that open links with `UseShellExecute = true`. `AboutForm` is metadata-driven: the window title, product name, version (`Version {Version}`), copyright and footer link targets all come from `MetadataRepository`, with a 48×48 app icon, a bold title row and a one-line description in the header, and a thin separator above the footer. The header logo ships as black artwork with a transparent background, so both dialogs replace it with `ThemeArtwork.Tint(Properties.Resources.pen_square_svgrepo_com)`: the tint keeps the alpha mask and paints the visible pixels in `ThemePalette.Artwork` (black in light mode, light in dark mode), which is what keeps the pen-square from vanishing on a dark window. The footer links follow the same rule: the light palette keeps the system link colours, while the dark palette sets `LinkColor`/`ActiveLinkColor`/`VisitedLinkColor` from `ThemePalette.LinkText`/`LinkActive`/`LinkVisited` (white at rest), because the system blue/red/purple pair is unreadable on a dark window. Its footer carries **GitHub**, **Report an issue** and **GPL-3.0 license**; all three raise one `OpenLink` handler, which opens the URL held in the label's `Tag`, and on `Win32Exception`/`InvalidOperationException`/`FileNotFoundException` logs through `IAppLog` and shows a `DialogHelper` notice instead of the global unexpected-error dialog. **OK** is both `AcceptButton` and `CancelButton`, so Enter and Esc close the dialog. `UpdateForm` shows an app-icon header, the installed → available version pair from `UpdateResponse.CurrentVersion`/`Tag`, and the release notes; it opens the release page with **Open release page** (**Enter**) and closes with **Close** (**Esc**), and it does not download or install anything. It is a normal window, so its constructor also sets `Icon = Properties.Resources.favicon`; `AboutForm` and `VariableFileForm` are `FixedDialog` windows, which show no title-bar icon, and the entry dialogs set `ShowIcon = false`. Neither mutates application state.

The release body is Markdown, so `UpdateForm` renders it through `Static/ReleaseNotesFormatter.ToPlainText` — a pure, dependency-free flattener for the constructs GitHub's generated notes use (comments, images, links and autolinks, HTML, headings, quotes, bullets, rules, inline code, bold, entities, blank-line collapse). The plain text goes into a read-only, word-wrapping box that is keyboard-focusable (`TabStop` is left on) so the notes can be scrolled without a mouse; empty notes show a placeholder instead of an empty pane.

`AboutForm` lives in namespace `enviro` (like `MainForm`), whereas the entry/update forms live in `enviro.Forms`; this split is historical and should be preserved to avoid breaking designer files and `Program`’s registrations.

---

## 9. Key flows

### 9.1 Create a variable

1. The toolbar **Add**, `Ctrl+N`, or an empty-area **Create** right-click → `AdminChecker.ValidateTab`.
2. `EntryFormFactory.Create(type)` resolves the scope through `IEnvService.GetScope(type)` and shows a `CreateForm` bound to it.
3. Validation → duplicate check via `Contains`.
   The always-offered **Edit entries** toggle opens the segment view, where **Add folder…** appends each pick; multi-select **Browse folder…** composes a whole list at once.
4. New value: one `Upsert(name, value, overwrite: false)` call → `UpsertOutcome.Added`, a model inserted with `State = Added`; grid repaints green and the corruption hook may mark it. The dialog closes without a success modal.
5. Duplicate accepted: the prompt shows the existing value, then one `Upsert(name, value, overwrite: true)` call → `UpsertOutcome.Overridden` → `Modified` (yellow) or stays `Added`; a row whose deletion was still pending is restored first, so it takes the new value too.

No OS call happens in this flow. The form holds only the prompt and the validation dialogs; the duplicate/restore/update sequencing is the scope's.

### 9.2 Edit / rename

1. Double-click, context **Edit**, the toolbar **Edit**, `F2` or `Ctrl+E` → `EditForm` bound to the selected `EnvModel`. Its **Save** button is disabled until the trimmed input differs from the model.
2. **Save** makes one `ApplyEdit(model, name, value)` call that applies the name and the value together: a value-only change marks the row `Modified`, a name change on an `Added` row keeps it `Added`, and a name change on a persisted row renames the same row in place and marks it `Modified`. The grid still shows one row for the variable. Apply removes the old registry key and writes the new one (§9.4).
3. `ApplyEdit` rejects — with a reason the form shows and not one model touched — a blank name, a case-insensitive collision with another variable, a row this scope does not hold and a row that is `Deleted`; a rejected edit applies no part of it. A blank name is rejected before the collision check with its own reason (“The name cannot be empty”), so the two cases cannot be confused.
4. Editing a `Deleted` row is therefore refused; it must be restored first.
5. A `PATH`-like row is edited through the same **Edit** window: the **Edit entries** toggle switches the value row to the segment list, and every list change updates the hidden value field. **Save** therefore follows steps 2–4 on the joined value.

### 9.3 Delete / restore / reset

Delete is reachable from the context menu, the toolbar and the `Delete` key; all routes go through `RowActionHelper` to `IEnvScope.Delete` on the grid's scope.

- Deleting an `Added` row removes it immediately.
- Deleting a persisted row marks it `Deleted`; the row stays visible with a coral background and strikeout.
- **Restore** (only on `Deleted`) returns it to `Unchanged` — under its `OriginalName` — making it identical to the OS value again.
- **Reset** (only on `Modified`) restores `Name` from `OriginalName` and `Path` from `OriginalPath` and returns to `Unchanged`.
- `Update` ignores a `Deleted` row, so an edit cannot cancel a pending deletion.

### 9.4 Apply

1. The toolbar **Apply** and `Ctrl+S` are enabled only while `EnvService.HasChanges()` is true; `Apply` still guards with a silent no-op when it is reached with nothing pending.
2. `EnvAdapter.ApplyAsync()`:
   - user scope: deleted → modified → added;
   - machine scope: same, only if elevated and only after the user scope succeeded;
   - each written row is cleaned immediately through `IEnvScope.MarkApplied`: deleted rows leave the list, the rest become `Unchanged` with `OriginalName`/`OriginalPath` synced; a renamed row's old key is deleted before its new key is written, and written back if that new-key write throws.
3. `ApplyCoordinator.ApplyAsync()` records the pending count and turns the returned `ApplyResult` into an `ApplyOutcome`. On `Succeeded` the message is the transient `Applied N changes` text; otherwise it names the variable that could not be written, its scope and the exception message, how many rows the run had already written, and how many changes are still pending per scope (e.g. “4 changes are still pending (3 User, 1 Machine)”). `MainForm` only shows that message: the status strip on success (no modal), `DialogHelper.ShowError(..., "Apply")` on failure.
4. Grid colours return to the state colour via `PropertyChanged` on `State`; the resulting `ListChanged` event requests a fresh corrupted-path scan (the service, not the grid, recomputes each row's `IsCorrupted` overlay), refreshes the status strip and re-applies the active filters. On success, `MainForm` also clears every filter box so the full list is visible again.

The corruption overlay itself never counts as a change: `HasChanges()` examines `State` only.

Because the user scope is applied first and the run stops on the first failure, a user-scope failure means the machine scope is never attempted.

### 9.5 Corrupted-path detection

Enabled by `ConfigModel.EnableCorruptedValidation` (default **on**). The whole-value verdict is `ICorruptedPathService.IsCorrupted` (`CorruptedPathService`); it uses the `ValidationHelper` predicates per segment:

- splits the value on `;` and checks every **non-blank segment**, so a multi-entry `PATH` is validated end to end (empty segments such as a trailing `;` are ignored);
- each investigated segment is expanded with `Environment.ExpandEnvironmentVariables`;
- `IsPathToDirectory` decides whether it looks like a path at all (rooted paths only);
- non-path values (e.g. `%OS%`, plain text, relative paths) are treated as valid;
- a rooted path is valid only if `Directory.Exists` or `File.Exists` succeeds;
- the value is corrupted when **any** path-like segment is missing.

Marking is triggered:

- once for every model already in the list bound to a grid when that grid is created (each list asks for its own scope's scan), a no-op while the option is off — this request runs in the `MainForm` constructor, before the message loop, so it is best-effort only;
- by `MainForm.OnShown`, which asks for both scopes once the window is up and the loop is pumping — the request that makes the startup overlay reliable;
- on every `ListChanged` event of that grid's binding, except the overlay's own `IsCorrupted` writes — the grid skips those, because a scan result is itself a list change and would otherwise re-request a scan;
- by `ICorruptedPathService.RequestScanAll()` when the option is toggled on (§9.6) and after a refresh (§9.10).

Each request is debounced and scanned off the UI thread as described in §7 `CorruptedPathService`. A pass both **sets and clears** the overlay — `IsCorrupted = IsCorrupted(value)` for every reviewed row whose value is unchanged — and never touches `State`. A row turns red while its value is invalid and returns to its state colour once the value becomes valid (or the next scan finds it valid). The overlay is a display concern only: Apply/Reset/Restore behave exactly as if it did not exist.

### 9.6 Toggle corrupted validation

The checkable menu item is the source of truth. Its handler copies `Checked` into `ConfigModel.EnableCorruptedValidation`, saves `config.json` immediately (the exit hook still saves when dirty), and then calls `ICorruptedPathService`: turning the option on requests a scan of both scopes, turning it off calls `ClearOverlay()`, which cancels pending work, drops verdicts that were already posted and clears every `IsCorrupted` flag at once. No restart is required. The menu check state cannot drift from the running setting because the handler simply mirrors the item. `EnvGridFactory`'s `ListChanged` subscription is attached unconditionally and the service ignores requests while the option is off, so enabling validation later needs no grid rebuild.

### 9.7 Update check

`MainForm` → `IUpdateService.CheckForUpdatesAsync()` → GitHub API.

- `null` → “No updates found”. This also covers a malformed or non-version tag (e.g. `latest`) and a release body without a `tag_name`: unusable release data means "no update", never an exception.
- newer release → `UpdateForm` with the installed and available versions and the release notes; **Open release page** opens the release page in the browser (it does not download or install anything) and is disabled unless the reported URL is an absolute http or https address, so no other scheme reaches the shell. The tag is shown without a leading `v`.
- socket error → “Cannot access the internet”.
- timeout (10 s) → the same “Cannot access the internet” message.
- 404 → “The repository is unavailable”.

The check runs on the UI thread as an `async void` event handler and resumes on the UI context. The **Check for updates** menu item is disabled for the duration of the check and re-enabled in `finally`, so two checks cannot overlap.

### 9.8 Elevation

If the process is not elevated, the Machine tab shows a slim banner — *Read-only — run as administrator to edit machine variables* — with an **Elevate** button that calls `RestartAsAdmin`; File → **Restart as administrator** (a menu-only command, §8) reaches the same action and disables itself once the process is elevated, so the banner and the menu item are the two in-app elevation entry points. Elevation starts a new process with `Verb = "runas"` and `UseShellExecute = true`; the current window bounds and maximised state are passed along as a `--window-bounds x,y,width,height,maximized` argument, and the new instance parses it with `Static/WindowGeometry` and applies it before its first show when the rectangle fits entirely inside one screen's working area. Nothing is written to `config.json`, so the hand-off ends with that process tree: an ordinary launch, including one after a reboot, centres the window. Only after the process starts does the app set `_isRestarting` (bypassing the close guard) and exit. Cancelling the UAC prompt raises a `Win32Exception` with `NativeErrorCode` 1223, which shows an information dialog and leaves the current window open. Any other launch failure is not caught.

Elevation is checked once per process — `AdminChecker.IsAdmin()` caches the result — because it cannot change while the process runs.

### 9.9 Closing with unapplied changes

`MainForm.OnFormClosing` protects the two-phase model:

1. When `HasChanges()` is false, or `_isRestarting` is set, the window closes normally.
2. Otherwise the close is cancelled and `DialogHelper.ShowUnsavedChanges(count)` asks — `count` is `ApplyCoordinator.PendingChanges()`:
   - **Cancel** — stay open, nothing changes;
   - **No** — set `_isRestarting` and close without writing;
   - **Yes** — call `ApplyCoordinator.ApplyAsync()`; only on an outcome with `Succeeded` set does it set `_isRestarting` and close.
3. A failed apply shows the same failure dialog as `Apply` (failing variable, reason, rows written, rows still pending) and leaves the window open; the flag stays false, so a later close asks again.

`_isRestarting` marks every deliberate teardown: the elevation flow sets it after the new process starts, before `Application.Exit()`, so the restart never prompts twice; the discard and apply-then-close paths set it as well. `ApplyAsync` is synchronous underneath, so the handler runs to completion before the close is re-issued.

### 9.10 Refresh from the system

`View → Refresh` and `F5` call `MainForm.RefreshEntryClicked`:

1. If `HasChanges()` is true, `DialogHelper.ShowConfirm` states the pending count (`ApplyCoordinator.PendingChanges()`); anything other than OK cancels and discards nothing.
2. The filter boxes are cleared and the transient `Applied N changes` label is reset.
3. `IEnvService.Reload` re-reads both scopes and replaces each scope's rows through the scope itself, so the grids stay bound to the same lists and show a fresh snapshot in name order. Any active sort is not preserved. A failing read leaves the current rows untouched and the refresh stops there; a successful one raises a single `Reset` per list, so the filter, action state and status strip recompute once.
4. `ICorruptedPathService.RequestScanAll()` re-applies the corrupted overlay shortly after, and `UpdateActionState()` recomputes the toolbar enablement and the status-strip counts (which reset because every model is `Unchanged` again).

External changes made in Windows' own dialogs therefore appear without restarting the app.

### 9.11 Import and export

Export and import are menu-only commands (File → Export variables…, Import variables…): no toolbar button and no shortcut key, so the five-button toolbar and the shortcut router stay as they are. Both put the entry list through `VariableFileForm` (§8) between the file picker and the operation, so the user chooses exactly which variables travel; the window changes nothing by itself.

1. **Export** — the shell asks for a path with `CommonSaveFileDialog` (default `enviro-variables-yyyyMMdd.json`), then `Snapshot(IEnvService)` reads both scopes' current in-memory rows (rows whose deletion is pending are skipped) and `Entries` lists them for the review window. After the user accepts the selection, the shell builds the `VariableFile` from the checked entries — a scope with none is left out — and `Save` writes it. Success is one transient status message; a failure shows one `DialogHelper` error naming the file and logs the exception. Export is never elevation-gated, because it only reads memory. An empty workspace or an empty selection writes nothing: the first is reported before the window opens, the second keeps the window's accept button disabled.
2. **Import** — the shell reads the file with `Read`; a malformed, empty or wrong-version file shows the returned error and changes no row. `Compare(file, service, AdminChecker.CanMutate(Machine))` then marks every entry and the review window opens with `New`/`Unchanged` checked and `Overwrites` unchecked. Accepted entries are handed to `IEnvScope.Import` per scope; `MachineSkipped` entries are unselectable, and when the file has a machine section without elevation the status message still says the section was skipped. Cancelling changes no row.
3. `IEnvScope.Import` merges by name through `Upsert(name, value, overwrite: true)`: a name absent from the scope becomes `Added`, an existing name takes the value and becomes `Modified` (a pending deletion is restored first), and a row already holding the identical value is left untouched and counted `Unchanged`. An entry carrying the row's baseline (same name case-insensitively, `OriginalPath` value) cancels a pending edit or deletion and leaves the row `Unchanged`. Entries rejected by `ValidationHelper.IsValidEnvironmentVariableInput` are counted `Skipped`. The whole batch runs with `RaiseListChangedEvents = false` and raises one `ResetBindings()`, so a 500-row file costs one refresh.
4. Import deletes nothing and writes no registry: the merged rows are reviewed in the grids, and Apply remains the only write (§9.4). The batch's single list change is what requests the corrupted-path rescan and refreshes the status strip.

---

## 10. Static helpers

| Helper | Responsibility | Architectural note |
|---|---|---|
| `AdminChecker` | `IsAdmin`, `CanMutate`, `ValidateTab` | Scope guard for all machine mutations; scope resolves from the tab's `Tag`, `IsAdmin` is cached once per process |
| `DialogHelper` | Info/Warning/Error/Confirm/Message | Single funnel for user messaging |
| `ControlHelper` | `GetCurrentModel`, `GetTabType` | Bridges WinForms controls back to domain types |
| `ClipboardHelper` | Copy a selected model property | Null-safe no-op |
| `GridStyleHelper` | State colours (normal + selection), state markers, strikeout, grid chrome (background, lines, cell text, headers), sort glyph + selection/scroll restore | The colours come from `ThemePalette`; `ApplyGridTheme` is shared by the three grids and turns the OS header painter off in dark mode; the refill itself is `IEnvScope.Sort`, not a static helper |
| `StateCounter` | Counts one scope's rows by edit state and by the corrupted overlay | `StatusPresenter` sums both scopes for the status strip |
| `StatusPresenter` | The status strip's per-refresh content (`StatusSummary`) from the service state and the setting | Pure function; the shell only writes the strings onto its labels, and the elevation label is written once in the constructor |
| `ThemePalette` | The light and dark palettes and every themed lookup: a row's normal/selection background, the selection text colour, the grid background, cell text, grid lines, header colours, the logo ink, link colours, a PATH verdict's colour, a file-entry status background, muted text and the read-only banner | The only place a colour is named; the active palette follows `Application.IsDarkModeEnabled`, which the colour mode fixed at startup. `SystemHeaders` decides whether the OS paints grid headers (light) or the palette does (dark); `SystemLinkColours` likewise decides whether links keep the system colours |
| `ThemeArtwork` | Recolours the black, alpha-blended logo with `ThemePalette.Artwork` | Keeps the source's alpha channel, so the anti-aliased edges stay smooth; used by `AboutForm` and `UpdateForm` |
| `WindowGeometry` | `--window-bounds` parsing + the screen check | Takes the working areas as an argument, so `Screen.AllScreens` stays in the form and the parser is testable |
| `ValidationHelper` | Non-blank input, forbidden-character rules, the `;` list split/join, per-segment path predicates, the folder-vs-file test | Name rejects `=`/NUL, value rejects NUL only; the whole-value verdict belongs to `ICorruptedPathService`, which expands a segment once before the predicates see it; no consumer calls `Directory.Exists` itself |
| `EnvTypeConverter` | `EnvironmentalVariableType` → `EnvironmentVariableTarget` | Exhaustive over `User`/`Machine`; any other value throws `ArgumentOutOfRangeException` instead of defaulting to a target |

These are static because they are stateless and used from UI event handlers; they hold no state, though `GridStyleHelper` and `StateCounter` take the `IEnvScope` they act on as an argument.

---

## 11. Threading, async and error handling

- The UI thread owns every model. `UpdateService`'s `HttpClient` continuations are awaited back onto the UI thread via the captured context, and `CorruptedPathService` is the only other background work: it scans a `(row, value)` snapshot on a worker thread and posts the verdicts back through the requesting thread's `SynchronizationContext`, so `IsCorrupted` is never written off the UI thread.
- `CorruptedPathService` keeps one cancellation source per scope. A newer request cancels the previous scan of that scope; `Cancel()`/`ClearOverlay()` cancel everything on window close or when validation is disabled; a verdict that is posted and then superseded is dropped. Every fire-and-forget scan catches its own exceptions into `IAppLog`, so no scan failure reaches `AppDomain.UnhandledException`, and the UI thread never waits on one (no `.Result`/`.Wait()`).
- The update request is bounded by a 10 s `HttpClient.Timeout` plus an optional `CancellationToken`; a timeout surfaces as `OperationCanceledException`, which the update handler logs and reports as a network failure instead of letting it escape. An unusable (non-JSON) release body is logged before it becomes "no update".
- `ApplyAsync` is `Task`-returning but synchronous; the UI does not block on a separate thread.
- The adapter catches per row and only to build its `ApplyResult`; the exception is written to `IAppLog` and its message travels to the failure dialog instead of being discarded.
- Unhandled exceptions are funnelled, not swallowed: `Main` installs `Application.ThreadException` and `AppDomain.UnhandledException` and forces `UnhandledExceptionMode.CatchException`. A UI-thread exception is logged and shown as one dialog naming the log path, then the app keeps running; an exception from another thread is logged and shown, and the process terminates as usual afterwards. A guard on the exception instance keeps a single exception from being reported twice when both handlers see it.
- `IAppLog` (`Services/AppLog.cs`) is the only log funnel: an append-only text file at `%APPDATA%\Enviro\enviro.log`, rotated once to `enviro.log.old` when it reaches 256 KB. That 256 KB is a per-file rotation threshold, not a cap on the log: the live file plus its one `.old` is normally under ~512 KB, and a single entry can overshoot the threshold before the next append rotates the file. It is created directly in `Main` before the container so the crash path does not depend on DI, then registered as a singleton for `EnvAdapter`, `ConfigService`, `UpdateService` and `MainForm`. Every write is serialized and its own IO failure is swallowed, so logging never turns one failure into two. No telemetry, no network reporting.
- Validation failures are communicated through dialogs, not exceptions: `IEnvScope.Upsert` returns `UpsertOutcome.Refused`, `ApplyEdit` returns a rejection reason and `ValidateTab` simply denies.
- `CreateForm`/`EditForm` validate their input in the dialog and then make exactly one scope call; the create and edit rules (duplicate handling, restore-then-overwrite, rename-then-revalue, and every rejection) live in `IEnvScope.Upsert`/`ApplyEdit`, so the store is a boundary for those two flows while `Add`/`Update` still accept whatever they are given.
- The close path (§9.9) reuses `ApplyCoordinator.ApplyAsync()` and the shared apply-failure dialog: a failed apply keeps the window open with the models still dirty.

---

## 12. Persistence and external I/O

| Data | Location | Format | Writer |
|---|---|---|---|
| Environment variables (user scope) | HKCU environment | Windows registry via .NET | `EnvAdapter` |
| Environment variables (machine scope) | HKLM environment | Windows registry via .NET | `EnvAdapter` (admin only) |
| Application settings | `%APPDATA%\Enviro\config.json` (legacy: beside `Enviro.exe`, read once) | JSON, indented; includes the `Theme` preference | `ConfigService.Save` |
| Variable backup / transfer | the file the user picks in File → Export variables… (default `enviro-variables-yyyyMMdd.json`) | JSON, `version: 1`, one section per scope | `VariableFileService.Save` (menu Export, only the reviewed entries; import reads it back) |
| Application log | `%APPDATA%\Enviro\enviro.log` (+ one `.old`) | Timestamped text, 256 KB rotation threshold per file (live + `.old` normally under ~512 KB) | `AppLog.Error` |
| Update information | GitHub REST API | JSON | `UpdateService` (read-only) |

The app never persists the variable list itself. The OS is the store; the in-memory list is a snapshot taken at startup and re-read on Refresh. The export file is written only when the user asks for it, and importing it feeds the in-memory model — Apply is still the only registry write. Settings live under `%APPDATA%` so the install folder may be read-only; a `config.json` beside the executable is only ever read, once, to migrate it. Window geometry is not persisted either: an elevation restart passes the bounds as a `--window-bounds` command-line argument (§9.8), and every ordinary launch centres the window.

---

## 13. Permissions and safety model

- Reading both scopes is allowed for any user.
- Scope comes from the tab's `Tag` (`ControlHelper.GetTabType`), never from tab text, and a control with no scope-carrying ancestor is denied every mutation instead of defaulting to `User`. `AdminChecker.IsAdmin()` is evaluated once per process and cached: elevation cannot change while the process runs, and the check used to construct a `WindowsPrincipal` on every selection change and `ListChanged`.
- The mutation rule has exactly one implementation: `AdminChecker.CanMutate(scope)` (`scope != Machine || IsAdmin()`). The control overload `CanMutate(Control)` is a thin wrapper that resolves the scope from the tab's `Tag`, and every caller reads that one function — the command predicates, the context menus, the handlers through `ValidateTab`, and `EnvAdapter` through the scope it holds (`CanMutate(scope.Type)`); nothing decides elevation from a grid's constructor argument or from the process alone.
- Editing machine variables is blocked at four levels, all reading `AdminChecker.CanMutate`: `ContextMenuFactory` omits edit/delete from the row menu (and offers no empty-area Create) when `CanMutate(grid)` is false; the toolbar's Add/Edit/Delete buttons are disabled for the non-elevated Machine tab through the `MainCommand` predicates; the Apply path skips machine scope when `CanMutate(Machine)` is false; and every mutating handler re-checks with `AdminChecker.ValidateTab` and shows the one “Please run as administrator” dialog. The grid double-click asks `AdminChecker.CanMutate(grid)` first and stays silent when the scope is read-only, so double-clicking the Machine tab without elevation does nothing instead of showing a dialog; it goes through `RowActionHelper` like the toolbar and the menu.
- `Restore`, `Reset` and override-in-`CreateForm` are not elevation-gated because they only change in-memory state until Apply.
- The user can elevate from inside the app; the process restarts rather than re-initialising the existing forms.
- `EnvAdapter` applies user changes even when not elevated, so an elevated and a non-elevated session behave identically for user variables. Machine rows cannot be dirty outside an elevated session because every machine mutation is gated, so a non-elevated apply never has machine work to skip.

---

## 14. Extension points

Adding a new variable scope, a new persisted setting, or a new grid action follows the existing seams:

1. **New setting** — add a property to `ConfigModel` (with `field`/`PropertyChanged`), consume it in `ConfigService.Config`, and add a UI toggle in `MainForm`. Saving happens automatically on exit when dirty; a setting that must survive a crash (like corrupted validation) also calls `IConfigService.Save()` from its toggle.
2. **New grid action** — add an item in `ContextMenuFactory.CreateForRow`, guard it if it mutates machine scope, and route the mutation through the grid's `IEnvScope`, resolving the scope the way `ContextMenuFactory` does: `ControlHelper.GetTabType(grid) is { } type ? envService.GetScope(type) : null`.
3. **New shell command** — one `MainCommand` entry in `MainForm.CreateCommandSurfaces()`; its menu item is projected automatically and an existing toolbar button is passed to `Project`, so the shortcut and the button cannot drift.
4. **New entry dialog** — derive from `AbstractEntryForm`, implement `RunAction`, and extend `EntryFormFactory`.
5. **New scope** — extend `EnvironmentalVariableType`, `EnvTypeConverter`, `EnvService` (one more `EnvScope` field, its property and the `GetScope` mapping), `Reload`, and the tab creation in `MainForm`; `TabFactory` tags each new tab page with its scope, which is what `ControlHelper.GetTabType` and `AdminChecker.ValidateTab` resolve.
6. **New data source / persistence** — implement `IEnvService`/`IEnvScope`; the grid, the dialogs and the adapter consume only those interfaces. `EnvGridFactory` and `PathEntriesControl` also depend on `ICorruptedPathService` for the corrupted-path verdict.
7. **New status-strip content** — extend `Static/StatusPresenter.Describe` and its `StatusSummary`; `MainForm` only writes the strings onto the labels, so layout stays in the designer. Content that cannot change while the process runs (the elevation label) is written once in the constructor instead of joining the refresh.
8. **New apply-visible step** — extend `Services/ApplyCoordinator`; it returns what to show (the success summary or the failure report) and never opens a dialog or starts a timer itself.
9. **New elevation hand-off value** — extend `Static/WindowGeometry` (parsing and the screen check) and keep the `Screen` lookup in the form.
10. **New variable-file content** — extend `VariableFile` (and `VariableFileService.Normalise`) plus the import/export handlers, and bump `VariableFile.CurrentVersion` whenever the shape changes: a file with any other version is refused rather than guessed at. A new per-entry comparison is `VariableFileEntryStatus` plus the branch in `VariableFileService.CompareEntry`; the review window displays whatever status and current value the comparison returns.
11. **New themed colour** — add it to both palettes in `Static/ThemePalette` and expose one lookup; feature code asks the palette and never names a colour. A colour that only one mode needs still gets an entry in the other palette, so a surface can never fall back to a literal.

New services should be registered in `Program.Main` and injected through constructors; forms created with `new` should receive dependencies from `EntryFormFactory`.

---

## 15. Known constraints and gotchas

- **Single project, internal-by-default.** Most types are `internal`; only `EnvironmentalVariableType`, `EnvironmentalVariableState` and `ITabFactory` are public.
- **Snapshot semantics.** Changes made to environment variables outside the app while it is running are not observed automatically. **View → Refresh** (`F5`) re-reads both scopes; it discards pending edits after confirmation and returns the grids to name order (the active sort is lost).
- **No undo beyond Reset/Restore**, and no backup/export (it is on the roadmap).
- **No confirmation before Apply**, only the change list itself.
- **Apply is not transactional.** A failure names the failing variable and reason in an error dialog, and rows written before it stay written; rows are cleaned one at a time, so the failing row and every row after it stay dirty. The one exception is a rename: if the new-name write fails after the old key was deleted, the old key is written back once with its original value, so that failure cannot lose the variable (a failed restore is logged).
- **Scope comes from the tab's `Tag`**, not its text: `TabFactory` tags each page with its scope, `ControlHelper.GetTabType` finds the nearest scope-carrying ancestor and `AdminChecker` consumes that scope. The tab text (`"User"` / `"Machine"`) is presentation only, and a control without a scope-carrying ancestor denies mutation instead of defaulting to `User`.
- **Corrupted validation checks every `;`-separated segment** and treats non-rooted text as valid; empty segments are ignored. The per-segment verdict is `ICorruptedPathService.ValidateSegment`, and a value is corrupted when any rooted segment is missing.
- **The corruption overlay is written on the requesting thread only.** `CorruptedPathService` scans a snapshot of `(row, value)` on a worker and posts the verdicts back to the thread that asked; a verdict is dropped when the row has left the scope or holds a different value by then, so a slow scan can never mark an edited row.
- **A scan requested before the message loop exists is not reliable.** The grid asks for its initial scan from `EnvGridFactory.Create`, which runs in the `MainForm` constructor; the verdicts can only come back through a `SynchronizationContext.Post`, and a post scheduled before `Application.Run` is pumping may never reach the rows. The startup overlay is therefore requested again from `MainForm.OnShown`, inside the loop. Any new caller that needs a scan to be seen must issue its request from a running message loop, not from construction.
- **The corrupted count reads 0 until the first debounced scan lands.** The status strip counts the overlay, and the overlay is written ~150 ms after open or refresh by the debounced worker scan, so the label truthfully says `0 corrupted` for that moment and corrects itself when the verdicts arrive (the overlay write raises `ListChanged`, which refreshes the strip).
- **Corruption is an overlay (`EnvModel.IsCorrupted`), not a `State`.** A red row cannot be applied on its own, and its edit state (`Added`/`Modified`/`Deleted`) is preserved.
- **A `State` or overlay change repaints nothing by itself.** `State` and `IsCorrupted` have no bound column — the indicator column is unbound and styled in `CellFormatting` — and `DataGridView` only invalidates cells whose `DataPropertyName` matches the changed property. `EnvGridFactory` therefore calls `grid.Invalidate()` on an `ItemChanged` event whose property is `State` or `IsCorrupted`; without it the rows keep their old colour until something else repaints them (a click, a scroll), which looks exactly like detection or delete failing.
- **`config.json` lives in `%APPDATA%\Enviro`.** A legacy `config.json` beside the executable is read once for migration and then ignored. A read-only install folder no longer prevents saving, and a failed save is logged and reported as one transient status-strip message instead of disappearing.
- **Colours come only from `Static/ThemePalette`.** A row's state/corruption pair, the selection text, the grid backgrounds, cell text, grid lines, header colours, the logo ink, the link colours, the PATH verdicts, the file-entry statuses and the read-only banner are palette lookups; feature code names no colour literal, and `SystemColors` is used only where a standard control expects a system colour. The light palette reproduces the pre-theme look exactly; the dark palette uses dark tints with light text rather than an inversion.
- **Windows does not theme `DataGridView` headers in dark mode.** The visual-style header painter ignores the colour mode, so the header row (the `Name`/`Value` captions) would stay light while the cells go dark. `GridStyleHelper.ApplyGridTheme` therefore sets `EnableHeadersVisualStyles` from `ThemePalette.SystemHeaders` — `true` for the light palette, `false` for the dark one, which then supplies `HeaderBackground`/`HeaderText` itself. The same call sets the grid lines, the default cell text and the empty-area background for all three grids.
- **The colour mode is fixed for the process.** `Program.Main` calls `Application.SetColorMode` once, before `MainForm` exists; WinForms applies the mode as each window is created, so the Options → Theme choice is written to `config.json` and only takes effect on the next start. `Application.SetColorMode`/`SystemColorMode`/`Application.IsDarkModeEnabled` are experimental (`WFO5001`), so the warning is suppressed with a local `#pragma` in `Program.cs` and `ThemePalette.cs` instead of a project-file change.
- **A failed log rotation still writes the entry.** When `enviro.log.old` is locked or read-only, the move fails and the entry is appended to the live file anyway (rewritten in place if even the append fails), so the log grows past its threshold instead of stalling; with no second backup and no retry the repeated IO failure itself stays swallowed.
- **`Debug` builds auto-generate columns**; release builds show exactly the two declared columns.
- **A `Form` does not inherit the executable's icon.** `<ApplicationIcon>` brands the `.exe` only; `Form.Icon` falls back to the WinForms default image, which is what the title bar, taskbar and Alt-Tab show. `MainForm` and `UpdateForm` assign `Properties.Resources.favicon`, and a new window that shows a title-bar icon must do the same. `FixedDialog` windows (`AboutForm`, `VariableFileForm`) display no icon, and the entry dialogs set `ShowIcon = false`.
- **Designer-generated files** (`.Designer.cs`, `.resx`) must stay in sync; hand edits should preserve `InitializeComponent` structure and resource keys.
- **The toolbar is text-only on purpose, and it keeps no sizes in the `.resx`.** The project ships no icon set, so every `toolStrip1` button is `DisplayStyle = Text` and `AutoSize`; `MainForm.resx` deliberately holds no `<button>.Size` keys, because the baked widths would clip a localized or DPI-scaled label. The strip's grouping (`toolStripSeparator1` between Add/Edit/Delete and Refresh, `toolStripSeparator2` before Apply) is decoration, not a command: nothing in the command table knows about the separators. Re-opening `MainForm` in the designer may write `Size` values back into the `.resx`; drop them again rather than accepting them.
- **`MetadataRepository` derives repo URLs from assembly metadata**, so renaming the assembly or changing `<Company>` changes the update endpoint.
- **The rows are service-owned.** `IEnvScope.Rows` is the live `IBindingList` the grid binds to, handed out so `ListChanged` keeps flowing; adding, removing, reordering and refilling happen only inside `EnvScope` (`Add`/`Delete`/`Sort`/`Replace`/`MarkApplied`). A consumer that mutates the list itself breaks change tracking and the apply cleanup.
- **`EnvironmentalVariableType` has exactly two members** (`User`, `Machine`). The retired `All` member was dead and self-contradictory (`GetVariablesByType` mapped it to Machine, `EnvTypeConverter.ToTarget` to User); there is no replacement — ask for the scope you want. Both scope↔target mappings (`EnvTypeConverter.ToTarget` and `IEnvService.GetScope`) are exhaustive over the two members and throw `ArgumentOutOfRangeException` for anything else, so an added member fails loudly instead of writing to a wrong hive (§14.5).
- **The Variables and View menus are added in code**, not by the designer: `CreateCommandSurfaces()` inserts them into `menuStrip1` after File, in the order File, Variables, View, Options, Help. Re-opening `MainForm` in the WinForms designer will not show those menus, and an item added in the designer instead would sit outside the command table.
- **`--window-bounds` is accepted only when it fits one screen.** `WindowGeometry.IsOnScreen` requires at least 200×150 and full containment in one screen's working area (`Rectangle.Contains`), so a rectangle that would land partly off-screen is centred instead. The screen list still lives in the form; only the check was tightened.
- **The app is Windows-only**, including the Linux build workaround `-p:EnableWindowsTargeting=true` (compilation only, not execution).

---

## 16. Build and release shape

- SDK pinned by `global.json` (`10.0.0`, `latestMajor`, prereleases disallowed).
- `enviro.csproj` sets `WinExe`, `net10.0-windows`, nullable, implicit usings, assembly name `Enviro`, the version (`<Version>`, the one place the number is written — `MetadataRepository` reports it from the assembly and the docs quote no value), application icon, and references DI plus the Windows API Code Pack.
- CI (`.github/workflows/build.yml`) runs on `windows-latest`, restores/builds `enviro.slnx` in Release, then publishes a self-contained single-file `win-x64` binary to `./publish/win-x64` and uploads it as an artifact.
- No test project is part of the solution; `tests/` contains only ignored build output.
