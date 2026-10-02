import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import unittest
import uuid
from contextlib import contextmanager

TOOL = Path(__file__).resolve().parents[1] / "tools" / "constraints_transfer.py"
SPEC = importlib.util.spec_from_file_location("constraints_transfer", TOOL)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def document(*frames):
    return {"mountingDescription": {"mountingReferences": {"ref_frames": list(frames)}}}


@contextmanager
def temporary_directory():
    # Keep test data inside the checkout, also on machines with restricted TEMP access.
    directory = Path(__file__).resolve().parent / "bin" / ("transfer-" + uuid.uuid4().hex)
    directory.mkdir(parents=True)
    try:
        yield directory
    finally:
        for file in directory.iterdir():
            file.unlink()
        directory.rmdir()


class TransferTests(unittest.TestCase):
    def test_preserves_geometry_and_inputs(self):
        target = document({"name": "a", "transformation": {"x": 2}, "constraints": {}})
        source = document({"name": "a", "transformation": {"x": 1}, "constraints": {"manual": True}})
        result = MODULE.transfer_constraints(target, source)
        frame = MODULE.frames(result)[0]
        self.assertEqual(frame["transformation"], {"x": 2})
        self.assertEqual(frame["constraints"], {"manual": True})
        self.assertEqual(MODULE.frames(target)[0]["constraints"], {})

    def test_copy_missing_is_optional(self):
        source = document({"name": "manual"})
        self.assertEqual(len(MODULE.frames(MODULE.transfer_constraints(document(), source))), 1)
        self.assertEqual(MODULE.frames(MODULE.transfer_constraints(document(), source, False)), [])

    def test_missing_constraints_do_not_erase_target(self):
        result = MODULE.transfer_constraints(document({"name": "a", "constraints": {"keep": True}}), document({"name": "a"}))
        self.assertEqual(MODULE.frames(result)[0]["constraints"], {"keep": True})

    def test_rejects_invalid_and_duplicate_frames(self):
        for value in ({}, document({}), document({"name": "a"}, {"name": "a"})):
            with self.assertRaises(ValueError):
                MODULE.transfer_constraints(document(), value)

    def test_cli_separate_output_and_malformed_input(self):
        with temporary_directory() as directory:
            root = Path(directory)
            source, target, output = (root / name for name in ("source.json", "target.json", "output.json"))
            source.write_text(json.dumps(document({"name": "manual"})), encoding="utf-8")
            target.write_text(json.dumps(document()), encoding="utf-8")
            run = subprocess.run([sys.executable, str(TOOL), str(source), str(target), "--output", str(output)], capture_output=True, text=True)
            self.assertEqual(run.returncode, 0, run.stderr)
            self.assertEqual(MODULE.frames(json.loads(target.read_text())), [])
            self.assertEqual(len(MODULE.frames(json.loads(output.read_text()))), 1)
            source.write_text("broken", encoding="utf-8")
            original = target.read_bytes()
            run = subprocess.run([sys.executable, str(TOOL), str(source), str(target)], capture_output=True)
            self.assertNotEqual(run.returncode, 0)
            self.assertEqual(target.read_bytes(), original)
            self.assertEqual(list(root.glob("*.tmp")), [])


if __name__ == "__main__":
    unittest.main()
