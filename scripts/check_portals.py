#!/usr/bin/env python3
"""RB GovPortals Auto-Radar (Task 5.3).

Checks availability and response latency of 4 core Belarusian government portals
using Python 3 standard library only with strict TLS verification.
Sends Russian alert notifications to Telegram ONLY on UP -> DOWN or DOWN -> UP
state transitions.
"""

import argparse
import datetime
import json
import os
import socket
import ssl
import sys
import time
import urllib.error
import urllib.parse
import urllib.request


MSK_TZ = datetime.timezone(datetime.timedelta(hours=3))
USER_AGENT = "Mozilla/5.0 (compatible; BuhlyPortalRadar/1.0; +https://buhly.by)"

PORTALS = [
    {
        "id": "mns",
        "name": "МНС",
        "host": "nalog.gov.by",
        "url": "https://www.nalog.gov.by",
        "fallback_urls": ["https://nalog.gov.by"],
        "stb_http_fallback": None,
    },
    {
        "id": "fszn",
        "name": "ФСЗН 2.0",
        "host": "portal2.ssf.gov.by",
        "url": "https://portal2.ssf.gov.by",
        "fallback_urls": [],
        "stb_http_fallback": None,
    },
    {
        "id": "vat",
        "name": "ЭСЧФ",
        "host": "vat.gov.by",
        "url": "https://vat.gov.by",
        "fallback_urls": [],
        # vat.gov.by uses STB national TLS cipher suites unsupported by standard OpenSSL.
        # Strict TLS is attempted first to diagnose STB handshake vs network outage;
        # when STB cipher mismatch is observed, availability is verified over plain HTTP
        # (never by disabling TLS certificate validation).
        "stb_http_fallback": "http://vat.gov.by/mainPage/",
    },
    {
        "id": "epasluga",
        "name": "Е-Паслуга",
        "host": "e-pasluga.by",
        "url": "https://e-pasluga.by",
        "fallback_urls": [],
        "stb_http_fallback": None,
    },
]


def now_msk_iso():
    return datetime.datetime.now(MSK_TZ).replace(microsecond=0).isoformat()


def now_msk_hhmm():
    return datetime.datetime.now(MSK_TZ).strftime("%H:%M")


def classify_ssl_error(exc):
    text = str(exc)
    upper = text.upper()
    if (
        "UNKNOWN_CIPHER_RETURNED" in upper
        or "UNSUPPORTED_PROTOCOL" in upper
        or "HANDSHAKE_FAILURE" in upper
        or "NO_PROTOCOLS_AVAILABLE" in upper
    ):
        return "stb_or_legacy_cipher"
    if "CERTIFICATE_VERIFY_FAILED" in upper:
        return "cert_verify_failed"
    return "ssl_error"


def _http_get_strict(url, timeout, ssl_context):
    req = urllib.request.Request(
        url,
        headers={
            "User-Agent": USER_AGENT,
            "Accept": "text/html,application/xhtml+xml,*/*;q=0.8",
        },
        method="GET",
    )
    t0 = time.monotonic()
    try:
        with urllib.request.urlopen(req, timeout=timeout, context=ssl_context) as resp:
            status = getattr(resp, "status", None)
            if status is None:
                status = resp.getcode()
            resp.read(256)
            latency_ms = max(1, int(round((time.monotonic() - t0) * 1000)))
            return status, latency_ms, None
    except urllib.error.HTTPError as err:
        latency_ms = max(1, int(round((time.monotonic() - t0) * 1000)))
        if err.code < 500:
            return err.code, latency_ms, None
        return err.code, latency_ms, "HTTP {0}".format(err.code)


def _check_tcp_port(host, port, timeout):
    t0 = time.monotonic()
    with socket.create_connection((host, port), timeout=timeout):
        return max(1, int(round((time.monotonic() - t0) * 1000)))


def probe_portal_once(portal, timeout, strict_ctx):
    """Run one availability check for a portal without disabling TLS validation."""
    urls_to_try = [portal["url"]] + list(portal.get("fallback_urls", []))
    last_error = None
    ssl_status = "ok"

    for target_url in urls_to_try:
        try:
            code, latency_ms, http_err = _http_get_strict(target_url, timeout, strict_ctx)
            if http_err is None:
                return {
                    "up": True,
                    "latency_ms": latency_ms,
                    "http_status": code,
                    "ssl_status": "ok",
                    "error": None,
                }
            last_error = http_err
        except urllib.error.URLError as err:
            reason = err.reason
            if isinstance(reason, ssl.SSLError):
                ssl_status = classify_ssl_error(reason)
                last_error = "SSL ({0}): {1}".format(ssl_status, reason)
                stb_fallback = portal.get("stb_http_fallback")
                if stb_fallback and ssl_status == "stb_or_legacy_cipher":
                    try:
                        tcp_ms = _check_tcp_port(portal["host"], 443, timeout)
                        fb_code, fb_ms, fb_err = _http_get_strict(stb_fallback, timeout, strict_ctx)
                        if fb_err is None:
                            return {
                                "up": True,
                                "latency_ms": min(fb_ms, tcp_ms) if tcp_ms else fb_ms,
                                "http_status": fb_code,
                                "ssl_status": "stb_cipher_diagnosed",
                                "error": None,
                            }
                        last_error = "STB fallback failed: {0}".format(fb_err)
                    except Exception as fb_exc:
                        last_error = "STB fallback error: {0}".format(fb_exc)
            else:
                last_error = "URLError: {0}".format(reason)
        except (socket.timeout, TimeoutError) as err:
            last_error = "Timeout: {0}".format(err)
        except Exception as err:
            last_error = "{0}: {1}".format(type(err).__name__, err)

    return {
        "up": False,
        "latency_ms": None,
        "http_status": None,
        "ssl_status": ssl_status,
        "error": last_error or "Unknown error",
    }


def check_portal_with_retries(portal, retries, backoff, timeout, strict_ctx):
    last_result = None
    for attempt in range(1, retries + 1):
        result = probe_portal_once(portal, timeout, strict_ctx)
        last_result = result
        if result["up"]:
            result["attempts"] = attempt
            return result
        print(
            "  [{0}] attempt {1}/{2} failed: {3}".format(
                portal["host"], attempt, retries, result["error"]
            )
        )
        if attempt < retries and backoff > 0:
            time.sleep(backoff)

    last_result["attempts"] = retries
    return last_result


def load_state(state_path):
    if not os.path.exists(state_path):
        return {"schema_version": 1, "portals": {}}
    try:
        with open(state_path, "r", encoding="utf-8") as fh:
            data = json.load(fh)
        if not isinstance(data, dict) or not isinstance(data.get("portals"), dict):
            return {"schema_version": 1, "portals": {}}
        return data
    except Exception as exc:
        print("[WARN] Failed to read state file {0}: {1}".format(state_path, exc))
        return {"schema_version": 1, "portals": {}}


def save_state(state_path, state):
    parent = os.path.dirname(os.path.abspath(state_path))
    if parent and not os.path.exists(parent):
        os.makedirs(parent, exist_ok=True)
    with open(state_path, "w", encoding="utf-8") as fh:
        json.dump(state, fh, ensure_ascii=False, indent=2)
        fh.write("\n")


def format_down_message(portal, hhmm):
    return (
        "[{0} MSK/BY] 🔴 Сбой на портале {1} ({2}). "
        "Ваш компьютер и ЭЦП в порядке — не переустанавливайте Авест. "
        "Сообщим сразу, как сервер восстановится. (Авто-радар @buhly_by)"
    ).format(hhmm, portal["name"], portal["host"])


def format_up_message(portal, hhmm, latency_ms):
    ms_val = latency_ms if latency_ms is not None else 0
    return (
        "[{0} MSK/BY] 🟢 Портал {1} ({2}) снова работает (отклик {3} мс). "
        "Войти в 1 клик без чужих прокси — в закреплённом посте (buhly.by)."
    ).format(hhmm, portal["name"], portal["host"], ms_val)


def send_telegram_message(bot_token, chat_id, text, strict_ctx, timeout=15):
    """Send Telegram message using strict TLS verification. Never log token."""
    api_url = "https://api.telegram.org/bot{0}/sendMessage".format(bot_token)
    payload = json.dumps(
        {
            "chat_id": chat_id,
            "text": text,
            "disable_web_page_preview": True,
        }
    ).encode("utf-8")
    req = urllib.request.Request(
        api_url,
        data=payload,
        headers={"Content-Type": "application/json; charset=utf-8"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout, context=strict_ctx) as resp:
            body = resp.read().decode("utf-8", errors="replace")
            data = json.loads(body)
            if not data.get("ok"):
                return False, "Telegram API returned ok=false: {0}".format(
                    data.get("description", "unknown error")
                )
            return True, None
    except urllib.error.HTTPError as err:
        err_body = err.read().decode("utf-8", errors="replace")
        return False, "Telegram HTTP {0}: {1}".format(err.code, err_body)
    except Exception as err:
        return False, "Telegram request error ({0})".format(type(err).__name__)


def parse_args(argv):
    parser = argparse.ArgumentParser(description="Check RB GovPortals and alert Telegram on transitions.")
    parser.add_argument(
        "--state-file",
        default=os.environ.get("PORTAL_STATE_FILE", "portal_state.json"),
        help="Path to JSON state file (default: PORTAL_STATE_FILE or portal_state.json)",
    )
    parser.add_argument("--timeout", type=int, default=10, help="HTTP timeout in seconds (default: 10)")
    parser.add_argument("--retries", type=int, default=3, help="Retry attempts per portal (default: 3)")
    parser.add_argument("--backoff", type=int, default=5, help="Backoff seconds between retries (default: 5)")
    parser.add_argument("--dry-run", action="store_true", help="Do not send Telegram messages")
    return parser.parse_args(argv)


def main(argv=None):
    args = parse_args(argv)
    strict_ctx = ssl.create_default_context()

    prev_state = load_state(args.state_file)
    prev_portals = prev_state.get("portals", {})
    new_portals = {}
    alerts = []

    hhmm = now_msk_hhmm()
    checked_iso = now_msk_iso()

    print("Buhly RB GovPortals Radar @ {0}".format(checked_iso))

    for portal in PORTALS:
        pid = portal["id"]
        result = check_portal_with_retries(
            portal,
            retries=args.retries,
            backoff=args.backoff,
            timeout=args.timeout,
            strict_ctx=strict_ctx,
        )
        new_status = "UP" if result["up"] else "DOWN"
        prev_entry = prev_portals.get(pid) or {}
        prev_status = prev_entry.get("status")
        last_transition = prev_entry.get("last_transition_msk")

        print(
            "- {0} ({1}): {2} | latency={3}ms | ssl={4} | prev={5}".format(
                portal["name"],
                portal["host"],
                new_status,
                result["latency_ms"],
                result["ssl_status"],
                prev_status or "NONE",
            )
        )

        if prev_status == "UP" and new_status == "DOWN":
            alerts.append((portal, "DOWN", format_down_message(portal, hhmm)))
            last_transition = checked_iso
        elif prev_status == "DOWN" and new_status == "UP":
            alerts.append((portal, "UP", format_up_message(portal, hhmm, result["latency_ms"])))
            last_transition = checked_iso
        elif prev_status is None:
            print("  [INIT] Baseline state recorded for {0} ({1}). No alert sent.".format(portal["name"], new_status))

        new_portals[pid] = {
            "name": portal["name"],
            "host": portal["host"],
            "url": portal["url"],
            "status": new_status,
            "latency_ms": result["latency_ms"],
            "http_status": result["http_status"],
            "ssl_status": result["ssl_status"],
            "attempts": result["attempts"],
            "error": result["error"],
            "checked_at_msk": checked_iso,
            "last_transition_msk": last_transition,
        }

    tg_token = os.environ.get("TG_BOT_TOKEN", "").strip()
    tg_channel = os.environ.get("TG_CHANNEL_ID", "").strip()
    dry_run = args.dry_run or not tg_token or not tg_channel

    if not tg_token or not tg_channel:
        print(
            "[DRY-RUN] TG_BOT_TOKEN or TG_CHANNEL_ID is not configured in environment. "
            "Telegram notifications are disabled; state will still be tracked."
        )

    if not alerts:
        print("No UP/DOWN state transitions detected.")
    elif dry_run:
        print(
            "[DRY-RUN] Skipping Telegram delivery for {0} transition alert(s):".format(
                len(alerts)
            )
        )
        for portal, kind, msg in alerts:
            print("  [{0} -> {1}] {2}".format(portal["host"], kind, msg))
    else:
        for portal, kind, msg in alerts:
            ok, err = send_telegram_message(tg_token, tg_channel, msg, strict_ctx)
            if ok:
                print("[SENT] {0} ({1}) alert delivered to Telegram.".format(portal["host"], kind))
            else:
                print("[ERROR] Failed to deliver {0} ({1}) alert: {2}".format(portal["host"], kind, err))

    updated_state = {
        "schema_version": 1,
        "updated_at_msk": checked_iso,
        "portals": new_portals,
    }
    save_state(args.state_file, updated_state)
    print("State saved to {0}".format(args.state_file))
    return 0


if __name__ == "__main__":
    sys.exit(main())
