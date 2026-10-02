# Developer tools

`build.ps1` builds the solution and runs both regression suites. See
[testing.md](../docs/testing.md) for prerequisites and switches.

`constraints_transfer.py` replaces the old `contraints_transferer.py`, which had
machine-specific desktop paths. It needs only the Python standard library:

```powershell
python tools/constraints_transfer.py authored.json fresh-export.json --output merged.json
```

Omit `--output` to replace the target file. Matching frame names receive the
source constraints while retaining target geometry. Source-only frames are copied
unless `--no-copy-missing` is supplied. Invalid structures and duplicate frame
names cause a nonzero exit without replacing the target. Output is staged next
to the destination before replacement.
