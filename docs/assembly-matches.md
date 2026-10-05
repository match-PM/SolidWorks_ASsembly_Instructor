# Assembly frame matches

The frame-matching workflow creates a native coordinate-system mate between two
coordinate systems (`swMateCOORDINATE`). Their origins and corresponding X/Y/Z axes coincide; there
is no offset or inverted-axis option. Existing plane mates may be used elsewhere
in the same assembly. Conflicting mates or fixed components cause Apply to fail.

Only loaded, unsuppressed first-level component instances are offered. A
subassembly's own typed frames are eligible; its nested components are not.
Resolve lightweight components if the dialog reports them as unavailable.
Assembly and Target types are allowed only on coordinate-system frames, including
constraint-created frames. Other types remain available on points.
Each row's dropdown excludes frames selected in other rows of the same role,
including pending changes. Changing, clearing, or removing a match immediately
makes its former selections available again. The current row's selections remain
visible for editing.

Matches have stable IDs and are stored in the parent assembly's SWASI metadata,
scoped by active configuration. Persistent SolidWorks references resolve renamed
components, frames, and mates where those references remain valid. Missing or
suppressed mates are shown in the dialog. Apply can recreate/restore them;
missing endpoints must be reassigned or the row removed. Save the assembly to
persist edits to disk. Cancel does not apply the pending rows.

## Export structure

`mountingDescription.assemblyConstraints` contains both workflows. Every entry
has `type: "PlaneMatch"` or `type: "FrameMatch"`, `component_1`, and `component_2`.
Plane entries retain their `move_component_1` movement setting and existing
`description` with three plane matches. Frame entries contain `assemblyFrame`,
`targetFrame`, and `alignAxes`; they omit the movement flag and empty plane
description. The separate `frameMatches` array is no longer exported.

For frame matches, component 1 is always the Assembly-frame component that moves
to component 2, the Target-frame component. These roles determine the assembly
direction without a movement flag or GUI movement selector. Previously saved
frame movement overrides are ignored. Native SolidWorks mates still obey fixed
components and existing mate constraints.

Plane movement is written as `move_component_1`; the previous spelling
`moveComponent_1` is still accepted when reading old plane constraint models.

This fragment shows the new fields; normal GUID, pose, and other fields remain
in the full export:

```json
{
  "mountingDescription": {
    "components": [
      {
        "name": "Carrier-1",
        "frameProperties": {
          "Target_1": {
            "assemblyProperties": {
              "isAssemblyFrame": false,
              "isTargetFrame": true,
              "associatedFrame": "Mount_1",
              "associatedComponent": "Module-2"
            }
          }
        }
      },
      {
        "name": "Module-2",
        "frameProperties": {
          "Mount_1": {
            "assemblyProperties": {
              "isAssemblyFrame": true,
              "isTargetFrame": false,
              "associatedFrame": "Target_1",
              "associatedComponent": "Carrier-1"
            }
          }
        }
      }
    ],
    "assemblyConstraints": [
      {
        "name": "(auto)SWASI_ModuleMount_a1b2c3d4",
        "type": "FrameMatch",
        "component_1": "Module-2",
        "component_2": "Carrier-1",
        "assemblyFrame": {
          "component": "Module-2",
          "configuration": "Default",
          "frame": "Mount_1"
        },
        "targetFrame": {
          "component": "Carrier-1",
          "configuration": "Default",
          "frame": "Target_1"
        },
        "alignAxes": true
      }
    ]
  }
}
```

`component` is the exact first-level instance name, not the part filename.
`configuration` is that instance's referenced component configuration.
`frame` is the exported SWASI frame name without the feature prefix.
`frameProperties` is an assembly-context property overlay keyed by frame name;
merge its property groups onto the referenced component's frame properties.
Component files and shared component JSON definitions do not acquire an
assembly-specific association. Each instance can therefore be matched differently.

Empty instance property dictionaries are omitted. Re-export replaces the
assembly constraint list and instance property overrides, so removing a match also
removes its exported associations. Internal persistent-reference bytes are never
exported. Invalid or suppressed saved matches fail assembly export explicitly
instead of silently describing mates that are not active.

## Validation

Automated tests cover frame-only type eligibility, endpoint uniqueness, component
instance/configuration identity, persistence, mixed plane/frame export, and stale
association removal. `tools/AssemblyMatchesProbe.cs` provides the live SolidWorks
check: it creates its own temporary part and assembly, mates two instances,
checks aligned origins/axes, unchanged Apply, save/reopen, read-only component
metadata, export readiness, and removal of the generated mate. It also checks
first-level-only enumeration and restoration of the prior match/mate when a
rematch conflicts with fixed components. These checks passed in SolidWorks 2025.
It does not modify
user documents. Temporary test files are retained for diagnosis.

Run after building, using Windows PowerShell:

```powershell
$interop = 'C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\SolidWorks.Interop.sldworks.dll'
$core = (Resolve-Path 'src/Swasi.Core/bin/Debug/Swasi.Core.dll').Path
$addin = (Resolve-Path 'src/Swasi.AddIn/bin/Debug/SolidWorks_ASsembly_Instructor.dll').Path
$json = (Resolve-Path 'src/Swasi.AddIn/bin/Debug/Newtonsoft.Json.dll').Path
foreach ($path in @($interop, $core, $json, $addin)) { [Reflection.Assembly]::LoadFrom($path) | Out-Null }
Add-Type -Path tools/AssemblyMatchesProbe.cs -ReferencedAssemblies $interop,$core,$json,'System.Numerics','System.Core'
[AssemblyMatchesProbe]::Run('C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2025\templates\Teil.PRTDOT', 'C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2025\templates\Baugruppe.ASMDOT', $addin)
```
