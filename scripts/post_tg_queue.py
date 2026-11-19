#!/usr/bin/env python3
"""Telegram Channel Auto-Poster for @buhly_by.

Publishes queued posts from a JSON file to a Telegram channel using Python 3
standard library only with strict TLS certificate validation.
"""

import argparse
import datetime
import html
import json
import os
import re
import ssl
import sys
import urllib.error
import urllib.request


MSK_TZ = datetime.timezone(datetime.timedelta(hours=3))
TELEGRAM_API_BASE = "https://api.telegram.org"


def _configure_stdio():
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            try:
                stream.reconfigure(encoding="utf-8", errors="replace")
            except Exception:
                pass


def now_msk_iso():
    return datetime.datetime.now(MSK_TZ).replace(microsecond=0).isoformat()


def load_queue(path):
    with open(path, "r", encoding="utf-8") as fh:
        data = json.load(fh)
    if not isinstance(data, list):
        raise ValueError("Queue JSON must be an array of post objects.")
    seen_ids = set()
    for idx, item in enumerate(data):
        if not isinstance(item, dict):
            raise ValueError("Queue item at index {0} must be an object.".format(idx))
        if not item.get("id") or not isinstance(item.get("id"), str):
            raise ValueError("Queue item at index {0} missing string 'id'.".format(idx))
        if item["id"] in seen_ids:
            raise ValueError("Duplicate post id '{0}' in queue.".format(item["id"]))
        seen_ids.add(item["id"])
        if not item.get("text") or not isinstance(item.get("text"), str):
            raise ValueError("Queue item '{0}' missing string 'text'.".format(item["id"]))
    return data


def load_state(path):
    if not os.path.exists(path):
        return {"sent_ids": []}
    try:
        with open(path, "r", encoding="utf-8") as fh:
            data = json.load(fh)
        if not isinstance(data, dict):
            return {"sent_ids": []}
        raw_ids = data.get("sent_ids", [])
        if not isinstance(raw_ids, list):
            raw_ids = []
        sent_ids = []
        for item in raw_ids:
            if isinstance(item, str) and item and item not in sent_ids:
                sent_ids.append(item)
        state = dict(data)
        state["sent_ids"] = sent_ids
        return state
    except Exception as exc:
        print(
            "[WARN] Failed to read state file '{0}' ({1}); starting fresh.".format(
                path, type(exc).__name__
            ),
            file=sys.stderr,
        )
        return {"sent_ids": []}


def save_state(path, state):
    parent = os.path.dirname(os.path.abspath(path))
    if parent and not os.path.isdir(parent):
        os.makedirs(parent, exist_ok=True)
    tmp_path = path + ".tmp"
    with open(tmp_path, "w", encoding="utf-8") as fh:
        json.dump(state, fh, ensure_ascii=False, indent=2)
        fh.write("\n")
    os.replace(tmp_path, path)


def call_telegram_api(token, method, payload, timeout, strict_ctx):
    url = "{0}/bot{1}/{2}".format(TELEGRAM_API_BASE, token, method)
    body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=body,
        headers={"Content-Type": "application/json; charset=utf-8"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout, context=strict_ctx) as resp:
            raw = resp.read().decode("utf-8", errors="replace")
            data = json.loads(raw)
            if data.get("ok") is not True:
                return False, None, "Telegram API ok=false: {0}".format(
                    data.get("description", "unknown error")
                )
            return True, data.get("result"), None
    except urllib.error.HTTPError as err:
        err_body = err.read().decode("utf-8", errors="replace")
        return False, None, "Telegram HTTP {0}: {1}".format(err.code, err_body)
    except Exception as err:
        # Never expose full URL or token in logs.
        return False, None, "Telegram request error ({0})".format(type(err).__name__)


def normalize_plain_text(raw_html):
    text = html.unescape(re.sub(r"<[^>]+>", " ", raw_html or ""))
    return re.sub(r"\s+", " ", text).strip()


def resolve_channel_username(token, channel_id, timeout, strict_ctx):
    if channel_id.startswith("@"):
        return channel_id.lstrip("@").strip()
    ok, result, _ = call_telegram_api(
        token,
        "getChat",
        {"chat_id": channel_id},
        timeout=timeout,
        strict_ctx=strict_ctx,
    )
    if ok and isinstance(result, dict):
        return (result.get("username") or "").strip()
    return ""


def fetch_recent_channel_texts(username, timeout, strict_ctx):
    if not username:
        return []
    url = "https://t.me/s/{0}".format(username)
    req = urllib.request.Request(
        url,
        headers={"User-Agent": "Mozilla/5.0 (compatible; BuhlyAutoPoster/1.0)"},
        method="GET",
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout, context=strict_ctx) as resp:
            page = resp.read().decode("utf-8", errors="replace")
    except Exception:
        return []

    texts = []
    for match in re.finditer(
        r'<div class="tgme_widget_message_text[^"]*"[^>]*>(.*?)</div>',
        page,
        re.S,
    ):
        clean = normalize_plain_text(match.group(1))
        if clean:
            texts.append(clean)
    return texts


def is_post_already_on_channel(post_text, channel_texts):
    if not channel_texts:
        return False
    norm = normalize_plain_text(post_text)
    if len(norm) < 30:
        return False
    prefix = norm[:80]
    for ch_text in channel_texts:
        if ch_text == norm:
            return True
        if len(prefix) >= 40 and ch_text.startswith(prefix):
            return True
    return False


def select_post(queue, sent_ids, requested_id, force):
    target_id = (requested_id or "auto").strip()
    if target_id == "auto":
        for item in queue:
            if item["id"] not in sent_ids:
                return item, None
        return None, "Queue empty: all scheduled posts already published."

    for item in queue:
        if item["id"] == target_id:
            if target_id in sent_ids and not force:
                return None, "Post '{0}' already published (use --force to resend).".format(
                    target_id
                )
            return item, None

    raise KeyError("Post id '{0}' not found in queue.".format(target_id))


def _parse_edit_message_id(raw_value):
    text = (raw_value or "0").strip()
    if not text:
        return 0
    val = int(text)
    if val < 0:
        raise argparse.ArgumentTypeError("edit-message-id must be >= 0")
    return val


def parse_args(argv):
    parser = argparse.ArgumentParser(
        description="Publish next unsent Telegram post from queue JSON."
    )
    parser.add_argument(
        "--queue",
        default="scripts/tg_autopost_queue.json",
        help="Path to queue JSON file (default: scripts/tg_autopost_queue.json)",
    )
    parser.add_argument(
        "--state",
        default="tg_posts_state.json",
        help="Path to state JSON file (default: tg_posts_state.json)",
    )
    parser.add_argument(
        "--post-id",
        default="auto",
        help="Specific post id to send, or 'auto' for next unsent (default: auto)",
    )
    parser.add_argument(
        "--force",
        action="store_true",
        help="Allow resending a specific --post-id even if already in sent_ids",
    )
    parser.add_argument(
        "--edit-message-id",
        type=_parse_edit_message_id,
        default=0,
        help="If > 0, edit an existing channel message_id instead of sendMessage",
    )
    parser.add_argument(
        "--dry-run-mark",
        action="store_true",
        help="Local test helper: mark post as sent in state file during dry-run",
    )
    parser.add_argument(
        "--timeout",
        type=int,
        default=15,
        help="HTTP timeout in seconds (default: 15)",
    )
    return parser.parse_args(argv)


def main(argv=None):
    _configure_stdio()
    args = parse_args(argv)
    strict_ctx = ssl.create_default_context()

    try:
        queue = load_queue(args.queue)
    except Exception as exc:
        print("[ERROR] Failed to load queue '{0}': {1}".format(args.queue, exc), file=sys.stderr)
        return 1

    state = load_state(args.state)
    sent_ids = list(state.get("sent_ids", []))
    sent_messages = state.get("sent_messages")
    if not isinstance(sent_messages, dict):
        sent_messages = {}

    tg_token = os.environ.get("TG_BOT_TOKEN", "").strip()
    tg_channel = os.environ.get("TG_CHANNEL_ID", "").strip()

    # Secondary duplicate protection: if state cache was evicted/empty, inspect recent
    # public channel messages before choosing the next post to publish.
    if args.edit_message_id <= 0 and not args.force and tg_token and tg_channel:
        username = resolve_channel_username(tg_token, tg_channel, args.timeout, strict_ctx)
        recent_texts = fetch_recent_channel_texts(username, args.timeout, strict_ctx)
        if recent_texts:
            state_dirty = False
            for item in queue:
                if item["id"] not in sent_ids and is_post_already_on_channel(item["text"], recent_texts):
                    print(
                        "[DEDUP] Post '{0}' already present on @{1}; syncing state.".format(
                            item["id"], username
                        )
                    )
                    sent_ids.append(item["id"])
                    state_dirty = True
            if state_dirty:
                state["sent_ids"] = sent_ids
                state["sent_messages"] = sent_messages
                state["updated_at_msk"] = now_msk_iso()
                save_state(args.state, state)

    try:
        post, notice = select_post(queue, sent_ids, args.post_id, args.force)
    except KeyError as exc:
        print("[ERROR] {0}".format(exc), file=sys.stderr)
        return 1

    if post is None:
        print(notice)
        return 0

    post_id = post["id"]
    should_pin = bool(post.get("pin", False))
    preview = post["text"][:120].replace("\n", "\\n")

    if not tg_token or not tg_channel:
        action = "edit message_id={0}".format(args.edit_message_id) if args.edit_message_id > 0 else "publish"
        print(
            "[DRY-RUN] TG_BOT_TOKEN or TG_CHANNEL_ID not set. "
            "Would {0} id='{1}' (pin={2}), preview (first 120 chars): {3}".format(
                action, post_id, should_pin, preview
            )
        )
        if args.dry_run_mark:
            if post_id not in sent_ids:
                sent_ids.append(post_id)
            state["sent_ids"] = sent_ids
            state["updated_at_msk"] = now_msk_iso()
            save_state(args.state, state)
            print("[DRY-RUN] Marked '{0}' in {1}".format(post_id, args.state))
        return 0

    if args.edit_message_id > 0:
        ok, result, err = call_telegram_api(
            tg_token,
            "editMessageText",
            {
                "chat_id": tg_channel,
                "message_id": args.edit_message_id,
                "text": post["text"],
                "parse_mode": "HTML",
                "disable_web_page_preview": False,
            },
            timeout=args.timeout,
            strict_ctx=strict_ctx,
        )
        if not ok:
            print("[ERROR] Failed to edit message_id={0} for '{1}': {2}".format(args.edit_message_id, post_id, err), file=sys.stderr)
            return 1
        print("[EDITED] Updated '{0}' in message_id={1}.".format(post_id, args.edit_message_id))
        return 0

    ok, result, err = call_telegram_api(
        tg_token,
        "sendMessage",
        {
            "chat_id": tg_channel,
            "text": post["text"],
            "parse_mode": "HTML",
            "disable_web_page_preview": False,
        },
        timeout=args.timeout,
        strict_ctx=strict_ctx,
    )
    if not ok:
        print("[ERROR] Failed to publish '{0}': {1}".format(post_id, err), file=sys.stderr)
        return 1

    message_id = result.get("message_id") if isinstance(result, dict) else None
    print("[SENT] Published '{0}' (message_id={1}).".format(post_id, message_id))

    if should_pin:
        if message_id is not None:
            pin_ok, _, pin_err = call_telegram_api(
                tg_token,
                "pinChatMessage",
                {
                    "chat_id": tg_channel,
                    "message_id": message_id,
                    "disable_notification": True,
                },
                timeout=args.timeout,
                strict_ctx=strict_ctx,
            )
            if pin_ok:
                print("[PINNED] Pinned message_id={0} silently.".format(message_id))
            else:
                print(
                    "[WARN] Post '{0}' sent, but pinChatMessage failed: {1}".format(
                        post_id, pin_err
                    ),
                    file=sys.stderr,
                )
        else:
            print(
                "[WARN] Post '{0}' has pin=true, but Telegram did not return message_id.".format(
                    post_id
                ),
                file=sys.stderr,
            )

    if post_id not in sent_ids:
        sent_ids.append(post_id)
    sent_messages[post_id] = {
        "message_id": message_id,
        "sent_at_msk": now_msk_iso(),
    }
    state["sent_ids"] = sent_ids
    state["sent_messages"] = sent_messages
    state["updated_at_msk"] = now_msk_iso()
    save_state(args.state, state)
    print("State saved to {0} (sent_ids={1}).".format(args.state, len(sent_ids)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
