"""Copy authored frame constraints between SWASI exports, matching frames by name."""
import argparse
from copy import deepcopy
import json
from pathlib import Path
import os
import tempfile


def frames(document):
    try:
        result = document["mountingDescription"]["mountingReferences"]["ref_frames"]
    except (KeyError, TypeError) as exc:
        raise ValueError("Expected mountingDescription.mountingReferences.ref_frames") from exc
    if not isinstance(result, list) or any(
        not isinstance(frame, dict) or not isinstance(frame.get("name"), str)
        for frame in result
    ):
        raise ValueError("ref_frames must contain objects with string names")
    names = [frame["name"] for frame in result]
    if len(set(names)) != len(names):
        raise ValueError("Reference frame names must be unique")
    return result


def transfer_constraints(target, source, copy_missing=True):
    """Return a new document; geometry of matching target frames is unchanged."""
    result = deepcopy(target)
    target_frames = frames(result)
    by_name = {frame["name"]: frame for frame in target_frames}
    for frame in frames(source):
        if frame["name"] in by_name:
            if "constraints" in frame:
                by_name[frame["name"]]["constraints"] = deepcopy(frame["constraints"])
        elif copy_missing:
            target_frames.append(deepcopy(frame))
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="Export containing authored constraints")
    parser.add_argument("target", type=Path, help="Export whose geometry should be retained")
    parser.add_argument("--output", type=Path, help="Write a separate output file (default: replace target)")
    parser.add_argument("--no-copy-missing", action="store_true", help="Do not append source-only frames")
    args = parser.parse_args(argv)
    output = args.output or args.target
    temporary = None
    try:
        source = json.loads(args.source.read_text(encoding="utf-8-sig"))
        target = json.loads(args.target.read_text(encoding="utf-8-sig"))
        result = transfer_constraints(target, source, not args.no_copy_missing)
        with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=output.parent,
                                         prefix=output.name + ".", suffix=".tmp", delete=False) as file:
            temporary = Path(file.name)
            json.dump(result, file, indent=4, ensure_ascii=False)
            file.write("\n")
        os.replace(temporary, output)
    except (OSError, ValueError) as exc:
        parser.exit(1, f"Error: {exc}\n")
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)
    print(f"Wrote {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
