# Import constrained frames

Use **Import constrained frames...** in the task pane and select a SWASI JSON
file. A selection dialog opens with all constrained frames checked by default.
Uncheck frames you do not want, or use **Select all** / **Select none**, then click
**Import selected**. Cancel makes no changes. Dependencies must already exist in
the current document or also be selected; unchecked JSON frames are not created
automatically. Selecting no frames makes no changes.

The active part or assembly must have a SWASI origin whose suffix exactly
matches `mountingDescription.mountingReferences.spawningOrigin` (case sensitive).
For example, `SWASI_Origin_Spawn` matches `"spawningOrigin": "Spawn"`.
A mismatch stops the import before anything is created.

The importer reads the current document's `ref_frames` array, not nested
components. It supports centroid, orthogonal, and transform creation constraints.
Exactly one creation constraint is required per frame. Optional in-plane checks
and frame properties/types are retained. Assembly counterpart associations are
not imported into component metadata.

Dependencies may be existing SWASI points/frames or other constrained frames in
the JSON. JSON order does not matter. A dependency becomes available only after
successful creation. Missing references, cycles, duplicate names, invalid
constraints, and CAD creation failures are skipped while independent frames
continue. Existing names are kept unchanged and may satisfy dependencies.
Ordinary frames without creation constraints are not created from their saved
poses. Imported poses are calculated from the current document's references.

A copyable completion report lists created and skipped frames, with reasons.
Save the CAD document to persist the new geometry and editable constraints.
Reimporting the same file keeps already existing frames; remove or rename a frame
first if you want to import a replacement.

Use millimeters for distances and centroid/transform offsets (`documentUnits`
may be omitted or set to `"mm"`). Orthogonal `unit_distance_from_f1` accepts
`"mm"` or `"%"`. Frame names and dependency names omit the SWASI feature prefix.

Minimal example, assuming `Base` exists in the current document:

```json
{
  "documentUnits": "mm",
  "mountingDescription": {
    "mountingReferences": {
      "spawningOrigin": "Spawn",
      "ref_frames": [
        {
          "name": "Offset_2",
          "type": "frame",
          "constraints": {
            "centroid": {
              "refFrameNames": ["Offset_1"],
              "offsetValues": [0, 5, 0]
            }
          }
        },
        {
          "name": "Offset_1",
          "type": "frame",
          "constraints": {
            "centroid": {
              "refFrameNames": ["Base"],
              "offsetValues": [10, 0, 0]
            }
          }
        }
      ]
    }
  }
}
```

Here `Offset_1` is created first despite appearing second in the JSON. Offsets are
local to the reference orientation, using the same constraint engine as manual
creation. No stored `transformation` is needed on the generated frame entry.
