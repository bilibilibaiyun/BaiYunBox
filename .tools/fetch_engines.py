# -*- coding: utf-8 -*-
"""
BaiYun Box 引擎下载与解压。
- libmpv（LGPL）：播放内核
- 7zr.exe：7z 解压工具
"""
import os, subprocess, shutil, urllib.request

BASE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ENGINE = os.path.join(BASE, "engine")
TOOLS = os.path.join(BASE, ".tools")
os.makedirs(ENGINE, exist_ok=True)
os.makedirs(TOOLS, exist_ok=True)

MPV_TAG = "2026-09-17-0b7ed670f7"
MPV_DATE = "20260917"

def download(url, dest):
    print("[下载]", url, flush=True)
    req = urllib.request.Request(url, headers={"User-Agent": "BaiYunBox-build"})
    with urllib.request.urlopen(req) as r, open(dest, "wb") as f:
        total = int(r.headers.get("Content-Length", 0))
        done = 0
        last = -1
        while True:
            c = r.read(1024 * 256)
            if not c:
                break
            f.write(c)
            done += len(c)
            if total:
                pct = done * 100 // total
                if pct != last:
                    print("  %d%%  %d/%d MB" % (pct, done // 1024 // 1024, total // 1024 // 1024), flush=True)
                    last = pct
    print("[完成]", dest, flush=True)

def run(cmd):
    r = subprocess.run(cmd, capture_output=True)
    return r.returncode, (r.stdout + r.stderr).decode("utf-8", "ignore")

# 1) 7zr.exe（解压工具）
seven_zr = os.path.join(TOOLS, "7zr.exe")
if not os.path.exists(seven_zr):
    download("https://www.7-zip.org/a/7zr.exe", seven_zr)

# 2) LGPL libmpv
mpv_dir = os.path.join(ENGINE, "mpv")
os.makedirs(mpv_dir, exist_ok=True)
if not os.path.exists(os.path.join(mpv_dir, "libmpv-2.dll")):
    pkg = os.path.join(ENGINE, "mpv.7z")
    download(f"https://github.com/zhongfly/mpv-winbuild/releases/download/{MPV_TAG}/mpv-dev-lgpl-x86_64-{MPV_DATE}-git-0b7ed670f7.7z", pkg)
    tmp = os.path.join(ENGINE, "_t")
    shutil.rmtree(tmp, ignore_errors=True)
    os.makedirs(tmp, exist_ok=True)
    run([seven_zr, "x", pkg, "-o" + tmp, "-y"])
    for root, _, files in os.walk(tmp):
        for f in files:
            if f.lower() in ("libmpv-2.dll", "mpv-2.dll"):
                shutil.copy(os.path.join(root, f), os.path.join(mpv_dir, f))
    shutil.rmtree(tmp, ignore_errors=True)
    os.remove(pkg)
    print("[完成] libmpv:", os.listdir(mpv_dir), flush=True)

print("=== 引擎就绪 ===", flush=True)
