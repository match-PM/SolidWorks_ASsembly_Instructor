# Architecture and maintenance

The solution targets .NET Framework 4.8. The add-in retains its assembly name,
namespace, assembly GUID and task-pane ProgID so COM registration remains familiar.
The DLL's location changed: register the copy under `src/Swasi.AddIn/bin/<configuration>`.
Deploy the whole output directory, including `Swasi.Core.dll` and `Newtonsoft.Json.dll`.

## Boundaries

| Area | Responsibility |
| --- | --- |
| `Swasi.AddIn/Integration` | COM registration and task-pane lifetime |
| `Swasi.AddIn/UI` | Folder selection, frame metadata, constraint editing and export logs |
| `Swasi.AddIn/Export` | Coordinating a complete export and assigning persistent document identities |
| `Swasi.AddIn/SolidWorks` | Reading features/mates, document metadata, managed constraint frames, STL and host state |
| `Swasi.Core/Models` | Existing JSON contract classes |
| `Swasi.Core/Geometry` | Matrix conversion, inversion and units |
| `Swasi.Core/Export` | Feature naming rules and export result types |
| `Swasi.Core/Serialization` | Re-export merge policy and staged JSON file writes |

`Swasi.AddIn` references `Swasi.Core`; Core has no SolidWorks or Windows Forms
dependency. The UI calls `ExportCoordinator.Run()` and displays its per-document
results. Extraction services receive the document they need explicitly. They do
not depend on a mutable, shared `currentDoc`.

SolidWorks calls remain on the calling UI thread. Do not move this workflow into
`Task.Run` without first designing and validating the COM threading model.

## Export behavior

The coordinator exports the active document and the direct components of an
assembly, matching the previous traversal scope. Repeated instances of the same
document are exported once. Distinct documents with the same output name cause
an explicit failure instead of silently overwriting each other.

Each document produces a success, skip or failure result. Missing/suppressed
documents and documents without a unique SWASI origin are skipped. Errors from
feature extraction, STL export or JSON persistence are failures. A mixture is
reported as a partial export; an empty run is not a success.

Subassemblies still use the component JSON shape with `type: "Assembly"`. Their
mesh is now exported directly from the assembly with the one-file STL setting.
The old conversion to a temporary part depended on a German template path and
closed source documents. That conversion path has been removed. Direct export
also retains the assembly's named origin and separate bodies.

Document activation is restored at the end of a run. User documents remain open.
STL preferences (binary format, units, quality, combined components, positive
translation and coordinate system) are captured and restored in `finally` blocks.
Selection access for coordinate systems, planes and axes is released on errors.

STL and JSON files are staged individually before replacement. The pair is **not**
a filesystem transaction: a JSON write failure after an STL replacement can leave
the old JSON alongside the new mesh. The document is reported as failed in that
case. Concurrent exports to the same output directory are not supported.

## Re-export merge contract

`ExportMergePolicy.Merge(generated, existing)` returns a new JSON object and does
not mutate either input:

1. Existing non-null document `guid` and `saveDate` survive.
2. For matching frame names, generated geometry wins. Properties edited in the
   document UI override existing JSON properties. Constraints created in the UI
   override existing JSON constraints; legacy manually edited constraints survive
   until a document-backed constraint replaces them.
3. Frames present only in the existing JSON are retained because they may have
   been manually authored. This also retains CAD-deleted frames; remove obsolete
   frames explicitly from the saved JSON when appropriate.
4. Generated component membership and transformations win. Existing GUIDs survive
   for component instances with matching names; removed components are not resurrected.
5. Existing component color survives until a color is explicitly chosen in the
   document UI. Other fields come from the newly generated model. Missing optional
   arrays do not prevent identity preservation.

Frame metadata is compressed and split across document custom properties named
`SWASI_Metadata_v1*`. Managed constraint coordinate systems use numerical values
and create no helper sketches. Only the numerical coordinate system is replaced
when its pose changes. `ConstraintOriginPointManager` maintains three offset
planes, their intersection axis, and an axis/plane intersection reference point.
The base planes are found in feature-history order and their transforms determine
signed offsets, independent of localized feature names. Zero offsets use coincident
planes. Existing planes are modified in place, retaining the axis, point and any
user features that reference the point. The three planes and axis are placed in the
shared `(auto)_SWASI_helper` folder. The visible point remains outside it; points
grouped by earlier versions are moved outside on update without recreation.
`SwasiFeatureFolders` groups regular `SWASI_` features and generated frames/points
in a separate `SWASI` folder on explicit constraint updates. Folder features
and legacy construction sketches are excluded. Helper axes are appended after
their parent planes, and folder membership is checked after moves. Rejected
dependency-sensitive moves are logged rather than deleting/recreating geometry.
Folder organization also guards rebuild callbacks. Construction names deliberately
do not match the export prefix. Generated points are skipped by frame extraction;
plane/axis references to them resolve to the owning exported frame name.
Native rebuild callbacks do no model work and enqueue no UI/model work. Automatic
rebuild-driven synchronization is disabled: users synchronize with Update frames
or constraint edits after completing native feature commands. Export reads current
geometry without constraint synchronization, folder organization or a forced rebuild;
coordinate-system replacement during synchronization can delete dependent user features.
UI reloads and export coordinate-system reads
are read-only and coordinate transforms are read through
`GetCoordinateSystemTransformByName`, without `AccessSelections` rollback.
Complete helper sets already at the desired position return before selection,
plane editing, folder moves or rebuilding. This avoids repeated full synchronization
and nested rebuilds during plane creation and other native operations.
Point/helper failures are logged without rolling back the coordinate system or its
metadata. Legacy `SWASI_CONSTRAINT_HELPER_*` names are retained only for cleanup.

This replaces the previous array-size-dependent precedence. JSON property names,
including their existing capitalization, are intentionally retained. C# method
and type names can be normalized without renaming serialized fields. New contract
changes should receive explicit migration rules and regression fixtures.

## Geometry conventions

JSON translations and distances use millimeters. SolidWorks transform arrays use
meters for translation, so `CoordinateTransforms.FromCadArray` multiplies those
entries by 1000. STL exports retain the previous meter setting; consumers must
continue to interpret mesh and JSON units accordingly.

SWASI's established matrix representation stores translation in `M14/M24/M34`.
The CAD rotation array is transposed into the upper-left 3x3 matrix. Existing
quaternion conversion and multiplication order are preserved. These matrices
must not be passed directly to `Vector3.Transform`, whose translation convention
differs. Singular inverses fail explicitly.

This refactor does not redefine the legacy coordinate convention. Rotated CAD
assemblies and plane normals remain part of the manual SolidWorks acceptance
checks in [testing.md](testing.md).

## Dependencies and examples

The only NuGet dependency is Newtonsoft.Json 13.0.3, pinned in `packages.config`.
Framework-provided numerics types replace redundant vector/tuple package
references. SolidWorks interop DLLs remain checked in under `Swasi.AddIn/dlls`
so compilation does not require launching or installing SolidWorks. Keep their
versions aligned with the supported SolidWorks installation.

The maintained example is documented in [examples/README.md](../examples/README.md).
Historical experiments were moved together to `examples/archive`, retaining
their internal paths. A frozen example JSON copy in the test fixtures protects
merge and coordinate behavior without requiring CAD.
