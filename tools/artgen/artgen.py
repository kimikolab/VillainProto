#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""素材の道具（第482期・指示書 design/PHASE482_ART_TOOL_SPEC.md）。OpenAI の画像 API で候補の絵を作る。

口:
  gen    文から作る            python tools/artgen/artgen.py gen   --job foe_face/levy --n 3 --size 1024x1024
  edit   参照の絵を渡して直す  python tools/artgen/artgen.py edit  --job gan_quiet --ref DemoApp/assets/portraits/battle/gan_idle_right.png --n 3 --transparent
  sheet  見比べの絵を作る      python tools/artgen/artgen.py sheet --job foe_face
  reject 候補から外す          python tools/artgen/artgen.py reject --job saint --file 20261119-120000_2.png --why "手が崩れている"
  status この期の枚数と料金    python tools/artgen/artgen.py status
  init   候補の置き場を作る    python tools/artgen/artgen.py init
  check  API を呼ばない検査    python tools/artgen/artgen.py check

決まり（指示書 §1）:
  1. キーは環境変数 OPENAI_API_KEY から読むだけ。書かない・出さない。無ければ何も作らずに止まる。
  2. 1 回 4 枚まで・この期の合計 40 枚まで（config.json）。超えるなら API を呼ばずに止まる。
  3. 出力は design/art/_candidates/<job>/（git に入れない）。ゲームが読む場所へは置かない。
  4. 絵 1 枚ごとに、文・参照の絵・モデル・設定・日付を同じ名前の .json に残す。
  5. 参照に使えるのは DemoApp/assets/portraits と design/art の下の自作の絵だけ（購入素材は送らない）。
  7. API が断った依頼は、文を直して 1 回だけ試せる（同じ依頼で 2 回断られたら、もう呼ばない）。
文は design/art/prompts/_style.md（絵柄・どの依頼にも頭に付ける）＋ design/art/prompts/<job>.md。
どちらも、最初の ```text の囲みの中だけを送る（囲みが無ければ全文）。
"""
from __future__ import annotations

import argparse
import base64
import datetime as dt
import io
import json
import os
import re
import sys
import tempfile
import urllib.error
import urllib.request
import uuid
from pathlib import Path

KEY_ENV = "OPENAI_API_KEY"
STOP = 2      # 決まりで止めた（API は呼んでいない）
BLOCKED = 3   # API が断った
FAILED = 4    # API の失敗


class Stop(Exception):
    """決まりで止める（API は呼ばない）。"""


class Ctx:
    """置き場の根（検査は仮の根を渡す）。"""

    def __init__(self, root: Path | None = None):
        self.root = (root or Path(__file__).resolve().parents[2]).resolve()
        self.config = json.loads((Path(__file__).resolve().parent / "config.json").read_text(encoding="utf-8"))
        self.prompts = self.root / "design" / "art" / "prompts"
        self.out = self.root / "design" / "art" / "_candidates"
        self.ledger_path = self.out / "_ledger.json"

    # ---- 帳簿（この期の枚数・料金・断られた回数） ----
    def ledger(self) -> dict:
        if self.ledger_path.exists():
            return json.loads(self.ledger_path.read_text(encoding="utf-8"))
        return {"phases": {}}

    def phase(self, ledger: dict) -> dict:
        return ledger["phases"].setdefault(self.config["phase"], {"images": 0, "usd": 0.0, "runs": 0, "blocked": {}})

    def save_ledger(self, ledger: dict) -> None:
        self.ledger_path.write_text(json.dumps(ledger, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


def say(text: str) -> None:
    sys.stdout.write(text + "\n")
    sys.stdout.flush()


def fenced(text: str) -> str:
    """最初の ```text（または ```）の囲みの中。無ければ全文。"""
    m = re.search(r"```(?:text)?[ \t]*\r?\n(.*?)\r?\n```", text, re.S)
    return (m.group(1) if m else text).strip()


def job_ok(job: str) -> bool:
    return bool(re.fullmatch(r"[a-z0-9_]+(/[a-z0-9_]+)?", job))


def build_prompt(ctx: Ctx, job: str) -> tuple[str, list[str]]:
    if not job_ok(job):
        raise Stop(f"依頼の名前が読めない: {job}（英小文字・数字・_ と、1 つまでの /）")
    style, body = ctx.prompts / "_style.md", ctx.prompts / (job + ".md")
    for p in (style, body):
        if not p.exists():
            raise Stop(f"文が無い: {p.relative_to(ctx.root).as_posix()}")
    return fenced(style.read_text(encoding="utf-8")) + "\n\n" + fenced(body.read_text(encoding="utf-8")), \
        [style.relative_to(ctx.root).as_posix(), body.relative_to(ctx.root).as_posix()]


def guard(ctx: Ctx, job: str, n: int) -> tuple[dict, dict, Path]:
    """API を呼ぶ前の門: キー・置き場・枚数・断られた回数。通ったら（帳簿・この期の欄・出力の置き場）。"""
    if not os.environ.get(KEY_ENV, "").strip():
        raise Stop(f"環境変数 {KEY_ENV} が無い——何も作らない（キーは環境変数にだけ置く・ファイルに書かない）")
    if not ctx.out.is_dir():
        raise Stop(f"候補の置き場が無い: {ctx.out.relative_to(ctx.root).as_posix()}（`artgen.py init` で作る）")
    cap_run, cap_phase = ctx.config["per_run_cap"], ctx.config["phase_cap"]
    if n < 1 or n > cap_run:
        raise Stop(f"1 回に作れるのは 1〜{cap_run} 枚（頼んだのは {n} 枚）")
    ledger = ctx.ledger()
    ph = ctx.phase(ledger)
    if ph["images"] + n > cap_phase:
        raise Stop(f"この期の上限 {cap_phase} 枚を超える（今 {ph['images']} 枚 ＋ {n} 枚）——作らない")
    tries = ctx.config["blocked_tries_per_job"]
    if ph["blocked"].get(job, 0) >= tries:
        raise Stop(f"依頼 {job} は API に {ph['blocked'][job]} 回断られた——もう試さない（報告する）")
    out = ctx.out / job
    return ledger, ph, out


def check_ref(ctx: Ctx, ref: str) -> Path:
    p = Path(ref)
    p = (p if p.is_absolute() else ctx.root / p).resolve()
    if not p.is_file():
        raise Stop(f"参照の絵が無い: {ref}")
    rel = p.relative_to(ctx.root).as_posix() if ctx.root in p.parents else None
    if rel is None or not any(rel.startswith(r.rstrip("/") + "/") for r in ctx.config["ref_roots"]):
        raise Stop(f"参照に使えるのは {' ／ '.join(ctx.config['ref_roots'])} の下の自作の絵だけ: {ref}")
    if any(w in rel.lower() for w in ctx.config["ref_banned_words"]):
        raise Stop(f"この絵は参照に使えない（購入素材・候補の置き場）: {rel}")
    if p.suffix.lower() not in (".png", ".webp", ".jpg", ".jpeg"):
        raise Stop(f"参照は png ／ webp ／ jpg: {rel}")
    return p


# ---- 通信（検査はここを差し替える） ----
def http_post(url: str, headers: dict, body: bytes) -> tuple[int, bytes]:
    req = urllib.request.Request(url, data=body, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=600) as res:
            return res.status, res.read()
    except urllib.error.HTTPError as e:
        return e.code, e.read()


def multipart(fields: dict, files: list[tuple[str, Path]]) -> tuple[bytes, str]:
    boundary = "----artgen" + uuid.uuid4().hex
    buf = io.BytesIO()
    for k, v in fields.items():
        buf.write(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{k}\"\r\n\r\n{v}\r\n".encode("utf-8"))
    for name, path in files:
        mime = {".png": "image/png", ".webp": "image/webp"}.get(path.suffix.lower(), "image/jpeg")
        # 送るファイル名は中身と関係のない名前にする（置き場の道筋を外へ出さない）。
        buf.write(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"; filename=\"ref{path.suffix.lower()}\"\r\nContent-Type: {mime}\r\n\r\n".encode("utf-8"))
        buf.write(path.read_bytes())
        buf.write(b"\r\n")
    buf.write(f"--{boundary}--\r\n".encode("utf-8"))
    return buf.getvalue(), f"multipart/form-data; boundary={boundary}"


def cost_of(ctx: Ctx, usage: dict | None, n: int, size: str, quality: str) -> tuple[float, str]:
    """料金（USD）。返りの usage があればそこから数え、無ければ資料の 1 枚の目安。"""
    rate = ctx.config["usd_per_million_tokens"]
    if usage and "output_tokens" in usage:
        det = usage.get("input_tokens_details") or {}
        img_in, txt_in = det.get("image_tokens", 0), det.get("text_tokens", usage.get("input_tokens", 0) - det.get("image_tokens", 0))
        usd = (txt_in * rate["text_in"] + img_in * rate["image_in"] + usage["output_tokens"] * rate["image_out"]) / 1_000_000
        return usd, "usage"
    per = ctx.config["usd_per_image_estimate"].get(quality, {}).get(size)
    return (per * n, "目安") if per is not None else (0.0, "不明")


def has_alpha(png: bytes) -> bool:
    from PIL import Image
    im = Image.open(io.BytesIO(png))
    if im.mode != "RGBA":
        return False
    w, h = im.size
    a = im.getchannel("A")
    return any(a.getpixel(p) < 8 for p in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1), (w // 2, 0)))


def key_out(png: bytes, tol: int = 46) -> tuple[bytes, dict]:
    """無地の背景を抜く: 縁からつながっている、隅の色に近い画素を透明にする（中の同じ色は残す）。縁は 1px なじませる。"""
    from PIL import Image, ImageChops, ImageDraw, ImageFilter
    im = Image.open(io.BytesIO(png)).convert("RGB")
    w, h = im.size
    corners = [im.getpixel(p) for p in ((2, 2), (w - 3, 2), (2, h - 3), (w - 3, h - 3))]
    bg = tuple(sorted(c[i] for c in corners)[1] for i in range(3))
    spread = max(max(abs(c[i] - bg[i]) for i in range(3)) for c in corners)
    diff = ImageChops.difference(im, Image.new("RGB", im.size, bg)).convert("L").point(lambda v: 255 if v * 3 > tol else 0)
    # diff: 255 ＝ 絵・0 ＝ 背景の色。縁の背景から塗りつぶして、つながった背景だけを 128 にする。
    for seed in [(x, y) for x in range(0, w, 8) for y in (0, h - 1)] + [(x, y) for y in range(0, h, 8) for x in (0, w - 1)]:
        if diff.getpixel(seed) == 0:
            ImageDraw.floodfill(diff, seed, 128)
    alpha = diff.point(lambda v: 0 if v == 128 else 255).filter(ImageFilter.GaussianBlur(0.8))
    out = im.convert("RGBA")
    out.putalpha(alpha)
    buf = io.BytesIO()
    out.save(buf, "PNG")
    cleared = sum(alpha.resize((64, 64)).histogram()[:8]) / 4096
    return buf.getvalue(), {"bg": list(bg), "corner_spread": spread, "cleared": round(cleared, 3), "flat": spread <= 24}


FLAT_BG = "\n\nBackground: one perfectly flat, uniform solid magenta (#FF00FF) fill behind the figure, no gradient, no shadow on the background, and no magenta anywhere on the figure."


def run_api(ctx: Ctx, kind: str, job: str, n: int, size: str, quality: str | None, model: str | None, transparent: bool, refs: list[str]) -> int:
    model, quality = model or ctx.config["model"], quality or ctx.config["quality"]
    prompt, sources = build_prompt(ctx, job)
    ref_paths = [check_ref(ctx, r) for r in refs]
    if kind == "edit" and not ref_paths:
        raise Stop("edit には --ref（参照の絵）が要る")
    ledger, ph, out = guard(ctx, job, n)
    key = os.environ[KEY_ENV].strip()

    def call(want_alpha: bool, text: str) -> tuple[int, dict]:
        fields = {"model": model, "prompt": text, "n": n, "size": size, "quality": quality, "output_format": "png"}
        if want_alpha:
            fields["background"] = "transparent"
        if kind == "gen":
            body, ctype = json.dumps(fields).encode("utf-8"), "application/json"
        else:
            name = "image" if len(ref_paths) == 1 else "image[]"
            body, ctype = multipart(fields, [(name, p) for p in ref_paths])
        status, raw = http_post(ctx.config["endpoint"] + ("/generations" if kind == "gen" else "/edits"),
                                {"Authorization": "Bearer " + key, "Content-Type": ctype}, body)
        try:
            return status, json.loads(raw.decode("utf-8"))
        except ValueError:
            return status, {"error": {"message": raw[:300].decode("utf-8", "replace")}}

    mode = "api" if transparent else "opaque"
    status, data = call(transparent, prompt)
    err = data.get("error") or {}
    if transparent and status == 400 and err.get("code") != "moderation_blocked" and "background" in json.dumps(err).lower():
        # このモデルは透過を受けない——無地の背景で作って、道具の側で抜く（絵は作られていないので枚数は増えない）。
        say(f"ARTGEN 透過を API が受けなかった——無地の背景で作って道具で抜く（{err.get('message', '')[:120]}）")
        mode = "keyed"
        status, data = call(False, prompt + FLAT_BG)
        err = data.get("error") or {}
    if status != 200 or "data" not in data:
        blocked = err.get("code") == "moderation_blocked"
        if blocked:
            ph["blocked"][job] = ph["blocked"].get(job, 0) + 1
            ctx.save_ledger(ledger)
        say(f"ARTGEN_{'BLOCKED' if blocked else 'FAILED'} job={job} http={status} code={err.get('code')} type={err.get('type')} "
            f"message={str(err.get('message', ''))[:300]}" + (f" 断られた回数 {ph['blocked'][job]}／{ctx.config['blocked_tries_per_job']}（文を直して 1 回だけ試せる）" if blocked else ""))
        return BLOCKED if blocked else FAILED

    out.mkdir(parents=True, exist_ok=True)
    stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    usd, basis = cost_of(ctx, data.get("usage"), len(data["data"]), size, quality)
    made, hows = [], []
    for i, item in enumerate(data["data"], 1):
        png = base64.b64decode(item["b64_json"])
        how, keyed = mode, None
        if transparent and not has_alpha(png):
            # 透過を頼んだのに α が無い（または無地で作った）——道具で抜く。地が無地でなければ抜かずに出す。
            cut, keyed = key_out(png)
            if keyed["flat"] and keyed["cleared"] > 0.05:
                png, how = cut, "keyed"
            else:
                how = "opaque（地が無地でないので抜かない）"
        elif transparent:
            how = "api"
        path, bump = out / f"{stamp}_{i}.png", 0
        while path.exists():   # 同じ秒に 2 回走っても上書きしない
            bump += 1
            path = out / f"{stamp}{chr(96 + bump)}_{i}.png"
        hows.append(how)
        path.write_bytes(png)
        record = {
            "file": path.name, "job": job, "kind": kind, "date": dt.datetime.now().isoformat(timespec="seconds"),
            "model": model, "size": size, "quality": quality, "n": n, "transparent": transparent, "background": how,
            "prompt": prompt + (FLAT_BG if mode == "keyed" else ""), "prompt_sources": sources,
            "refs": [p.relative_to(ctx.root).as_posix() for p in ref_paths],
            "usage": data.get("usage"), "usd_run": round(usd, 4), "usd_basis": basis, "keying": keyed,
        }
        path.with_suffix(".json").write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
        made.append(path.name)
    ph["images"] += len(made)
    ph["usd"] = round(ph["usd"] + usd, 4)
    ph["runs"] += 1
    ctx.save_ledger(ledger)
    say(f"ARTGEN_DONE job={job} 作った {len(made)} 枚（{' '.join(made)}）・この期の合計 {ph['images']}／{ctx.config['phase_cap']} 枚・"
        f"今回 約 ${usd:.3f}（{basis}）・この期 約 ${ph['usd']:.2f}・背景 {' ／ '.join(sorted(set(hows)))}")
    return 0


# ---- 選り分け ----
def rejected_of(folder: Path) -> dict:
    p = folder / "_rejected.json"
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else {}


def cmd_reject(ctx: Ctx, job: str, file: str, why: str) -> int:
    folder = ctx.out / job
    if not (folder / file).is_file():
        raise Stop(f"その絵が無い: {job}/{file}")
    rej = rejected_of(folder)
    rej[file] = why
    (folder / "_rejected.json").write_text(json.dumps(rej, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    say(f"ARTGEN_REJECT {job}/{file}: {why}（外した {len(rej)} 枚）")
    return 0


# ---- 見比べの絵 ----
def framing(ctx: Ctx) -> dict:
    """味方の顔の位置（`DemoApp/UnitFaceIcon.cs` の表をそのまま読む・写しを持たない）。"""
    src = ctx.root / "DemoApp" / "UnitFaceIcon.cs"
    text = src.read_text(encoding="utf-8-sig") if src.exists() else ""
    return {m.group(1): tuple(float(m.group(i)) for i in (2, 3, 4))
            for m in re.finditer(r'\["(\w+)"\]\s*=\s*new\(([\d.]+)f,\s*([\d.]+)f,\s*([\d.]+)f\)', text)}


def face_of(im, frame):
    """立ち絵から顔の四角を切る（ゲームの `UnitFaceIcon.TextureFor` と同じ切り方）。"""
    w, h = im.size
    side = max(1, min(int(w * frame[2]), w, h))
    x = min(max(int(w * frame[0]) - side // 2, 0), w - side)
    y = min(max(int(h * frame[1]) - side // 2, 0), h - side)
    return im.crop((x, y, x + side, y + side))


def round_face(face, px):
    from PIL import Image, ImageDraw
    face = face.convert("RGBA").resize((px * 4, px * 4), Image.LANCZOS)
    mask = Image.new("L", face.size, 0)
    ImageDraw.Draw(mask).ellipse((0, 0, face.size[0] - 1, face.size[1] - 1), fill=255)
    disc = Image.new("RGBA", face.size, (38, 30, 30, 255))
    disc.alpha_composite(face)
    disc.putalpha(mask)
    return disc.resize((px, px), Image.LANCZOS)


def cmd_sheet(ctx: Ctx, job: str, allies: list[str], face: tuple[float, float, float] | None, cell_h: int, current: bool) -> int:
    from PIL import Image, ImageDraw, ImageFont
    top = ctx.out / job
    if not top.is_dir():
        raise Stop(f"候補が無い: {top.relative_to(ctx.root).as_posix()}")
    groups = [(d.name, d) for d in sorted(top.iterdir()) if d.is_dir()] or [("", top)]
    frames = framing(ctx)
    font_path = next((p for p in (r"C:\Windows\Fonts\meiryo.ttc", r"C:\Windows\Fonts\YuGothM.ttc", r"C:\Windows\Fonts\msgothic.ttc") if os.path.exists(p)), None)
    font = ImageFont.truetype(font_path, 20) if font_path else ImageFont.load_default()
    small = ImageFont.truetype(font_path, 14) if font_path else font
    battle = ctx.root / "DemoApp" / "assets" / "portraits" / "battle"

    def ally(aid: str):
        p = next((q for q in (battle / f"{aid}_idle_right.png", ctx.root / "DemoApp" / "assets" / "portraits" / f"{aid}.png") if q.exists()), None)
        return (Image.open(p).convert("RGBA"), frames.get(aid, (.5, .14, .36)), f"味方 {aid}") if p else None

    left, right = (ally(allies[0]) if allies else None), (ally(allies[-1]) if len(allies) > 1 else None)
    pad, face_px, label_h = 18, 40, 30
    rows, kept_total, rej_total = [], 0, 0
    for name, folder in groups:
        rej = rejected_of(folder)
        files = [p for p in sorted(folder.glob("*.png")) if p.name != "sheet.png" and p.name not in rej]
        rej_total += len(rej)
        kept_total += len(files)
        cells = []
        if left:
            cells.append(left)
        if current and name:
            cur = ctx.root / "DemoApp" / "assets" / "map" / "enemies" / f"{name}.png"
            if cur.exists():
                cells.append((Image.open(cur).convert("RGBA"), (.5, .5, 1.0), "今の顔"))
        for i, p in enumerate(files, 1):
            im = Image.open(p).convert("RGBA")
            square = abs(im.size[0] - im.size[1]) < 8
            cells.append((im, face or ((.5, .5, 1.0) if square else (.5, .14, .36)), f"{i}  {p.stem}"))
        if right:
            cells.append(right)
        rows.append((name, cells, len(rej)))
    if kept_total == 0:
        raise Stop(f"並べる候補が無い（{job}・外した {rej_total} 枚）")

    def scaled(im):
        return im.resize((max(1, round(im.size[0] * cell_h / im.size[1])), cell_h), Image.LANCZOS)

    row_h = label_h + cell_h + 8 + face_px * 2 + 8 + 22 + pad
    width = max(sum(scaled(c[0]).size[0] + pad for c in cells) for _, cells, _ in rows) + pad
    sheet = Image.new("RGBA", (max(width, 640), row_h * len(rows) + pad + 34), (58, 60, 66, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((pad, 8), f"{job}   残した候補 {kept_total}・外した {rej_total}   上: 絵　下: 顔（40px と 2 倍）", font=font, fill=(240, 236, 224))
    y = 34 + pad // 2
    for name, cells, n_rej in rows:
        x = pad
        if name:
            draw.text((x, y), f"{name}（外した {n_rej}）", font=font, fill=(250, 214, 120))
        for im, frame, label in cells:
            pic = scaled(im)
            ty = y + label_h
            # 市松の代わりに、明るい地と暗い地を半分ずつ（透過の縁のにじみを両方で見る）。
            draw.rectangle((x, ty, x + pic.size[0] // 2, ty + cell_h), fill=(206, 200, 188))
            draw.rectangle((x + pic.size[0] // 2, ty, x + pic.size[0], ty + cell_h), fill=(34, 36, 44))
            sheet.alpha_composite(pic, (x, ty))
            f = face_of(im, frame)
            fy = ty + cell_h + 8
            sheet.alpha_composite(round_face(f, face_px), (x, fy + face_px // 2))
            sheet.alpha_composite(round_face(f, face_px * 2), (x + face_px + 10, fy))
            draw.text((x, fy + face_px * 2 + 6), label, font=small, fill=(226, 226, 226))
            x += pic.size[0] + pad
        y += row_h
    dest = top / "sheet.png"
    sheet.convert("RGB").save(dest, "PNG")
    say(f"ARTGEN_SHEET {dest.relative_to(ctx.root).as_posix()} 残した {kept_total}・外した {rej_total}・{sheet.size[0]}x{sheet.size[1]}")
    return 0


def cmd_status(ctx: Ctx) -> int:
    ph = ctx.phase(ctx.ledger())
    say(f"ARTGEN_STATUS 第{ctx.config['phase']}期: {ph['images']}／{ctx.config['phase_cap']} 枚・実行 {ph['runs']} 回・約 ${ph['usd']:.2f}・"
        f"断られた {ph['blocked'] or 'なし'}・キー {'あり' if os.environ.get(KEY_ENV, '').strip() else '無い'}・置き場 {'あり' if ctx.out.is_dir() else '無い'}")
    return 0


# ---- API を呼ばない検査（指示書 §7） ----
def cmd_check() -> int:
    global http_post
    real_post, real_key = http_post, os.environ.pop(KEY_ENV, None)
    ok = True
    calls = []

    def req(passed: bool, what: str) -> None:
        nonlocal ok
        ok &= passed
        say(f"ARTGEN_CHECK {'○' if passed else '×'} {what}")

    def stopped(fn) -> str | None:
        try:
            fn()
        except Stop as e:
            return str(e)
        return None

    try:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            ctx = Ctx(root)
            ctx.prompts.mkdir(parents=True)
            (ctx.prompts / "_style.md").write_text("# 絵柄\n```text\nSTYLE\n```\n", encoding="utf-8")
            (ctx.prompts / "t.md").write_text("メモ\n```text\nBODY\n```\n", encoding="utf-8")
            refdir = root / "DemoApp" / "assets" / "portraits" / "battle"
            refdir.mkdir(parents=True)
            from PIL import Image
            Image.new("RGBA", (64, 96), (200, 40, 40, 255)).save(refdir / "x_idle_right.png")
            (root / "DemoApp" / "PolyBlocks").mkdir()
            Image.new("RGB", (8, 8)).save(root / "DemoApp" / "PolyBlocks" / "bought.png")
            http_post = lambda *a, **k: (calls.append(a[0]), (500, b"{}"))[1]

            run = lambda **k: run_api(ctx, k.get("kind", "gen"), k.get("job", "t"), k.get("n", 1), "1024x1024", None, None, k.get("transparent", False), k.get("refs", []))
            ctx.out.mkdir(parents=True)
            why = stopped(run)
            req(why is not None and KEY_ENV in why and not calls, f"キーが無ければ何も作らない（{why}）")
            os.environ[KEY_ENV] = "sk-test-NOT-A-REAL-KEY"
            why = stopped(lambda: run(n=5))
            req(why is not None and not calls, f"1 回の上限を超えたら作らない（{why}）")
            led = ctx.ledger()
            ctx.phase(led)["images"] = ctx.config["phase_cap"] - 2
            ctx.save_ledger(led)
            why = stopped(lambda: run(n=3))
            req(why is not None and not calls, f"この期の上限を超えたら作らない（{why}）")
            ctx.ledger_path.unlink()
            ctx.out.rmdir()
            why = stopped(run)
            req(why is not None and not calls, f"置き場が無ければ作らない（{why}）")
            ctx.out.mkdir()
            why = stopped(lambda: run(job="nothing"))
            req(why is not None and not calls, f"文が無ければ作らない（{why}）")
            why = stopped(lambda: run(kind="edit", refs=["DemoApp/PolyBlocks/bought.png"]))
            req(why is not None and not calls, f"購入素材は参照に使えない（{why}）")
            why = stopped(lambda: run(kind="edit"))
            req(why is not None and not calls, f"edit は参照の絵が無ければ作らない（{why}）")

            # ここからは通信を偽物に差し替えて、記録の形を見る（本物の API は呼ばない）。
            buf = io.BytesIO()
            img = Image.new("RGB", (64, 64), (255, 0, 255))
            img.paste((40, 60, 200), (16, 12, 48, 64))
            img.save(buf, "PNG")
            fake = json.dumps({"data": [{"b64_json": base64.b64encode(buf.getvalue()).decode()}],
                               "usage": {"input_tokens": 100, "output_tokens": 1000, "input_tokens_details": {"text_tokens": 100, "image_tokens": 0}}}).encode()
            sent = []
            http_post = lambda url, headers, body: (sent.append((url, headers, body)), (200, fake))[1]
            code = run(transparent=True)
            files = sorted((ctx.out / "t").glob("*.png"))
            rec = json.loads(files[0].with_suffix(".json").read_text(encoding="utf-8")) if files else {}
            req(code == 0 and len(files) == 1 and rec.get("prompt") == "STYLE\n\nBODY" and rec.get("model") == ctx.config["model"] and "date" in rec and rec.get("refs") == [],
                f"絵と同じ名前の .json に 文・参照・モデル・設定・日付 が残る（{files[0].name if files else '無い'}）")
            req(rec.get("background") == "keyed" and has_alpha(files[0].read_bytes()), f"透過を頼んで α が無ければ、無地の背景を道具で抜く（{rec.get('keying')}）")
            everything = "".join(p.read_text(encoding="utf-8", errors="replace") for p in root.rglob("*.json"))
            req("sk-test" not in everything, "キーは記録にも帳簿にも書かれない")
            ph = ctx.phase(ctx.ledger())
            req(ph["images"] == 1 and abs(ph["usd"] - 0.0305) < 1e-6, f"帳簿に枚数と料金が乗る（{ph['images']} 枚・${ph['usd']}）")
            code = run(kind="edit", refs=["DemoApp/assets/portraits/battle/x_idle_right.png"])
            url, headers, body = sent[-1]
            req(code == 0 and url.endswith("/edits") and headers["Content-Type"].startswith("multipart/form-data") and b'name="image"' in body and str(root).encode() not in body,
                "edit は参照の絵を multipart で送る（置き場の道筋は送らない）")
            http_post = lambda *a, **k: (400, json.dumps({"error": {"code": "moderation_blocked", "type": "image_generation_user_error", "message": "blocked"}}).encode())
            c1, c2 = run(), run()
            before = len(sent)
            why = stopped(run)
            req(c1 == BLOCKED and c2 == BLOCKED and why is not None and len(sent) == before, f"断られた依頼は 2 回まで（言い換えて 1 回）——3 回目は呼ばない（{why}）")
            cmd_reject(ctx, "t", files[0].name, "検査")
            code = cmd_sheet(ctx, "t", [], None, 240, False)
            req(code == 0 and (ctx.out / "t" / "sheet.png").is_file(), "見比べの絵が出る（外した絵は並べない）")
    finally:
        http_post = real_post
        os.environ.pop(KEY_ENV, None)
        if real_key is not None:
            os.environ[KEY_ENV] = real_key
    say(f"ARTGEN_CHECK_COMPLETE ok={ok}")
    return 0 if ok else 1


def main(argv: list[str]) -> int:
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description="素材の道具（OpenAI の画像 API）")
    sub = ap.add_subparsers(dest="cmd", required=True)
    for name in ("gen", "edit"):
        p = sub.add_parser(name)
        p.add_argument("--job", required=True)
        p.add_argument("--n", type=int, default=1)
        p.add_argument("--size", default="1024x1536")
        p.add_argument("--quality")
        p.add_argument("--model")
        p.add_argument("--transparent", action="store_true")
        if name == "edit":
            p.add_argument("--ref", action="append", default=[])
    p = sub.add_parser("sheet")
    p.add_argument("--job", required=True)
    p.add_argument("--ally", default="rica,dolga", help="両端に置く味方の立ち絵（id を 2 つ）")
    p.add_argument("--face", help="候補の顔の位置 x,y,幅（絵の幅・高さに対する割合）。既定: 四角い絵は全体・立ち絵は .5,.14,.36")
    p.add_argument("--height", type=int, default=560)
    p.add_argument("--no-current", action="store_true", help="今の敵の顔（assets/map/enemies）を並べない")
    p = sub.add_parser("reject")
    p.add_argument("--job", required=True)
    p.add_argument("--file", required=True)
    p.add_argument("--why", required=True)
    sub.add_parser("status")
    sub.add_parser("init")
    sub.add_parser("check")
    a = ap.parse_args(argv)
    try:
        if a.cmd == "check":
            return cmd_check()
        ctx = Ctx()
        if a.cmd == "init":
            ctx.out.mkdir(parents=True, exist_ok=True)
            say(f"ARTGEN_INIT {ctx.out.relative_to(ctx.root).as_posix()}（git に入れない置き場）")
            return 0
        if a.cmd == "status":
            return cmd_status(ctx)
        if a.cmd == "reject":
            return cmd_reject(ctx, a.job, a.file, a.why)
        if a.cmd == "sheet":
            face = tuple(float(v) for v in a.face.split(",")) if a.face else None
            return cmd_sheet(ctx, a.job, [x for x in a.ally.split(",") if x], face, a.height, not a.no_current)
        return run_api(ctx, a.cmd, a.job, a.n, a.size, a.quality, a.model, a.transparent, getattr(a, "ref", []))
    except Stop as e:
        say(f"ARTGEN_STOP {e}")
        return STOP


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
