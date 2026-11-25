"""One-off diagnosis: why the PreToolUse hook of the hook-ask scenario has no effect on the CI runner.

Runs the scenario's setup the way tools/gravar.py does, with hook events in the stream and a debug file,
answers every permission request with deny and prints what concerns the hook.
"""
import json
import os
import shutil
import subprocess
import sys
import threading
import time
from datetime import datetime

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__))))
import gravar  # noqa: E402


def main():
    with open(gravar.CENARIOS, encoding="utf-8") as f:
        cenario = [c for c in json.load(f) if c["id"] == "hook-ask"][0]
    executavel = gravar.localizar_claude(None)
    print("claude:", executavel)
    for name in ("bash", "git", "pwsh", "powershell"):
        print("which %s: %s" % (name, shutil.which(name)))
    for name in ("TEMP", "SHELL", "MSYSTEM", "CLAUDE_CODE_GIT_BASH_PATH", "COMSPEC", "GITHUB_ACTIONS"):
        print("env %s=%s" % (name, os.environ.get(name)))

    temp = os.environ.get("TEMP") or os.path.expanduser("~")
    pasta = os.path.join(temp, "cvc-demo", "diag-%s" % datetime.now().strftime("%Y%m%d-%H%M%S"))
    os.makedirs(pasta)
    for nome, conteudo in cenario["setup"].items():
        destino = os.path.join(pasta, nome)
        os.makedirs(os.path.dirname(destino), exist_ok=True)
        with open(destino, "w", encoding="utf-8", newline="\n") as f:
            f.write(conteudo)
    print("folder:", pasta, "| real:", os.path.realpath(pasta))

    debug = os.path.join(pasta, "debug.log")
    comando = [executavel, "-p", "--input-format", "stream-json", "--output-format", "stream-json",
               "--verbose", "--include-partial-messages", "--permission-prompt-tool", "stdio",
               "--model", "haiku", "--setting-sources", "project", "--strict-mcp-config",
               "--include-hook-events", "--debug-file", debug]
    proc = subprocess.Popen(comando, cwd=pasta, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                            stderr=subprocess.PIPE, env=gravar.ambiente_do_cenario(cenario, os.environ))

    def send(obj):
        proc.stdin.write((json.dumps(obj) + "\n").encode("utf-8"))
        proc.stdin.flush()

    threading.Thread(target=lambda: [None for _ in proc.stderr], daemon=True).start()
    send({"type": "user", "message": {"role": "user", "content": cenario["passos"][0]["enviar"]},
          "parent_tool_use_id": None, "session_id": "default"})

    deadline = time.time() + 120
    done = threading.Event()

    def read():
        for raw in proc.stdout:
            try:
                ev = json.loads(raw.decode("utf-8", "replace"))
            except ValueError:
                continue
            kind = ev.get("type")
            sub = ev.get("subtype") or (ev.get("request") or {}).get("subtype")
            if kind == "system" and sub == "init":
                print("init: cwd=%s version=%s" % (ev.get("cwd"), ev.get("claude_code_version")))
            elif kind == "system" and "hook" in str(sub):
                print("HOOK EVENT:", json.dumps(ev)[:1500])
            elif kind == "control_request" and sub == "can_use_tool":
                req = ev["request"]
                print("REQUEST: tool=%s reason_type=%s reason=%r suggestions=%s" % (
                    req.get("tool_name"), req.get("decision_reason_type"), req.get("decision_reason"),
                    bool(req.get("permission_suggestions"))))
                send({"type": "control_response", "response": {
                    "subtype": "success", "request_id": ev["request_id"],
                    "response": {"behavior": "deny", "message": "diagnosis"}}})
            elif kind == "result":
                print("result:", ev.get("subtype"))
                done.set()
                return
        done.set()

    threading.Thread(target=read, daemon=True).start()
    done.wait(max(1, deadline - time.time()))
    try:
        proc.stdin.close()
    except OSError:
        pass
    try:
        proc.wait(timeout=20)
    except subprocess.TimeoutExpired:
        proc.kill()

    print("--- debug lines about hooks and the shell")
    shown = 0
    if os.path.exists(debug):
        with open(debug, encoding="utf-8", errors="replace") as f:
            for line in f:
                low = line.lower()
                if any(w in low for w in ("hook", "bash", "shell", "powershell", "git-bash")):
                    print(line.rstrip()[:700])
                    shown += 1
                    if shown >= 120:
                        break
    print("debug lines shown:", shown)


if __name__ == "__main__":
    main()
