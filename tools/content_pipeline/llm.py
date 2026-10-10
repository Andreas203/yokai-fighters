"""Model calls, recorded so a run can be replayed.

Live calls go through the Claude Code CLI in headless mode (`claude -p`) with
every tool switched off and an empty working directory, so the model sees only
the prompt this pipeline builds: no CLAUDE.md, no repo, no web. Whatever the
output knows about the game, it got from the retrieved vault chunks.

Every call is stored in the run folder (llm-calls.json) keyed by a hash of the
model and prompts. `--replay` reads that file instead of calling a model, which
makes a finished run reproducible on a machine with no login.
"""
from __future__ import annotations

import hashlib
import json
import shutil
import subprocess
import tempfile
from pathlib import Path


class LLM:
    def __init__(self, cache_path: Path, replay: bool = False):
        self.cache_path = cache_path
        self.replay = replay
        self.calls: dict[str, dict] = {}
        if cache_path.exists():
            self.calls = json.loads(cache_path.read_text(encoding="utf-8"))
        elif replay:
            raise SystemExit(f"--replay needs recorded calls at {cache_path}")
        self.live_calls = 0
        self.replayed_calls = 0
        self._workdir = tempfile.mkdtemp(prefix="yokai-pipeline-")

    def complete(self, role: str, model: str, system: str, user: str) -> str:
        key = hashlib.sha256(f"{model}\n{system}\n{user}".encode("utf-8")).hexdigest()[:16]
        if key in self.calls:
            self.replayed_calls += 1
            return self.calls[key]["response"]
        if self.replay:
            raise SystemExit(
                f"--replay: no recorded {role} call for this prompt (key {key}). "
                "The vault or the prompts changed since the run was recorded; run live instead."
            )
        response = self._call_cli(model, system, user)
        self.live_calls += 1
        self.calls[key] = {"role": role, "model": model, "system": system, "user": user, "response": response}
        self.cache_path.write_text(json.dumps(self.calls, indent=2, ensure_ascii=False), encoding="utf-8")
        return response

    def _call_cli(self, model: str, system: str, user: str) -> str:
        exe = shutil.which("claude")
        if not exe:
            raise SystemExit("The 'claude' CLI is not on PATH. Install Claude Code and log in, or use --replay.")
        cmd = [
            exe, "-p", "--model", model, "--output-format", "json",
            "--tools", "", "--strict-mcp-config", "--setting-sources", "",
            "--disable-slash-commands", "--no-session-persistence",
            "--system-prompt", system,
        ]
        last_error = ""
        for _ in range(2):
            proc = subprocess.run(
                cmd, input=user, capture_output=True, text=True, encoding="utf-8",
                cwd=self._workdir, timeout=300,
            )
            try:
                envelope = json.loads(proc.stdout)
                if not envelope.get("is_error") and envelope.get("result"):
                    return envelope["result"]
                last_error = str(envelope.get("result") or envelope)[:500]
            except json.JSONDecodeError:
                last_error = (proc.stderr or proc.stdout)[:500]
        raise SystemExit(f"Model call failed twice: {last_error}")


def parse_json(text: str) -> dict:
    """Read the JSON object out of a model reply, with or without a code fence."""
    start, end = text.find("{"), text.rfind("}")
    if start == -1 or end <= start:
        raise ValueError(f"No JSON object in model reply: {text[:200]!r}")
    return json.loads(text[start:end + 1])
