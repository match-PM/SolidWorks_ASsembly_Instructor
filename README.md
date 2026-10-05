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
   choose **Export JSON and STL** at the bottom. A log window opens when the run
   finishes; each successful document has JSON and STL output.

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

## Frame editor and constraints

**Assembly Matches** is enabled only for assemblies. Assign **Assembly** or
**Target** to coordinate frames in the component documents, then open the assembly
and click **Assembly Matches**. Points cannot receive these two types.
The dialog lists first-level component instances only; subassembly contents are
not traversed. Add a row, name the match, and select an Assembly and a Target
frame on different instances. Each frame can participate once in that role.
Apply creates a coordinate-system mate with coincident origins and aligned axes named
`(auto)SWASI_<match-name>_<id>`. Existing matches are loaded when reopening the
dialog. Edit a row to rematch it, or remove the row and Apply to delete its mate.
Matches are stored in the parent assembly for its active configuration.

The association is not written into the component document. During assembly
export, `mountingDescription.assemblyConstraints` contains both `"PlaneMatch"` and
`"FrameMatch"` entries, identified by `type`. Frame matches include `component_1`
(Assembly, the moving component) and `component_2` (Target). Frame matches omit
`move_component_1` because their roles determine the direction; plane matches
retain their movement setting. Each matched component instance also
has `frameProperties` overrides containing `assemblyProperties.associatedFrame`
and `associatedComponent`. These override the shared component definition for
that assembly instance; this avoids ambiguity when the same part is inserted
more than once. See [the JSON schema example](docs/assembly-matches.md).

To duplicate a constraint-created frame, select it in the task pane and use
**Copy constraint frame** and **Paste constraint frame...** from the right-click
menu, or press **Ctrl+C** and **Ctrl+V** while the frame list has focus. Paste opens
the constraint editor with the copied settings and a suggested unused name.
Change the name and settings, then choose **Apply** to create the new frame.
Copies retain frame types, properties, and in-plane checks; editing a copy does
not change the source. The copied frame stays available for repeated pastes
until another frame is copied or the add-in is unloaded.

Error and update-warning dialogs have selectable text and a **Copy** button.
For errors, Copy includes diagnostic details that can be pasted into a bug report.
Deleted frames do not reserve their names through leftover metadata; creating or
pasting a frame with a reused name replaces that name's stale settings.

Constraint fields display nine decimal places and preserve the stored value
when left unedited. CAD reference reads, constraint calculations, numerical
frame creation and helper-point placement use double precision. Reload the
rebuilt add-in and run Update to recalculate existing frames and correct old
helper points. Values already saved with rounding cannot be recovered
automatically. Legacy JSON models and their stored inputs retain their existing
numeric types; they are converted at the calculation boundary.

The task pane stores frame types, their properties, component color, and
constraints in the SolidWorks document. Select a SWASI point or coordinate system,
then use an icon to assign Vision, Laser, Gripping, Target, Assembly, or Glue.
The property grid edits the associated gripper, target/assembly, and glue values.
The output field accepts local paths and UNC paths such as `\\server\share\folder`.

The origin selector renames `SWASI_Origin` (or an existing named SWASI origin) to
`SWASI_Origin_<selection>`. Centroid, orthogonal, and transform actions create an
actual `(auto)SWASI_<name>` numerical coordinate-system feature without helper
sketches. A reference point at its origin is constructed using three offset
planes and an axis, grouped in `(auto)_SWASI_helper`. The construction planes
and axis are hidden. The visible point, generated frame, and regular `SWASI_`
features (including planes, axes and origin) are grouped in a separate `SWASI`
folder. Grouping runs through **Update frames** and constraint edits; existing features are moved without
recreation. SolidWorks dependency restrictions that prevent a move are logged.
The point stays selectable for defining user planes. Offset
planes are edited in place during synchronization, preserving the point and its dependents.
Only the frame is exported: the generated point resolves to its frame name in
exported plane/axis references, and construction geometry is excluded. If helper
creation fails, the frame is retained and a detailed warning is logged.
After changing defining geometry, finish the SolidWorks command and click
**Update frames** to recalculate constraints before exporting. Export reads the
current geometry without synchronizing frames, organizing folders or forcing a rebuild.
Native rebuild notifications and ordinary UI refreshes do not modify the model,
so creating planes or editing features does not trigger nested SWASI rebuilds.
Unchanged helper sets skip selection access, modification and rebuilding. Point
references inherit the SWASI-origin orientation. **Assign in-plane** attaches a
validation rule to the selected existing point or frame; it reports an out-of-plane
distance without moving that feature.

## Deutsche Kurzanleitung

Das Add-in exportiert Bauteile und Baugruppen als JSON-Montagebeschreibungen und
STL-Dateien. Die Solution mit Visual Studio und dem .NET-Framework-4.8-Targeting-Pack
bauen; NuGet-Pakete vorher wiederherstellen. Fuer Build und Tests kann das oben
angegebene PowerShell-Skript verwendet werden (Python 3.9+ erforderlich).

Die DLL liegt jetzt unter `src/Swasi.AddIn/bin/Debug/`. Diesen neuen Pfad mit dem
SolidWorks Add-In Installer registrieren und die abhaengigen DLLs im selben Ordner
belassen. SolidWorks vor dem Austausch geladener DLLs schliessen.

Im Taskpane einen Ausgabeordner waehlen und unten **Export JSON and STL** starten.
Danach zeigt ein Logfenster erfolgreiche, uebersprungene und fehlgeschlagene
Dokumente. Referenzen muessen mit `SWASI_`, der eindeutige Exportursprung mit
`SWASI_Origin_` beginnen. Beim erneuten Export bleiben vorhandene IDs und manuell
bearbeitete Frame-Constraints erhalten, solange sie nicht im neuen Dokumenteditor
ersetzt wurden. Historische Demonstratoren und Messdaten
liegen unter `examples/archive/`.
