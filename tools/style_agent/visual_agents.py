"""The visual loop's agents: Astra makes the image, a vision Evaluator scores it, a Refiner rewrites the prompt.

    Generator: Astra (gpt-6-astra, via codex exec) renders the sabotaged brief, with no style guide
    Evaluator: a vision model sees the take, the accepted grove backdrop as REFERENCE, the style guide
               and the code's pixel measurements; replies SCORE: X/10 and REASON
    Refiner:   reads the guide, the prompt that made the take and the REASON; writes a new scene
               description, which the pipeline wraps in style.md's style and negative blocks
    Astra renders again ... until 10/10 with no flag, or the image budget runs out.

Every Astra take is kept under runs/<run>/images/ and indexed in images.json; every model call is
recorded in llm-calls.json. --replay rebuilds a run from both without calling anything.
"""
from __future__ import annotations

import hashlib
import json
import shutil
import subprocess
import tempfile
import time
from pathlib import Path

from image_tools import format_image_measurements, image_block, store_take
from llm import LLM
from visual_guide import NEGATIVE_BLOCK, RUBRIC, STYLE_BLOCK, render_guide

ROOT = Path(__file__).resolve().parent.parent.parent
# The accepted Kitsune stage, cropped to the box the grove spec keeps (Meshy's letterbox removed).
REFERENCE = ROOT / "game" / "assets" / "generated" / "stage" / "bamboo-grove" / "backdrop.png"
REFERENCE_CROP = (110, 75, 1266, 693)

GENERATOR_NOTE = "Stage backdrop for Yokai Fighters, a 2.5D fighting game for PC set among Japanese yokai."

EVALUATOR_SYSTEM = (
    "You are the visual style Evaluator for Yokai Fighters. You grade one generated stage backdrop (the TAKE) against "
    "the game's STYLE GUIDE and nothing else: ignore anything you believe about the game from elsewhere, and do not mark "
    "down matters of taste that no rule covers. The first image is the REFERENCE, an accepted stage; the second is the "
    "TAKE you grade. Reply strictly in the format you are given and nothing else."
)

REFINER_SYSTEM = (
    "You are the visual style Refiner for Yokai Fighters. You rewrite the prompt for an image generator so that its next "
    "take follows the game's STYLE GUIDE exactly. Take every fact about the stage from the STYLE GUIDE and the ASSET SLOT; "
    "do not invent new places or objects. Reply with the scene description only."
)

ASTRA_WRAPPER = (
    "Generate exactly one image with your image generation tool from the PROMPT below. Use the aspect ratio and "
    "orientation the PROMPT asks for; if it names none, use whatever suits it. Do not save, copy, create, edit or delete "
    "any file and do not run any command: the image tool's own output is collected. Reply with the word done.\n\n"
    "PROMPT\n{prompt}\n"
)
# Codex writes every generated image here; a sandboxed copy elsewhere can be refused, so takes are collected from it.
CODEX_IMAGES = Path.home() / ".codex" / "generated_images"


def describe_slot(slot: dict) -> str:
    return f"{slot['asset']}. {slot['subject']}"


def wrap_prompt(scene: str) -> str:
    """style.md convention: every asset prompt starts with the style block and ends with the negative block."""
    return f"{STYLE_BLOCK} {scene.strip()} {NEGATIVE_BLOCK}"


def evaluator_prompt(slot: dict, m: dict) -> str:
    return (
        f"STYLE GUIDE\n{render_guide()}\n"
        f"SCORING\n{RUBRIC}\n\n"
        f"ASSET SLOT\n{describe_slot(slot)}\n\n"
        f"MEASUREMENTS OF THE TAKE (by code; trust these over your own estimate)\n{format_image_measurements(m)}\n\n"
        "Review the TAKE (the second image). Grade it on a scale of 1-10 against the style guide's three constraint types: "
        "look, palette and lore, spec form. Reply strictly in this format and nothing else:\n"
        "SCORE: <X>/10\n"
        "REASON: <one line per violation: the rule ID in brackets, what in the image breaks it (where it is), and the "
        "points deducted, e.g. [L3] glossy photographic snow with film grain across the whole frame (-3). If nothing is "
        "broken, say why the take meets each of the three constraint types.>\n"
    )


def refiner_prompt(slot: dict, scene: str, score: int, reason: str, m: dict) -> str:
    return (
        f"STYLE GUIDE\n{render_guide()}\n"
        f"ASSET SLOT\n{describe_slot(slot)}\n\n"
        f"PROMPT THAT MADE THE TAKE (scored {score}/10)\n<<<\n{scene}\n>>>\n\n"
        f"MEASUREMENTS OF THE TAKE (by code)\n{format_image_measurements(m)}\n\n"
        f"EVALUATOR'S REASON\n{reason}\n\n"
        "Take the prompt and the Evaluator's REASON, and rewrite the prompt so that the next take scores a perfect 10/10 on "
        "the style guide. Fix every violation named in the REASON; keep what the asset is for (the ASSET SLOT). Write the "
        "scene description only: the pipeline adds the style block before it and the negative block after it, so do not "
        "repeat them. Say the aspect ratio, what fills the frame, what stays empty, and the palette colours by name and "
        "hex. Reply with the scene description only: no quotation marks, no notes.\n"
    )


class VisionLLM(LLM):
    """LLM with image input: `claude -p` reads a stream-json message holding the images, with every tool still off."""

    def complete_with_images(self, role: str, model: str, system: str, user: str, images: list[Path]) -> str:
        digest = hashlib.sha256()
        for p in images:
            digest.update(Path(p).read_bytes())
        key = hashlib.sha256(f"{model}\n{system}\n{user}\n{digest.hexdigest()}".encode("utf-8")).hexdigest()[:16]
        if key in self.calls:
            self.replayed_calls += 1
            return self.calls[key]["response"]
        if self.replay:
            raise SystemExit(f"--replay: no recorded {role} call for this prompt and image (key {key}); run live instead.")
        content = [image_block(p) for p in images] + [{"type": "text", "text": user}]
        message = {"type": "user", "message": {"role": "user", "content": content}}
        exe = shutil.which("claude")
        if not exe:
            raise SystemExit("The 'claude' CLI is not on PATH. Install Claude Code and log in, or use --replay.")
        cmd = [exe, "-p", "--model", model, "--input-format", "stream-json", "--output-format", "stream-json", "--verbose",
               "--tools", "", "--strict-mcp-config", "--setting-sources", "", "--disable-slash-commands",
               "--no-session-persistence", "--system-prompt", system]
        last_error = ""
        for _ in range(2):
            proc = subprocess.run(cmd, input=json.dumps(message) + "\n", capture_output=True, text=True, encoding="utf-8",
                                  cwd=self._workdir, timeout=self.timeout)
            for line in proc.stdout.splitlines():
                try:
                    event = json.loads(line)
                except json.JSONDecodeError:
                    continue
                if event.get("type") == "result":
                    if not event.get("is_error") and event.get("result"):
                        response = event["result"]
                        self.live_calls += 1
                        self.calls[key] = {"role": role, "model": model, "system": system, "user": user,
                                           "images": [str(Path(p).name) for p in images], "response": response}
                        self.cache_path.write_text(json.dumps(self.calls, indent=2, ensure_ascii=False), encoding="utf-8")
                        return response
                    last_error = str(event.get("result"))[:500]
            last_error = last_error or (proc.stderr or proc.stdout)[-500:]
        raise SystemExit(f"Vision call failed twice: {last_error}")


class Astra:
    """Image takes from gpt-6-astra through `codex exec`, each kept in the run folder, with a hard budget."""

    def __init__(self, run_dir: Path, replay: bool, budget: int, model: str = "gpt-6-astra", timeout: int = 900):
        self.images = run_dir / "images"
        self.index_path = run_dir / "images.json"
        self.index = json.loads(self.index_path.read_text(encoding="utf-8")) if self.index_path.exists() else {}
        self.replay, self.budget, self.model, self.timeout = replay, budget, model, timeout
        self.live = 0

    def render(self, prompt: str, name: str) -> Path:
        key = hashlib.sha256(f"{self.model}\n{prompt}".encode("utf-8")).hexdigest()[:16]
        if key in self.index:
            return self.images / self.index[key]["file"]
        if self.replay:
            raise SystemExit(f"--replay: no recorded Astra take for this prompt (key {key}); run live instead.")
        # The budget covers every take this run folder has ever made, so splitting a run across calls can't exceed it.
        if len(self.index) >= self.budget:
            raise BudgetSpent(f"Image budget of {self.budget} Astra takes is spent.")
        exe = shutil.which("codex")
        if not exe:
            raise SystemExit("The 'codex' CLI is not on PATH; Astra runs through codex exec.")
        work = Path(tempfile.mkdtemp(prefix="yokai-astra-"))
        started = time.time()
        cmd = [exe, "exec", "-m", self.model, "-s", "read-only", "-C", str(work), "--skip-git-repo-check",
               "--ephemeral", "-"]
        self.live += 1   # a call counts against the budget whether or not it returns an image
        proc = subprocess.run(cmd, input=ASTRA_WRAPPER.format(prompt=prompt), capture_output=True,
                              text=True, encoding="utf-8", timeout=self.timeout)
        made = new_codex_images(started)
        if not made:
            raise SystemExit(f"Astra returned no image (exit {proc.returncode}): {(proc.stderr or proc.stdout)[-800:]}")
        return self.adopt(prompt, made[-1], name, extra=len(made) - 1)

    def adopt(self, prompt: str, source: Path, name: str, extra: int = 0) -> Path:
        """File a generated image as the take for this prompt: kept in the run folder and indexed for --replay."""
        key = hashlib.sha256(f"{self.model}\n{prompt}".encode("utf-8")).hexdigest()[:16]
        kept = store_take(source, self.images / f"{name}.png")
        self.index[key] = {"file": kept.name, "model": self.model, "prompt": prompt, "source": source.name,
                           "extra_images_ignored": extra}
        self.index_path.write_text(json.dumps(self.index, indent=2, ensure_ascii=False), encoding="utf-8")
        return kept


class BudgetSpent(Exception):
    pass


def new_codex_images(since: float) -> list[Path]:
    """PNGs Codex generated since `since`, oldest first."""
    if not CODEX_IMAGES.exists():
        return []
    return sorted((p for p in CODEX_IMAGES.glob("*/*.png") if p.stat().st_mtime >= since - 1), key=lambda p: p.stat().st_mtime)


def reference_image(run_dir: Path) -> Path:
    """The grove reference, cropped to the spec's box and kept in the run folder so a replay sees the same bytes."""
    from PIL import Image
    path = run_dir / "images" / "reference-bamboo-grove.png"
    if not path.exists():
        path.parent.mkdir(parents=True, exist_ok=True)
        Image.open(REFERENCE).convert("RGB").crop(REFERENCE_CROP).save(path, "PNG", optimize=True)
    return path
