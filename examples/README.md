# Examples

`SWASI_6D_Example_Ideal/` is the small maintained reference case:

- `Solidworks/Assembly_UFC_Glas_6D_ideal.SLDASM` and its parts are the CAD inputs.
- `SWASI_Exports/assemblies/` and `SWASI_Exports/components/` contain historical
  reference JSON and meshes for inspecting the data format and coordinate values.

Open the assembly in SolidWorks and export to a **new output directory**. Existing
reference JSON includes authored constraints and historical IDs, so fresh output
will not be byte-identical. Compare geometry and schema; re-export tests check
preservation of manually authored data separately.

`archive/` retains the other demonstrators, experiment logs, paper figures,
research notes, and the old `Files/` and `exampleParts/` material. Their internal
directory structures are preserved. They are historical data, not automated test
expectations. Do not place new routine export logs in the maintained example.
