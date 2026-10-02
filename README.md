# SolidWorks ASsembly Instructor (SWASI)

SWASI is a SolidWorks add-in that exports parts and assemblies to JSON mounting
descriptions and STL meshes for downstream assembly planning.

## Quick start

1. Install Visual Studio with MSBuild and the .NET Framework 4.8 targeting pack.
   SolidWorks is required to run the add-in; the included interop DLLs allow a build
   without launching SolidWorks. The original project targets SolidWorks 2020;
   verify other host versions with the acceptance checks below.
2. Open `SolidWorks_ASsembly_Instructor.sln`, restore NuGet packages, and build.
   To also run regression tests, install Python 3.9+ and use:

   ```powershell
   ./tools/build.ps1 -Configuration Debug -Restore
   ```

3. Register `src/Swasi.AddIn/bin/Debug/SolidWorks_ASsembly_Instructor.dll` using
   the [SolidWorks Add-In Installer](https://github.com/angelsix/solidworks-api/tree/develop/Tools/Addin%20Installer).
   Keep all DLLs from that output directory together, including `Swasi.Core.dll`.
   If upgrading from the old layout, deregister the previous DLL location and
   register this new location. Close SolidWorks when replacing a loaded DLL.
4. Enable the add-in in SolidWorks, select an output folder in its task pane, and
   choose **Export as JSON**. The summary lists successful, skipped and failed
   documents; each successful document has JSON and STL output.

The add-in is a DLL, so debug it by attaching Visual Studio to `SLDWORKS.exe`.

## Project layout

```text
src/
  Swasi.AddIn/          COM integration, WinForms UI, export coordination, SolidWorks adapters
  Swasi.Core/           JSON models, geometry, naming rules, results, merge and persistence
tests/                Core regression runner, fixtures and Python utility tests
examples/             Maintained ideal 6D example and historical archive
tools/                Build/test script and constraint-transfer CLI
docs/                 Architecture, data conventions and testing instructions
```

- [Architecture and merge rules](docs/architecture.md)
- [Build, automated tests and SolidWorks acceptance checks](docs/testing.md)
- [Example inputs and reference exports](examples/README.md)
- [Command-line tools](tools/README.md)

## Feature naming

Reference frames, planes, axes and points must start with `SWASI_`.
Use `SWASI_Origin_` for the export origin, for example
`SWASI_Origin_Assembly_Origin`. Each exported document needs a unique valid origin.
Features without the prefix are ignored. Feature names are case-sensitive.

Reference planes support definitions by an axis and a SWASI point, or by three
SWASI points. Reference axes use two SWASI points. Defining reference features
must also follow the naming convention.

JSON field names and coordinate conventions are retained. Re-export preserves
existing IDs and authored frame constraints while updating extracted geometry;
see the architecture document for the exact precedence and retention rules.

## Deutsche Kurzanleitung

Das Add-in exportiert Bauteile und Baugruppen als JSON-Montagebeschreibungen und
STL-Dateien. Die Solution mit Visual Studio und dem .NET-Framework-4.8-Targeting-Pack
bauen; NuGet-Pakete vorher wiederherstellen. Fuer Build und Tests kann das oben
angegebene PowerShell-Skript verwendet werden (Python 3.9+ erforderlich).

Die DLL liegt jetzt unter `src/Swasi.AddIn/bin/Debug/`. Diesen neuen Pfad mit dem
SolidWorks Add-In Installer registrieren und die abhaengigen DLLs im selben Ordner
belassen. SolidWorks vor dem Austausch geladener DLLs schliessen.

Im Taskpane einen Ausgabeordner waehlen und **Export as JSON** starten. Die
Zusammenfassung unterscheidet erfolgreiche, uebersprungene und fehlgeschlagene
Dokumente. Referenzen muessen mit `SWASI_`, der eindeutige Exportursprung mit
`SWASI_Origin_` beginnen. Beim erneuten Export bleiben vorhandene IDs und manuell
bearbeitete Frame-Constraints erhalten. Historische Demonstratoren und Messdaten
liegen unter `examples/archive/`.
