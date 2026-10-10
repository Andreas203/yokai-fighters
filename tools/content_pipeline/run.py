#!/usr/bin/env python3
"""Yokai Fighters content pipeline: drafts card text from the design vault.

    vault/*.md --chunk--> BM25 index --retrieve--> generator --draft--> critic --issues--> generator ... --> output

Usage (from anywhere):
    python tools/content_pipeline/run.py generate                 # write all ten pieces, planned retrieval (v2)
    python tools/content_pipeline/run.py generate --retrieval v1  # the first attempt: one query per piece
    python tools/content_pipeline/run.py generate --only modifier-card
    python tools/content_pipeline/run.py critic-test              # feed the critic deliberately broken drafts
    python tools/content_pipeline/run.py retrieve "Elder Kappa twist"
    python tools/content_pipeline/run.py generate --replay        # rebuild a recorded run without calling a model

Every run writes runs/<name>/trace.json and trace.md (query, retrieved chunks,
drafts, critic issues, final text) plus llm-calls.json for --replay.
Standard library only; live runs need the `claude` CLI, logged in.
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from content import CONTENT_TYPES, GENERATOR_SYSTEM, generation_prompt, revision_prompt  # noqa: E402
from critic import code_checks, critic_review  # noqa: E402
from llm import LLM, parse_json  # noqa: E402
from retrieval import VaultIndex, ability_names, load_vault  # noqa: E402

ROOT = HERE.parent.parent
VAULT = ROOT / "vault"
RUNS = HERE / "runs"
VALIDATOR = ROOT / "tools" / "validate_data.py"
FIXTURES = HERE / "seeded_faults.json"


def retrieve(index: VaultIndex, item: dict, mode: str) -> dict:
    """Return the queries run and the chunks they found, in the order the generator sees them."""
    queries, order, scores = [], [], {}

    def add(chunk, score):
        if chunk.id not in scores:
            order.append(chunk)
            scores[chunk.id] = score

    if mode == "v1":
        # First attempt: the request as a person would type it, over the whole vault.
        hits = index.search(item["request"], k=4)
        queries.append({"label": "request", "query": item["request"], "scope": "whole vault", "k": 4,
                        "hits": [{"id": c.id, "score": s} for c, s in hits]})
        for chunk, score in hits:
            add(chunk, score)
    else:
        # Planned retrieval: one narrow query per thing the piece needs, each scoped to the note that owns it.
        for label, query, scope, k in item["plan"]:
            hits = index.search(query, k=k, scope=f"{scope}#")
            queries.append({"label": label, "query": query, "scope": scope, "k": k,
                            "hits": [{"id": c.id, "score": s} for c, s in hits]})
            for chunk, score in hits:
                add(chunk, score)
        for chunk_id in item["pinned"]:
            add(index.get(chunk_id), "pinned")
    return {"mode": mode, "queries": queries, "chunks": order, "scores": scores}


def run_item(llm, index, move_names, type_id, item, args, seeded_draft=None) -> dict:
    ctype = CONTENT_TYPES[type_id]
    found = retrieve(index, item, args.retrieval)
    chunks = found["chunks"]
    # The critic always holds the canonical tone and rule sections, whatever the generator retrieved.
    evidence = list(chunks)
    for chunk_id in ctype["critic_pinned"] + item["critic_extra"]:
        if chunk_id not in {c.id for c in evidence}:
            evidence.append(index.get(chunk_id))

    def ask(role, prompt):
        try:
            return parse_json(llm.complete(role, args.gen_model, GENERATOR_SYSTEM, prompt))
        except ValueError:
            return {}

    draft = seeded_draft if seeded_draft is not None else ask("generator", generation_prompt(ctype, item, chunks))
    rounds = []
    for n in range(1, args.max_rounds + 1):
        code_issues = code_checks(type_id, draft, evidence, move_names)
        review = critic_review(llm, args.critic_model, ctype, item, draft, evidence)
        issues = code_issues + review["issues"]
        rounds.append({"round": n, "draft": draft, "code_issues": code_issues,
                       "critic_verdict": review["verdict"], "critic_issues": review["issues"]})
        print(f"    round {n}: {len(code_issues)} code issue(s), critic {review['verdict']} with {len(review['issues'])} issue(s)")
        if not issues or n == args.max_rounds:
            break
        # The rewrite sees the critic's evidence too, so it can fix what its own retrieval missed.
        draft = ask("reviser", revision_prompt(ctype, item, evidence, draft, issues))

    last = rounds[-1]
    passed = not last["code_issues"] and not last["critic_issues"]
    return {
        "id": item["id"], "type": type_id, "label": item["label"], "brief": item["brief"],
        "retrieval": {
            "mode": found["mode"], "queries": found["queries"],
            "chunks": [{"id": c.id, "score": found["scores"][c.id], "source": c.source, "text": c.text} for c in chunks],
        },
        "critic_evidence": [c.id for c in evidence],
        "seeded": seeded_draft is not None,
        "rounds": rounds,
        "status": "passed" if passed else "needs_human",
        "corrected": passed and len(rounds) > 1,
        "final": assemble(type_id, item, last["draft"], [c.id for c in chunks], args.run),
    }


def assemble(type_id: str, item: dict, draft: dict, chunk_ids: list[str], run: str) -> dict:
    """Wrap the model's text in the game's data shape. Ids, triggers and rule lists never come from the model."""
    note = f"Generated by tools/content_pipeline (run {run}) from vault chunks: " + "; ".join(chunk_ids)
    if type_id == "modifier-card":
        return {"modifier": item["modifier"], "source": item["source"], "status": "proposed",
                "card": {"plain": draft.get("plain", ""), "frames": draft.get("frames", "")},
                "rules": ["A2", "A8", "A10", "A12"], "notes": note}
    card = {"kind": "story_card", "id": item["id"], "status": "proposed", "trigger": item["trigger"]}
    if item.get("title"):
        card["title"] = item["title"]
    card["text"] = draft.get("text", "")
    if type_id == "wake-up-card":
        card.update({"placeholders": ["yokai"], "speaker": "narrator", "rules": ["S1", "S2", "S3", "S4", "M3"]})
    else:
        card.update({"speaker": "narrator", "rules": ["S1", "S2", "Y2", "Y3"]})
    card["notes"] = note
    return card


def write_outputs(results: list[dict], out_dir: Path) -> dict:
    """Write each final piece as JSON and run the repo's schema validator over the story cards."""
    story_files = []
    for r in results:
        path = out_dir / r["type"] / f"{r['id']}.json"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(r["final"], indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        if r["final"].get("kind") == "story_card":
            story_files.append(str(path))
    if not story_files or not VALIDATOR.exists():
        return {"ran": False, "reason": "no story cards or no validator"}
    proc = subprocess.run([sys.executable, str(VALIDATOR), *story_files], capture_output=True, text=True, encoding="utf-8")
    if proc.returncode == 2:
        return {"ran": False, "reason": proc.stderr.strip()}
    return {"ran": True, "ok": proc.returncode == 0, "files": len(story_files), "report": proc.stdout.strip()[-1500:]}


# ---------- trace rendering ----------

def _cell(text: str) -> str:
    return str(text).replace("|", "\\|").replace("\n", "<br>")


def _final_text(result: dict) -> str:
    final = result["final"]
    if "card" in final:
        return f"plain: {final['card']['plain']}\nframes: {final['card']['frames']}"
    return final["text"]


def _draft_text(draft: dict) -> str:
    if "plain" in draft or "frames" in draft:
        return f"plain: {draft.get('plain', '')}<br>frames: {draft.get('frames', '')}"
    return draft.get("text", "(no text)")


def render_trace(meta: dict, results: list[dict], validation: dict | None) -> str:
    lines = [f"# Trace: {meta['run']}", "",
             f"Command: `{meta['command']}`. Retrieval: {meta['retrieval']}. Generator: {meta['gen_model']}. Critic: {meta['critic_model']}.",
             f"Vault: {meta['chunks']} chunks from {meta['notes']} notes.", ""]
    lines += ["| Piece | Rounds | Issues caught | Result |", "|---|---|---|---|"]
    for r in results:
        caught = sum(len(x["code_issues"]) + len(x["critic_issues"]) for x in r["rounds"])
        result = "corrected, then passed" if r["corrected"] else ("passed first time" if r["status"] == "passed" else "needs a human")
        lines.append(f"| {r['label']} | {len(r['rounds'])} | {caught} | {result} |")
    if validation and validation.get("ran"):
        lines += ["", f"Schema check (`tools/validate_data.py`) on {validation['files']} story cards: "
                      f"{'all valid' if validation['ok'] else 'ERRORS'}.", "", "```", validation["report"], "```"]
    for r in results:
        lines += ["", f"## {r['label']}", "", f"Brief: {r['brief']}", ""]
        if r["seeded"]:
            lines += ["Round 1's draft is a seeded fault, written by hand to test the critic.", ""]
        lines += ["### Query, retrieved chunk, output", "", "| Query | Retrieved chunk (score) | Output |", "|---|---|---|"]
        output = _cell(_final_text(r))
        first = True
        for q in r["retrieval"]["queries"]:
            for hit in q["hits"]:
                chunk = next(c for c in r["retrieval"]["chunks"] if c["id"] == hit["id"])
                query = f"**{q['label']}** in {q['scope']}:<br>{q['query']}"
                lines.append(f"| {_cell(query)} | **{hit['id']}** ({hit['score']})<br>{_cell(chunk['text'])} | {output if first else ''} |")
                first = False
        pinned = [c for c in r["retrieval"]["chunks"] if c["score"] == "pinned"]
        for chunk in pinned:
            lines.append(f"| always included | **{chunk['id']}** (pinned)<br>{_cell(chunk['text'])} | |")
        lines += ["", "### Critic loop", ""]
        for x in r["rounds"]:
            lines += [f"**Round {x['round']} draft:** {_draft_text(x['draft'])}", ""]
            issues = x["code_issues"] + x["critic_issues"]
            if not issues:
                lines += ["Code checks: clean. Critic: pass.", ""]
                continue
            lines += ["| Caught by | Kind | Quote | Problem | Evidence | Fix |", "|---|---|---|---|---|---|"]
            for i in issues:
                lines.append(f"| {i['by']} | {i['kind']} | {_cell(i.get('quote', ''))} | {_cell(i['problem'])} | {_cell(i.get('evidence', ''))} | {_cell(i.get('fix', ''))} |")
            lines.append("")
        lines += [f"**Final ({r['status']}):** {_draft_text(r['rounds'][-1]['draft'])}"]
    return "\n".join(lines) + "\n"


# ---------- commands ----------

def select_items(only: list[str] | None):
    for type_id, ctype in CONTENT_TYPES.items():
        for item in ctype["items"]:
            if not only or type_id in only or item["id"] in only:
                yield type_id, item


def cmd_generate(args, index, chunks, move_names):
    run_dir = RUNS / args.run
    run_dir.mkdir(parents=True, exist_ok=True)
    llm = LLM(run_dir / "llm-calls.json", replay=args.replay)
    results = []
    if args.command == "critic-test":
        seeds = json.loads(FIXTURES.read_text(encoding="utf-8"))
        items = {item["id"]: (type_id, item) for type_id, item in select_items(None)}
        work = [(items[s["item"]][0], items[s["item"]][1], s["draft"]) for s in seeds]
    else:
        work = [(type_id, item, None) for type_id, item in select_items(args.only)]
    if not work:
        raise SystemExit("Nothing matched --only. Use a type id or a piece id from content.py.")
    for type_id, item, seeded in work:
        print(f"  {item['label']}")
        results.append(run_item(llm, index, move_names, type_id, item, args, seeded_draft=seeded))

    validation = write_outputs(results, run_dir / "output")
    meta = {"run": args.run, "command": args.command, "retrieval": args.retrieval, "gen_model": args.gen_model,
            "critic_model": args.critic_model, "chunks": len(chunks), "notes": len({c.note for c in chunks})}
    (run_dir / "trace.json").write_text(json.dumps({"meta": meta, "validation": validation, "results": results},
                                                   indent=2, ensure_ascii=False), encoding="utf-8")
    (run_dir / "trace.md").write_text(render_trace(meta, results, validation), encoding="utf-8")

    passed = sum(r["status"] == "passed" for r in results)
    corrected = sum(r["corrected"] for r in results)
    print(f"\n{passed}/{len(results)} passed ({corrected} after correction); "
          f"{llm.live_calls} live model call(s), {llm.replayed_calls} replayed.")
    if validation.get("ran"):
        print(f"Schema check on {validation['files']} story cards: {'all valid' if validation['ok'] else 'ERRORS'}")
    print(f"Trace: {run_dir / 'trace.md'}")
    return 0 if passed == len(results) and validation.get("ok", True) else 1


def cmd_retrieve(args, index):
    for chunk, score in index.search(args.query, k=args.k, scope=args.scope):
        print(f"{score:>7}  {chunk.id}")
        print("         " + chunk.text[:160].replace("\n", " ") + "...")
    return 0


def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    for name in ("generate", "critic-test"):
        p = sub.add_parser(name)
        p.add_argument("--retrieval", choices=["v1", "v2"], default="v2")
        p.add_argument("--run", help="run folder name under runs/ (default depends on the command)")
        p.add_argument("--only", nargs="*", help="content type ids or piece ids")
        p.add_argument("--gen-model", default="sonnet")
        p.add_argument("--critic-model", default="opus")
        p.add_argument("--max-rounds", type=int, default=3, help="drafts per piece, including the first")
        p.add_argument("--replay", action="store_true", help="use the run's recorded model calls; call nothing")
    p = sub.add_parser("retrieve")
    p.add_argument("query")
    p.add_argument("-k", type=int, default=4)
    p.add_argument("--scope", help="chunk id prefix, e.g. 01-vibe/Tone.md")
    args = parser.parse_args()

    chunks = load_vault(VAULT)
    index = VaultIndex(chunks)
    if args.command == "retrieve":
        return cmd_retrieve(args, index)
    if not args.run:
        args.run = "critic-seeded-faults" if args.command == "critic-test" else f"{args.retrieval}-" + (
            "single-query" if args.retrieval == "v1" else "planned-retrieval")
    print(f"Vault: {len(chunks)} chunks. Run: {args.run}")
    return cmd_generate(args, index, chunks, ability_names(VAULT))


if __name__ == "__main__":
    sys.exit(main())
