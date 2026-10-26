"""Claude Code から Codex CLI へ、UTF-8 の依頼ファイルを渡す。"""

import argparse
from datetime import datetime
import os
from pathlib import Path
import shutil
import subprocess
import sys
from uuid import uuid4


def find_codex() -> str | None:
    executable = shutil.which("codex")
    if executable:
        return executable
    # デスクトップが追加する PATH は、別に起動した Claude Code には無いことがある。
    local_app_data = os.environ.get("LOCALAPPDATA")
    if os.name == "nt":
        # サンドボックスで LOCALAPPDATA が仮想化されている場合も元の配置を探す。
        roots = ([Path(local_app_data)] if local_app_data else [])
        roots.append(Path.home() / "AppData" / "Local")
        for install_root in roots:
            bin_dir = install_root / "OpenAI" / "Codex" / "bin"
            candidates = [p for p in bin_dir.glob("*/codex.exe") if p.is_file()]
            if candidates:
                return str(max(candidates, key=lambda p: p.stat().st_mtime))
    return None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mode", choices=("review", "work"), default="review")
    parser.add_argument("--prompt-file", required=True, type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    prompt = args.prompt_file.read_text(encoding="utf-8-sig").strip()
    if not prompt:
        parser.error("依頼ファイルが空です。")
    codex = find_codex()
    if not codex:
        parser.error("Codex CLI が見つかりません。Codex を導入し、codex login を実行してください。")

    run_id = datetime.now().strftime("%Y%m%d-%H%M%S") + "-" + uuid4().hex[:8]
    output_dir = root / ".tmp" / "codex-bridge" / run_id
    output_dir.mkdir(parents=True)
    result_file = output_dir / "result.txt"
    events_file = output_dir / "events.jsonl"
    sandbox = "read-only" if args.mode == "review" else "workspace-write"
    instructions = (
        "You are handling a bounded task delegated from Claude Code. "
        "Before doing any work, read AGENTS.md, CLAUDE.md, and CONTRIBUTING.md "
        "in the working root. Follow CONTRIBUTING.md's section "
        "'Claude Code から Codex に依頼する' for delegation rules. "
        "Report in Japanese, with changes or findings, validation, and any blockers. "
    )
    if args.mode == "review":
        instructions += "This is a read-only investigation/review; do not edit files. "
    full_prompt = instructions + "\n\nTask from Claude Code:\n" + prompt + "\n"
    (output_dir / "prompt.txt").write_text(full_prompt, encoding="utf-8")
    command = [
        codex, "exec", "--cd", str(root), "--sandbox", sandbox,
        "-c", 'approval_policy="never"', "--ephemeral", "--color", "never",
        "--json", "--output-last-message", str(result_file), "-",
    ]
    # 日本語や引用符をシェルの引数に埋めず、UTF-8 の標準入力で渡す。
    # 親のチャット ID を引き継がず、この呼び出しを独立したセッションにする。
    child_env = os.environ.copy()
    child_env.pop("CODEX_THREAD_ID", None)
    print(f"Codex: {codex}\nMode: {args.mode}\nOutput: {output_dir}", flush=True)
    with events_file.open("wb") as events:
        completed = subprocess.run(
            command, input=full_prompt.encode("utf-8"), stdout=events,
            cwd=root, env=child_env, check=False,
        )
    print(f"Codex exit code: {completed.returncode}", flush=True)
    if result_file.is_file() and result_file.stat().st_size:
        print(result_file.read_text(encoding="utf-8"), flush=True)
    elif completed.returncode == 0:
        print(f"最終回答がありません。イベントを確認してください: {events_file}", file=sys.stderr)
        return 1
    return completed.returncode


if __name__ == "__main__":
    # Windows の親シェルのコードページに依存せず、日本語を返す。
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    try:
        sys.exit(main())
    except (OSError, UnicodeError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
