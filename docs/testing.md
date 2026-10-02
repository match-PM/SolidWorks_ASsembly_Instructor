# Building and testing

Prerequisites: Windows, Visual Studio/MSBuild with the .NET Framework 4.8 targeting
pack, and Python 3.9 or newer for the constraint-transfer utility tests.

From PowerShell in the repository root:

```powershell
./tools/build.ps1 -Configuration Debug -Restore
./tools/build.ps1 -Configuration Release
```

Use `-CoreOnly` to build the core and its tests without compiling the add-in.
`-Restore` downloads the pinned NuGet package into the repository's `packages/`
directory. Subsequent builds can run offline with the restored package.

The C# test project is a dependency-free executable regression runner. Run it
directly after building:

```powershell
./tests/Swasi.Core.Tests/bin/Debug/Swasi.Core.Tests.exe
python -m unittest discover -s tests -p 'test_*.py'
```

Both return nonzero on failure. The C# runner is not a `dotnet test` project.
CI uses the same build script on Windows without launching SolidWorks.

Coverage includes CAD unit conversion, rotation convention, inverse and relative
transforms, invalid matrices, feature naming and flags, JSON field compatibility,
merge precedence and frame retention, component identity and membership,
non-mutating/idempotent merges, partial results, JSON replacement, and the Python CLI.
The JSON fixture is copied from the maintained ideal 6D example; it is intentionally
frozen rather than regenerated during tests.

## Manual SolidWorks acceptance checks

These require an installed SolidWorks instance and are not covered by the core
runner. Close SolidWorks before replacing a loaded add-in DLL. Register the new
add-in output path and copy its dependency DLLs alongside it.

1. Open the ideal 6D assembly from `examples/SWASI_6D_Example_Ideal/Solidworks`.
   Export to a new directory and check one assembly JSON/STL plus its component files.
2. Compare JSON translations, rotations, planes, axes and mates with the reference
   exports. Inspect mesh placement using the named SWASI origin.
3. Repeat with a rotated assembly and a subassembly containing disjoint bodies.
   Check that direct subassembly STL export preserves all bodies and the correct origin.
4. Modify a frame's constraints in the exported JSON, move a CAD feature, and
   re-export. Confirm the geometry updates, authored constraints survive, and GUIDs
   remain stable. Add/remove a component and verify current membership.
5. Include a suppressed component and a part without a SWASI origin. Confirm the
   summary identifies skips. A read-only output or failed mesh export must report failure.
6. Record STL preferences and open documents before export. Confirm preferences,
   the active document and open user documents are preserved after success and failure.
7. With no active document, or with a drawing active, confirm a clear failure message.

The refactor has automated coverage of the core logic, not a substitute for this
host-level acceptance pass.
