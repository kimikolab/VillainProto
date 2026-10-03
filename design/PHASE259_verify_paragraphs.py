# 第259期 §5-2: 旧 CLAUDE.md の全段落が、新 CLAUDE.md ＋ design/ の移動先のどれかに逐語で含まれるか。
# 段落 = 空行区切り。BOM・各行の末尾空白・CRLF は正規化する。0 件落ちが条件。
#   python verify_paragraphs.py <旧CLAUDE.md> <リポジトリのルート>
import io, os, sys
old_path, root = sys.argv[1], sys.argv[2]
def norm(t): return "\n".join(l.rstrip() for l in t.replace("\r\n", "\n").split("\n"))
def paragraphs(t):
    out, cur = [], []
    for l in norm(t).split("\n"):
        if l.strip() == "":
            if cur: out.append("\n".join(cur)); cur = []
        else: cur.append(l)
    if cur: out.append("\n".join(cur))
    return out
old = io.open(old_path, encoding="utf-8-sig").read()
targets = ["CLAUDE.md"] + [os.path.join("design", n) for n in
           ("DEMOAPP_HISTORY.md", "ENGINE_HOOKS.md", "RULES_INDEX.md", "HISTORY_PHASES.md", "COMMANDS.md", "PHASE_INDEX.md")]
hay = {t: norm(io.open(os.path.join(root, t), encoding="utf-8-sig").read()) for t in targets}
paras = paragraphs(old)
miss, where = [], {}
for p in paras:
    hit = [t for t, h in hay.items() if p in h]
    if not hit: miss.append(p)
    else: where[hit[0]] = where.get(hit[0], 0) + 1
print(f"旧 CLAUDE.md の段落 {len(paras)} 件 → 落ち {len(miss)} 件")
for t, n in where.items(): print(f"  {t}: {n}")
for p in miss[:20]: print("---- 落ち:\n" + p[:300])
sys.exit(1 if miss else 0)
