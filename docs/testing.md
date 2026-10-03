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
The runner also links the constraint number control to exercise WinForms decimal
input under German, English, and French cultures. Both `10.00` and `10,00` mean
ten; thousands separators are not supported in constraint fields. Check typing,
pasting, leaving the field, pressing Enter/Apply, and using the spinner arrows
in SolidWorks when validating the constraint editor manually.

Coverage includes CAD unit conversion, rotation convention, inverse and relative
transforms, centroid/orthogonal/transform calculation, in-plane validation,
feature naming and roles, JSON field compatibility, metadata-aware merge precedence,
frame retention, component identity and membership,
non-mutating/idempotent merges, partial results, JSON replacement, and the Python CLI.
The JSON fixture is copied from the maintained ideal 6D example; it is intentionally
frozen rather than regenerated during tests.

## Manual SolidWorks acceptance checks

`tools/ConstraintPrecisionProbe.cs` exercises the actual add-in in an unsaved
part: native source coordinate systems, orthogonal and transform constraints,
generated helper points, and three native planes through those points. It checks
parallelism to the source plane and mutual perpendicularity below 1e-10 radians.
It also simulates old rounded helper-point positions and checks that Update
repairs the dependent planes. Compile through Windows PowerShell `Add-Type` with
the SolidWorks interop, built `Swasi.Core.dll`, `System.Numerics` and `System.Core`
references, then call `ConstraintPrecisionProbe.Run(templatePath, addInDllPath)`.
Like the coordinate probe below, it closes only its own unsaved test part.
The precision regression measured a maximum perpendicular error of 7.13e-13
degrees and a parallel error below 1.26e-12 degrees in SolidWorks 2025, including
the update of previously rounded helper points.

`tools/CoordinateSystemProbe.cs` provides a live API regression check using an
unsaved part and the installed SolidWorks interop DLL. It checks numerical XYZ
rotations, all 24 signed orthogonal axis combinations, and the HYENA module
normal after export conversion. It closes its test document without saving and
exits SolidWorks if no other documents are open. Run from the repository root
after a Release build (adjust the installed version/template path if needed):

```powershell
$interop = 'C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\SolidWorks.Interop.sldworks.dll'
$core = (Resolve-Path 'src/Swasi.Core/bin/Release/Swasi.Core.dll').Path
[Reflection.Assembly]::LoadFrom($interop) | Out-Null
[Reflection.Assembly]::LoadFrom($core) | Out-Null
Add-Type -Path tools/CoordinateSystemProbe.cs -ReferencedAssemblies $interop,$core,'System.Numerics'
[CoordinateSystemProbe]::Run('C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2025\templates\MBD\part 0051mm to 0250mm.prtdot')
```

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
   summary identifies skips. A valid parent assembly must still export JSON/STL;
   untagged components and their mates must be absent from its JSON, and no files
   must be written for those parts. Missing origins are warnings, not parent
   extraction errors. Duplicate origins and genuine extraction errors must still
   fail rather than being downgraded. A read-only output or failed mesh export must report failure.
6. Record STL preferences and open documents before export. Confirm preferences,
   the active document and open user documents are preserved after success and failure.
7. With no active document, or with a drawing active, confirm a clear failure message.
8. Rename an exact `SWASI_Origin`, select each frame-role icon, edit its properties
   and component color, save/reopen the document, and confirm the UI values persist.
9. Create centroid, orthogonal, and transform frames from points and coordinate
   systems. Move each defining reference, finish the native command and click
   **Update frames**. For orthogonal frames, also use a rotated SWASI origin and
   a tilted plane whose normal has a positive X but negative Z component. Select
   normal axis Z: the frame Z axis must be perpendicular to the plane and have
   a positive projection on SWASI-origin Z. Select -Z to reverse it; reverse the
   reference-point order and verify the same normal direction. Test all distinct
   normal/orthogonal axis pairs, including orientations at +/-90 degrees pitch.
   If the normal is exactly perpendicular to the selected SWASI axis, its sign
   uses the largest normal component as a deterministic fallback.
   Move each defining reference again, then click
   **Update frames**; confirm the
   `(auto)SWASI_<name>` coordinate system moves and remains selectable/exported.
   Confirm no helper sketches are created. Verify the shared `(auto)_SWASI_helper`
   folder contains three hidden planes and one hidden axis per frame, with each
   visible reference point outside the helper folder, in the separate `SWASI`
   folder alongside generated frames and regular SWASI points, axes, planes and
   origins. Confirm helper axes are after their parent planes inside the helper
   folder. Add a regular SWASI plane after creating frames, then click Update frames
   and verify it joins the main folder without changing its references. Verify
   both folders remain separate and repeated refresh does not duplicate them.
   Also test a pre-existing empty SWASI folder: Update frames must attempt to move
   each named feature into it. A rejected folder move must be shown in the update
   warning with the affected feature names, rather than silently leaving it empty.
   For older documents, click Update frames and
   verify points move out of the folder while dependent planes retain references.
   Create multiple frames and ensure the folder is reused. Check
   origins with positive, negative and zero XYZ values (including crossing zero
   during an edit), in both parts and assemblies. Build a plane using generated
   origin points (and any additional reference needed to define a plane), preview,
   accept and cancel the command. Confirm no SWASI synchronization/folder moves
   occur during native previews or rebuilds and selections remain intact.
   Move a constraint frame, click Update frames, and verify the point and user plane
   update without losing references. Export: expect only the coordinate frames,
   with plane/axis point references resolving to their owning frame names.
   Right-click a generated frame and choose **Rename constraint frame...**.
   Verify the coordinate system, origin point and helpers receive the new name,
   dependent centroid/orthogonal/transform/in-plane rules use it, and user planes
   retain their point references. Reject empty, reserved and duplicate names;
   cancel without changes. Save/reopen, update and export to verify persistence.
   Delete one frame and confirm its helpers are removed while other frames and
   their helpers remain. Save/reopen and repeat rebuild/export.
   If the host rejects the auxiliary
   reference point, confirm a specific warning is logged and the coordinate system
   and metadata remain present. Rebuild again without changing references: the
   missing point must not cause coordinate-system replacement. Save/reopen and
   export to confirm the retained frame remains usable.
10. Assign an in-plane rule to an existing point/frame. Confirm an in-plane case
    passes, an out-of-plane case reports its distance, and neither changes its pose.
    Confirm the points and frames list shows `In-plane` in the Constraint column,
    including alongside `Centroid`, `Orthogonal` or `Transform` for generated frames.
    Remove the in-plane rule and confirm only that indicator disappears. Save/reopen
    and verify the indicator persists for assigned rules.
11. Export to a typed UNC path and confirm the final log window reports each file.
12. Create `SWASI_X_Plane` using generated references. Record its parents and the
    feature tree, then export twice. Confirm the plane, its references, all other
    features and folder membership remain intact, and `X_Plane` is in the JSON
    `ref_planes` list. Repeat after changing a constraint's defining geometry
    without clicking **Update frames**: export must use the existing frame geometry
    without replacing frames or synchronizing helpers. Repeat with a failing output
    path and with the part exported as an assembly component. Recalculate constraints
    explicitly with **Update frames** before exporting when updated poses are needed.
13. Define a three-point plane using two `(auto)SWASI_<frame>_Point` features and one
    regular `SWASI_<point>`. Export and check all three JSON point references: the
    generated points must resolve to their frame names, and the regular point must
    retain its point name. Repeat with three regular points and three generated
    points. An untagged reference must report its actual name; an unresolved entity
    must report a resolution error, not a naming-convention error. For a point/axis
    plane, both the point and axis must be resolved.
    Also test one `SWASI_Gripping_Point` plus `(auto)SWASI_Vision_12` and
    `(auto)SWASI_Top_helper` coordinate systems: expect `refPointNames` to contain
    `Gripping_Point`, `Vision_12`, and `Top_helper`, in reference order. Any mix of
    three points and coordinate-system origins, including three coordinate systems,
    must export. A coordinate-system name ending in `_Point` must keep that suffix.

The refactor has automated coverage of the core logic, not a substitute for this
host-level acceptance pass.
