"""Builds the game and runs its own test runner. The agent never trusts code it has not run."""
from __future__ import annotations

import os
import re
import shutil
import subprocess
from dataclasses import dataclass, field
from pathlib import Path

GAME = "game"
TEST_SCENE = "res://tests/test_runner.tscn"


@dataclass
class TestRun:
    passed: set[str] = field(default_factory=set)
    failed: dict[str, str] = field(default_factory=dict)   # test -> message
    summary: str = ""

    @property
    def ran(self) -> bool:
        return bool(self.summary)


def _tool(name: str, env: str) -> str:
    exe = os.environ.get(env) or shutil.which(name)
    if not exe:
        raise SystemExit(f"'{name}' is not on PATH (or set {env}). The goal agent has to build and run the game.")
    return exe


def build(root: Path) -> list[str]:
    """`dotnet build` in game/. Returns the compiler errors, empty when it built."""
    proc = subprocess.run([_tool("dotnet", "DOTNET"), "build", "--nologo", "-v", "q"],
                          cwd=root / GAME, capture_output=True, text=True, timeout=600)
    errors = []
    for line in (proc.stdout + proc.stderr).split("\n"):
        m = re.search(r"([^\s(]+\.cs)\((\d+),\d+\): error (CS\d+): (.*?)(?: \[|$)", line)
        if m:
            path = m.group(1)
            rel = os.path.relpath(path, root) if os.path.isabs(path) else f"{GAME}/{path}"
            msg = f"{rel}:{m.group(2)}: {m.group(3)}: {m.group(4)}"
            if msg not in errors:
                errors.append(msg)
    if proc.returncode != 0 and not errors:
        errors.append("dotnet build failed: " + (proc.stdout + proc.stderr).strip()[-600:])
    return errors


def run_tests(root: Path) -> TestRun:
    """The game's headless test runner: every [Test] in the assembly, inside Godot."""
    proc = subprocess.run([_tool("godot", "GODOT"), "--headless", "--path", GAME, TEST_SCENE],
                          cwd=root, capture_output=True, text=True, timeout=900)
    run = TestRun()
    for line in (proc.stdout + "\n" + proc.stderr).split("\n"):
        line = line.strip()
        if m := re.match(r"PASS (\S+)$", line):
            run.passed.add(m.group(1))
        elif m := re.match(r"(?:ERROR: )?FAIL (\S+?): (.*)$", line):
            run.failed[m.group(1)] = m.group(2)
        elif re.match(r"TESTS \d+ passed, \d+ failed", line):
            run.summary = line
    return run
