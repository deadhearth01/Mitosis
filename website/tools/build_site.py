#!/usr/bin/env python3
"""Builds the Mitosis website's generated pages: guides, the Parall comparison, sitemap.xml, robots.txt, llms.txt,
site.webmanifest, and the structured data in index.html.

Run from anywhere:  python3 website/tools/build_site.py
Pages are plain HTML in website/public (served as static assets by Cloudflare Workers).
"""
import html
import json
import re
from datetime import date
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PUBLIC = ROOT / "website" / "public"
SITE = "https://mitosis.theavni.studio"
REPO = "https://github.com/deadhearth01/Mitosis"
INSTALL = "curl -fsSL https://raw.githubusercontent.com/deadhearth01/Mitosis/main/scripts/install.sh | bash"
BREW = "brew install --cask deadhearth01/tap/mitosis"
TODAY = date.today().isoformat()
VERSION = re.search(r'version = "([^"]+)"', (ROOT / "Sources/MitosisCore/Mitosis.swift").read_text()).group(1)

INSTALL_STEP = (f'Paste this in Terminal:<code class="command-block">{html.escape(INSTALL)}</code>'
                f'Or use Homebrew: <code>{BREW}</code>. Free; macOS 15 or later on Apple silicon.')

ORG = {"@type": "Organization", "name": "The Avni Studio", "url": "https://theavni.studio"}

IMG = {
    "pick": ("/assets/guides/new-clone-pick.webp", 1200, 1165, "Choosing the app to clone in Mitosis; every app shows whether it works great, works with limits, or isn't supported yet"),
    "details": ("/assets/guides/new-clone-details.webp", 1200, 825, "Adding a label and badge for the copy, with a live preview of its Dock icon"),
    "done": ("/assets/guides/new-clone-done.webp", 1200, 825, "Mitosis confirms the copy is ready after checking that it starts"),
    "links": ("/assets/guides/sign-in-links.webp", 1288, 1400, "A Claude clone's page in Mitosis with the switch Send sign-in links to the right copy"),
    "codex": ("/assets/guides/codex-details.webp", 1200, 825, "Making a Codex clone named Codex (Work) in Mitosis, with a live preview of its badge"),
    "claude": ("/assets/guides/claude-details.webp", 1200, 825, "Making a Claude clone named Claude (Personal) in Mitosis, with a live preview of its badge"),
    "main": ("/assets/screen-light.webp", 1600, 1165, "The Mitosis window: clones listed by app, and a clone's page with usage stats"),
}

# ---------------------------------------------------------------------------------------------------------------
# Content. Keep it specific and true: say what the app itself can and can't do, then how Mitosis helps.

GUIDES = [
    {
        "slug": "open-the-same-app-twice-on-mac",
        "title": "How to open two copies of the same app on a Mac",
        "seo_title": "How to Open Two Copies of the Same App on a Mac (2026)",
        "description": "macOS runs one copy of each app. Here's how to run two copies side by side, each with its own login and data, and why open -n usually isn't enough.",
        "keywords": "open app twice mac, run multiple instances of an app mac, open two instances mac, duplicate app mac",
        "intro": [
            "A Mac runs one copy of each app. Click the icon again and macOS brings the window you already have to the front. That's fine until you need two of something: a work and a personal Slack, two Discord accounts, or a second Claude for a different team.",
            "This guide shows the quick Terminal trick, why it usually falls short, and how to make a real second copy that keeps its own login, data, and Dock icon.",
        ],
        "answer": "Use Mitosis (free) to make a clone of the app, such as <em>Slack (Work)</em>. The clone is a separate app with its own login, settings, and Dock icon, and it runs next to the original.",
        "sections": [
            ("Why <code>open -n</code> isn't enough", [
                "Terminal can start a new instance: <code>open -n -a \"Slack\"</code>. But that instance shares everything with the first one: the same login, the same settings, the same Dock icon. Many apps also notice the copy already running and quit straight away, because they only allow one at a time.",
                "To be useful, a second copy needs its own data folder and its own identity, so the app and macOS treat it as a different app.",
            ]),
        ],
        "steps": [
            ("Install Mitosis", INSTALL_STEP),
            ("Choose the app", "Click <strong>New Clone</strong> and pick the app. Each one is marked <em>Works great</em>, <em>Works with limits</em>, or <em>Not supported</em>, so you know before you start."),
            ("Add a label", "Type a label like <strong>Work</strong>. Mitosis names the copy <em>Slack (Work)</em> and gives its Dock icon a badge so you can tell them apart."),
            ("Sign in to the copy", "Mitosis opens the copy once to check that it starts. Sign in with your other account; the original stays signed in as before."),
        ],
        "images": ["pick", "details"],
        "tips": [
            "Copies share the original's files on disk (APFS), so a clone usually adds only a couple of megabytes.",
            "When the original app updates, Mitosis rebuilds its clones automatically and keeps your logins.",
            "macOS treats each copy as a new app, so it may ask again for permissions like notifications or the camera.",
        ],
        "faqs": [
            ("Can I run two instances of any app on a Mac?", "Most apps built with Electron or Chromium work well, including Slack, Discord, Signal, VS Code, Notion, Claude, and Codex. Apple's own apps can't be copied, and sandboxed apps that rely on iCloud (like WhatsApp) aren't supported yet."),
            ("Does a copy change the original app?", "No. Mitosis never modifies the original. Each copy is a separate app in ~/Applications/Mitosis with its data in ~/Library/Mitosis."),
            ("Is it safe?", "Mitosis is free and source-available, so you can read every line. Copies are signed locally on your Mac, and deleting a copy moves it to the Trash."),
        ],
        "related": ["two-slack-accounts-on-mac", "two-discord-accounts-on-mac", "parall-alternative"],
    },
    {
        "slug": "two-slack-accounts-on-mac",
        "title": "How to use two Slack accounts at the same time on a Mac",
        "seo_title": "Two Slack Accounts at Once on Mac (Separate Logins)",
        "description": "Keep a work and a personal (or client) Slack fully separate on your Mac: two windows, two Dock icons, two sets of notifications, both signed in at once.",
        "keywords": "two slack accounts mac, multiple slack accounts at once, slack work and personal mac, second slack app mac",
        "intro": [
            "Slack's Mac app can hold several workspaces in one window, and that's often enough. It stops being enough when you want work and personal Slack truly apart: separate windows you can put on different desktops, notifications you can tell apart, or two accounts in the same workspace.",
            "The answer is a second copy of Slack with its own login.",
        ],
        "answer": "Make a clone of Slack with Mitosis, label it <em>Personal</em> (or the client's name), and sign in there. You get <em>Slack (Personal)</em> next to your normal Slack, each with its own login, Dock icon, and notifications.",
        "sections": [
            ("Signing in through the browser", [
                "Slack often signs you in through your browser and then hands a <code>slack://</code> link back to the app. macOS gives that link to the original Slack, so the wrong copy can end up signed in.",
                "Turn on <strong>Send sign-in links to the right copy</strong> on the clone's page in Mitosis. The link then goes to the copy you signed in from, and Mitosis asks which one when both are open.",
            ]),
        ],
        "steps": [
            ("Install Mitosis", INSTALL_STEP),
            ("Clone Slack", "Click <strong>New Clone</strong>, choose <strong>Slack</strong>, and add a label like <strong>Personal</strong>."),
            ("Route sign-in links", "On the clone's page, turn on <strong>Send sign-in links to the right copy</strong>."),
            ("Sign in", "Open <em>Slack (Personal)</em> and sign in with your other account."),
        ],
        "images": ["pick", "main"],
        "tips": [
            "Keep both copies in the Dock. The badge (a letter or an emoji) shows which is which.",
            "Each copy keeps its own preferences, so you can mute channels or set Do Not Disturb separately.",
            "Slack updates itself; Mitosis rebuilds the clone afterwards, keeping you signed in.",
        ],
        "faqs": [
            ("Can I be signed in to two Slack accounts at the same time?", "Yes. Each Mitosis clone of Slack is a separate app with its own login, so both stay signed in and both send notifications."),
            ("Isn't adding a workspace enough?", "Often it is. Use a clone when you want separate windows and Dock icons, separate notification settings, or two different accounts in the same workspace."),
            ("Will my original Slack change?", "No. The original keeps its login and settings; the clone has its own data folder."),
        ],
        "related": ["open-the-same-app-twice-on-mac", "two-discord-accounts-on-mac", "two-claude-accounts-on-mac"],
    },
    {
        "slug": "two-signal-accounts-on-mac",
        "title": "How to use two Signal accounts on one Mac",
        "seo_title": "Two Signal Accounts on One Mac (Signal Desktop for Two Numbers)",
        "description": "Signal Desktop links to one phone number. Here's how to run a second copy on your Mac and link it to a second number, so both accounts are open at once.",
        "keywords": "two signal accounts mac, signal desktop multiple accounts, signal desktop two phone numbers, second signal mac",
        "intro": [
            "Signal Desktop is linked to one phone number, and there's no switch for a second account. If you have a work phone and a personal phone, you'd normally have to unlink and relink every time.",
            "With a second copy of Signal on your Mac, each copy links to its own phone, and both run side by side.",
        ],
        "answer": "Clone Signal with Mitosis (label it <em>Work</em>), open the clone, and link it from your second phone in Signal under <strong>Settings › Linked devices</strong>. Each copy keeps its own messages and account.",
        "sections": [],
        "steps": [
            ("Install Mitosis", INSTALL_STEP),
            ("Clone Signal", "Click <strong>New Clone</strong>, choose <strong>Signal</strong>, and add a label like <strong>Work</strong>."),
            ("Link the second phone", "Open <em>Signal (Work)</em>. It shows a QR code. On your second phone, open Signal › Settings › Linked devices and scan it."),
            ("Use both", "Your original Signal stays linked to your first phone. Each copy has its own messages, settings, and notifications."),
        ],
        "images": ["details", "done"],
        "tips": [
            "Messages stay on the copy they belong to; the copies never share data.",
            "Signal updates itself; Mitosis rebuilds the clone after an update and keeps it linked.",
            "Give each copy a different badge color so you can tell them apart in the Dock and in notifications.",
        ],
        "faqs": [
            ("Can Signal Desktop use two phone numbers?", "Not in one app. A Mitosis clone is a second Signal app, so you can link it to a second phone number while the original stays linked to the first."),
            ("Is this secure?", "Each copy is the official Signal app with its own data folder. Mitosis doesn't touch your messages; it only gives the copy its own place to keep them."),
            ("What happens if I delete the copy?", "It moves to the Trash. You can keep or remove its data, and unlink the device from your phone afterwards."),
        ],
        "related": ["open-the-same-app-twice-on-mac", "two-slack-accounts-on-mac", "parall-alternative"],
    },
    {
        "slug": "two-discord-accounts-on-mac",
        "title": "How to be online on two Discord accounts at once on a Mac",
        "seo_title": "Two Discord Accounts at Once on Mac (Both Online, Separate Windows)",
        "description": "Discord's account switcher shows one account at a time. Run a second Discord on your Mac to keep two accounts online together, each with its own window and notifications.",
        "keywords": "two discord accounts at once mac, multiple discord accounts mac, discord second account desktop, run two discord mac",
        "intro": [
            "Discord can switch between accounts, but only one is online at a time. If you run a community account and a personal one, switching means missing messages on the other.",
            "A second copy of Discord keeps both online.",
        ],
        "answer": "Clone Discord with Mitosis, label it (for example <em>Community</em>), and sign in to your second account there. Both copies stay online with their own windows, notifications, and Dock icons.",
        "sections": [],
        "steps": [
            ("Install Mitosis", INSTALL_STEP),
            ("Clone Discord", "Click <strong>New Clone</strong>, choose <strong>Discord</strong>, and add a label."),
            ("Sign in", "Open the copy and sign in with your second account."),
            ("Keep both open", "Each copy has its own status, servers, and notification settings."),
        ],
        "images": ["pick", "main"],
        "tips": [
            "Voice works in both copies, but each one picks its microphone and speakers separately.",
            "macOS asks each copy for microphone and camera access the first time it needs them.",
            "Discord updates itself; Mitosis rebuilds the clone afterwards and keeps you signed in.",
        ],
        "faqs": [
            ("Can I use two Discord accounts at the same time on a Mac?", "Yes, with a second copy of the app. Discord's own switcher only shows one account at a time; a Mitosis clone keeps both online."),
            ("Does it break Discord's rules?", "Mitosis only keeps each account's data separate on your Mac. Using more than one account is up to you and Discord's terms."),
            ("How much space does a copy use?", "Usually a few megabytes, because the copy shares the original's files on disk."),
        ],
        "related": ["two-slack-accounts-on-mac", "open-the-same-app-twice-on-mac", "parall-alternative"],
    },
    {
        "slug": "two-claude-accounts-on-mac",
        "title": "How to use two Claude accounts on a Mac",
        "seo_title": "Two Claude Accounts on One Mac (Claude Desktop App, Side by Side)",
        "description": "The Claude desktop app signs in to one account at a time. Run a second copy on your Mac for a work and a personal Claude, both open at once.",
        "keywords": "two claude accounts mac, multiple claude accounts desktop, claude desktop second account, claude work and personal",
        "intro": [
            "The Claude app for Mac signs in to one account at a time. If you have a work account through your company and a personal one, switching means signing out and in again.",
            "A second copy of Claude keeps both ready.",
        ],
        "answer": "Clone Claude with Mitosis, label it <em>Personal</em>, and sign in there. Your work Claude and <em>Claude (Personal)</em> run side by side, each with its own chats, settings, and login.",
        "sections": [
            ("Signing in through the browser", [
                "Claude can finish signing in through your browser and send a link back to the app. Turn on <strong>Send sign-in links to the right copy</strong> on the clone's page in Mitosis, so the link reaches the copy you signed in from.",
            ]),
        ],
        "steps": [
            ("Install Mitosis", INSTALL_STEP),
            ("Clone Claude", "Click <strong>New Clone</strong>, choose <strong>Claude</strong>, and add a label like <strong>Personal</strong>."),
            ("Route sign-in links", "On the clone's page, turn on <strong>Send sign-in links to the right copy</strong>."),
            ("Sign in", "Open <em>Claude (Personal)</em> and sign in with your other account."),
        ],
        "images": ["claude", "links"],
        "tips": [
            "Each copy keeps its own settings and connected tools.",
            "Claude updates itself; Mitosis rebuilds the clone afterwards and keeps your login.",
            "Use a badge emoji or color per account so your Dock stays clear.",
        ],
        "faqs": [
            ("Can I be signed in to two Claude accounts at the same time?", "Yes, with a second copy of the Claude app. Each Mitosis clone has its own login and data."),
            ("Does the original Claude change?", "No. It keeps its account and chats; the clone is a separate app."),
            ("Does this work with Claude Code in Terminal?", "This guide is about the desktop app. Mitosis copies Mac apps; command-line tools are separate."),
        ],
        "related": ["two-codex-accounts-on-mac", "two-slack-accounts-on-mac", "open-the-same-app-twice-on-mac"],
    },
    {
        "slug": "two-codex-accounts-on-mac",
        "title": "How to use two Codex accounts on one Mac",
        "seo_title": "Two Codex Accounts on One Mac (Codex Desktop App, Separate Logins)",
        "description": "The Codex app keeps one account in ~/.codex. Run a second copy on your Mac with its own Codex folder, so each copy signs in to a different account.",
        "keywords": "two codex accounts mac, codex desktop multiple accounts, codex second account, CODEX_HOME separate account",
        "intro": [
            "The Codex app for Mac keeps its sign-in, settings, and history in a <code>~/.codex</code> folder. That's why simply copying the app isn't enough: a copy would read the same folder and show the same account.",
            "Mitosis gives each Codex copy its own Codex folder, so each one signs in separately.",
        ],
        "answer": "Clone Codex with Mitosis (0.2.1 or later). Each clone gets its own Codex folder (its own <code>CODEX_HOME</code>), starts signed out, and keeps its own account, settings, and history. Your original <code>~/.codex</code> stays as it is.",
        "sections": [
            ("How it works", [
                "Codex reads the <code>CODEX_HOME</code> setting to decide where its account lives (by default <code>~/.codex</code>). Mitosis starts each Codex copy with <code>CODEX_HOME</code> pointing at a folder inside that copy's data, so the copies never see each other's sign-in.",
                "The Codex command-line tool you use in Terminal keeps using <code>~/.codex</code>, so it stays signed in as before.",
            ]),
        ],
        "steps": [
            ("Install or update Mitosis", INSTALL_STEP + " Codex clones need Mitosis 0.2.1 or later."),
            ("Clone Codex", "Click <strong>New Clone</strong>, choose <strong>Codex</strong>, and add a label like <strong>Work</strong>."),
            ("Sign in", "Open <em>Codex (Work)</em>. It starts signed out; sign in with your other account."),
        ],
        "images": ["codex", "main"],
        "tips": [
            "Each copy has its own settings, projects, and history, separate from the original.",
            "Made a Codex clone with an older Mitosis? Mitosis rebuilds it automatically after you update; then sign in again inside it.",
            "Codex updates itself; Mitosis rebuilds the clone afterwards and keeps its login.",
        ],
        "faqs": [
            ("Why did my Codex copy show the same account?", "Codex keeps its account in ~/.codex, outside the app's normal data folder. Mitosis 0.2.1 gives each copy its own Codex folder, so copies sign in separately."),
            ("Does it affect the codex command in Terminal?", "No. The command-line tool keeps using ~/.codex."),
            ("Can I go back?", "Delete the clone from Mitosis; it moves to the Trash with its data if you choose. Your original Codex never changes."),
        ],
        "related": ["two-claude-accounts-on-mac", "open-the-same-app-twice-on-mac", "two-slack-accounts-on-mac"],
    },
]


# ---------------------------------------------------------------------------------------------------------------
# More guides. Categories group the guides page.

CATEGORIES = ["Getting started", "Apps", "How it works", "Help"]
for g in GUIDES:
    g.setdefault("category", "Apps")
GUIDES[0]["category"] = "Getting started"


def app_guide(app, slug, label, hook, native, *, why=None, tips=None, faqs=None, related=None, images=("pick", "main"), keywords=""):
    """A guide for running two accounts of one Electron/Chromium app; facts specific to the app go in hook/native/why."""
    clone = f"{app} ({label})"
    return {
        "slug": slug, "category": "Apps",
        "title": f"How to use two {app} accounts on one Mac",
        "seo_title": f"Two {app} Accounts on One Mac, Side by Side",
        "description": f"Run a second copy of {app} on your Mac with its own login, settings, and Dock icon, so two {app} accounts stay open at once.",
        "keywords": keywords or f"two {app.lower()} accounts mac, multiple {app.lower()} accounts, second {app.lower()} app mac",
        "intro": [hook, native],
        "answer": f"Clone {app} with Mitosis, label it <em>{label}</em>, and sign in there. <em>{clone}</em> runs next to your normal {app}, each with its own login, data, and Dock icon.",
        "sections": [("Why a second copy", why)] if why else [],
        "steps": [
            ("Install Mitosis", INSTALL_STEP),
            (f"Clone {app}", f"Click <strong>New Clone</strong>, choose <strong>{app}</strong>, and add a label like <strong>{label}</strong>."),
            ("Sign in", f"Open <em>{clone}</em>. It starts signed out; sign in with your other account."),
        ],
        "images": list(images),
        "tips": tips or [
            f"The original {app} keeps its account and settings; the copy never touches them.",
            f"{app} updates itself; Mitosis rebuilds the copy afterwards and keeps you signed in.",
            "Give each copy its own badge letter, emoji, or color so you can tell them apart in the Dock.",
        ],
        "faqs": faqs or [
            (f"Can I be signed in to two {app} accounts at the same time?", f"Yes. A Mitosis clone of {app} is a separate app with its own login, so both stay signed in."),
            (f"Does the clone change my original {app}?", f"No. The original keeps its account, settings, and data. The clone lives in ~/Applications/Mitosis with its data in ~/Library/Mitosis."),
        ],
        "related": related or ["open-the-same-app-twice-on-mac", "keep-work-and-personal-apps-separate-on-mac", "clones-and-app-updates"],
    }


GUIDES += [
    app_guide("Notion", "two-notion-accounts-on-mac", "Client",
              "Notion's app can switch between accounts, but you see one at a time. If you work with a client's Notion and your own, switching back and forth gets old.",
              "A second copy of Notion keeps both accounts open in their own windows.",
              keywords="two notion accounts mac, notion multiple accounts at once, notion work and personal mac"),
    app_guide("Cursor", "two-cursor-accounts-on-mac", "Work",
              "Cursor signs in to one account at a time. If your company pays for one Cursor account and you use another for side projects, you'd normally sign out and in again.",
              "A second copy of Cursor keeps each account, with its own settings and extensions, in its own window.",
              why=["Each copy has its own settings, extensions, and sign-in, so a work setup can stay strict while a personal one stays relaxed. Projects on disk are shared, as usual; open whichever folder you like in either copy."],
              keywords="two cursor accounts mac, cursor multiple accounts, cursor work and personal account"),
    app_guide("VS Code", "two-vs-code-instances-on-mac", "Work",
              "VS Code has profiles for different settings, but it signs in to one GitHub or Microsoft account for Settings Sync and Copilot at a time.",
              "A second copy of VS Code keeps a separate sign-in, settings, and extensions, and runs next to the first.",
              why=["Use VS Code's own profiles when you only want different settings. Use a copy when you also want a different account signed in, or two windows that never share extensions or state."],
              keywords="two vs code instances mac, vscode different github accounts, vscode separate copilot account"),
    app_guide("Figma", "two-figma-accounts-on-mac", "Client",
              "Figma's desktop app shows one account at a time. Designers who work in a client's Figma organization and their own often switch accounts all day.",
              "A second copy of Figma keeps both accounts signed in, each with its own window and recent files.",
              keywords="two figma accounts mac, figma multiple accounts desktop app, figma switch account"),
    app_guide("Canva", "two-canva-accounts-on-mac", "Team",
              "Canva's desktop app signs in to one account at a time. If you design for a team account and a personal one, switching gets in the way.",
              "A second copy of Canva keeps both accounts signed in, side by side.",
              keywords="two canva accounts mac, canva multiple accounts desktop, canva team and personal"),
    app_guide("Postman", "two-postman-accounts-on-mac", "Client",
              "Postman signs in to one account and workspace setup at a time. If you test APIs for a client under their account and for yourself under yours, switching is slow.",
              "A second copy of Postman keeps a separate account, workspaces, and settings.",
              keywords="two postman accounts mac, postman multiple accounts, postman separate workspace account"),
    {
        "slug": "second-google-chrome-on-mac", "category": "Apps",
        "title": "Chrome profiles or a second Chrome app on a Mac?",
        "seo_title": "Chrome Profiles vs a Second Chrome App on Mac (Separate Dock Icon)",
        "description": "Chrome profiles already keep accounts apart. Here's when that's enough, and when a second Chrome app with its own Dock icon and windows is the better fit.",
        "keywords": "second chrome app mac, chrome separate dock icon profile, chrome work profile separate app mac",
        "intro": [
            "Chrome profiles already keep work and personal browsing apart, each with its own sign-in, history, and extensions. For most people that's all they need.",
            "Profiles still share one app, though: one Dock icon, one entry in the app switcher, and windows that mix together. Some people want work Chrome to be a separate app.",
        ],
        "answer": "Use Chrome's profiles if you just want separate accounts. If you want work Chrome as its own app, with its own Dock icon and app-switcher entry, clone Chrome with Mitosis and label it <em>Work</em>.",
        "sections": [
            ("When a second Chrome helps", [
                "<strong>Separate app switching.</strong> Command-Tab shows <em>Google Chrome (Work)</em> as its own app, so you can jump straight to work windows.",
                "<strong>Separate Dock icon.</strong> The badge makes it obvious which Chrome you're in.",
                "<strong>Focus modes and window tools.</strong> Tools that act per app (Focus filters, window managers) can treat work Chrome on its own.",
            ]),
        ],
        "steps": [
            ("Install Mitosis", INSTALL_STEP),
            ("Clone Chrome", "Click <strong>New Clone</strong>, choose <strong>Google Chrome</strong>, and add a label like <strong>Work</strong>."),
            ("Sign in", "Open <em>Google Chrome (Work)</em> and sign in to your work Google account. It has its own profiles, history, and extensions."),
        ],
        "images": ["pick", "main"],
        "tips": [
            "Your normal Chrome keeps all its profiles; the copy starts fresh.",
            "Chrome updates itself; Mitosis rebuilds the copy afterwards and keeps its data.",
        ],
        "faqs": [
            ("Isn't a Chrome profile the same thing?", "For accounts, mostly yes. A copy adds what profiles can't: its own Dock icon, its own entry in Command-Tab, and windows that never mix with your other Chrome."),
            ("Does it work with other browsers?", "Chrome works as a full copy. Arc and Brave run in compatibility mode (separate data, but they share the original's Dock icon)."),
        ],
        "related": ["open-the-same-app-twice-on-mac", "keep-work-and-personal-apps-separate-on-mac", "two-slack-accounts-on-mac"],
    },
    {
        "slug": "keep-work-and-personal-apps-separate-on-mac", "category": "Getting started",
        "title": "How to keep work and personal apps separate on one Mac",
        "seo_title": "Keep Work and Personal Apps Separate on One Mac",
        "description": "Use one Mac for work and personal life without mixing accounts: separate copies of Slack, Chrome, Notion, and more, each with its own login and Dock icon.",
        "keywords": "separate work and personal apps mac, work and personal accounts one mac, work profile mac apps",
        "intro": [
            "One Mac for work and personal life is convenient until accounts start mixing: the wrong Slack pings you at dinner, a personal Notion page lands in the work workspace, or you post from the wrong account.",
            "Giving work its own copies of the apps you use keeps the two apart without a second computer or a second macOS user.",
        ],
        "answer": "Make a work copy of each app you use for both, like <em>Slack (Work)</em>, <em>Chrome (Work)</em>, and <em>Notion (Work)</em>, with Mitosis. Each copy has its own login, data, notifications, and Dock icon.",
        "sections": [
            ("A simple setup", [
                "<strong>Pick a badge for work.</strong> Use the same letter or color for every work copy, for example a blue <em>W</em>, so work apps are easy to spot in the Dock and in notifications.",
                "<strong>Keep work copies together.</strong> Put them side by side in the Dock, or in their own Dock folder from <code>~/Applications/Mitosis</code>.",
                "<strong>Quiet hours.</strong> Because each copy is its own app, macOS Focus can silence work copies after hours while personal apps keep working.",
            ]),
        ],
        "steps": [
            ("Install Mitosis", INSTALL_STEP),
            ("Make work copies", "For each app, click <strong>New Clone</strong>, choose the app, and add the label <strong>Work</strong>."),
            ("Sign in", "Sign in to your work accounts in the copies; your personal apps stay as they are."),
            ("Set up Focus", "In System Settings › Focus, choose which apps can notify you; the work copies appear as separate apps."),
        ],
        "images": ["main", "details"],
        "tips": [
            "Copies update automatically when the original apps update.",
            "Turn on <strong>Send sign-in links to the right copy</strong> for apps that sign in through the browser.",
        ],
        "faqs": [
            ("Is this better than a second macOS user account?", "A second user keeps everything apart, but you have to switch users to see it. Copies run side by side in one session, so you can glance at work Slack without logging out of anything."),
            ("Does my employer see my personal apps?", "Mitosis only keeps app data separate on your Mac. It doesn't change what your company's device management can see."),
        ],
        "related": ["mitosis-vs-a-second-macos-user", "two-slack-accounts-on-mac", "second-google-chrome-on-mac"],
    },
    {
        "slug": "test-your-app-with-two-accounts-on-mac", "category": "Getting started",
        "title": "How to test an app with two accounts at once on a Mac",
        "seo_title": "Test Your App With Two Accounts at Once on a Mac (Developers)",
        "description": "Developers: run two copies of your Electron or Chromium app (or a chat app you integrate with) side by side, each signed in as a different user.",
        "keywords": "test app two accounts mac, multiple instances electron app testing, run two users side by side mac",
        "intro": [
            "Testing anything with two users, like chat, sharing, or permissions, usually means a second machine, a VM, or constant signing out.",
            "A second copy of the app on the same Mac is faster: user A in one window, user B in the other.",
        ],
        "answer": "Clone the app with Mitosis (for example <em>Slack (User B)</em>) and sign in as the second user. Both copies run at once, each with its own data folder.",
        "sections": [
            ("Testing your own Electron app", [
                "If your app is built with Electron, Mitosis starts each copy with its own <code>--user-data-dir</code>, so storage, cookies, and logins stay apart.",
                "If your app keeps state outside its user-data folder (like Codex's <code>~/.codex</code>), the copies will share it. Reading such a folder from an environment variable lets each copy point somewhere else.",
            ]),
        ],
        "steps": [
            ("Install Mitosis", INSTALL_STEP),
            ("Make a copy", "Click <strong>New Clone</strong>, choose the app, and add a label like <strong>User B</strong>."),
            ("Sign in as user B", "Open the copy and sign in with the second test account."),
            ("Script it", "Prefer Terminal? <code>mitosis clone MyApp --label \"User B\"</code> does the same, and <code>mitosis delete \"MyApp (User B)\" --delete-data</code> cleans up."),
        ],
        "images": ["pick", "details"],
        "tips": [
            "<code>mitosis doctor MyApp</code> shows how an app will be cloned before you start.",
            "Copies are signed locally; your build's own signature stays as it is.",
        ],
        "faqs": [
            ("Can I automate this in CI?", "The mitosis command works in scripts on a Mac, but clones need a logged-in macOS session, so CI runners are hit and miss."),
            ("Does it modify my app?", "No. Mitosis copies the app and changes only the copy."),
        ],
        "related": ["open-the-same-app-twice-on-mac", "how-much-disk-space-a-clone-uses", "two-vs-code-instances-on-mac"],
    },
    {
        "slug": "mitosis-vs-a-second-macos-user", "category": "How it works",
        "title": "App copies vs a second macOS user account",
        "seo_title": "App Copies vs a Second macOS User Account: Which to Use",
        "description": "A second macOS user keeps everything apart but means switching users. App copies run side by side. Here's how to choose.",
        "keywords": "second user account mac vs app, fast user switching vs multiple app instances, separate accounts one mac",
        "intro": [
            "macOS has a built-in way to separate everything: a second user account. Each user has their own apps, files, and settings, and Fast User Switching moves between them.",
            "That's thorough, but it's all or nothing: you can't see work Slack while you're in your personal session.",
        ],
        "answer": "Use a second macOS user when you want everything apart, including files and passwords. Use app copies (Mitosis) when you want specific apps apart but side by side in one session.",
        "sections": [
            ("Side by side", [
                "<strong>Second macOS user:</strong> separate files, Keychain, Dock, and settings; switch users to see the other side; apps from both sides can't share a screen.",
                "<strong>App copies:</strong> one session, one set of files; only the copied apps keep their own login and data; both copies on screen at once.",
            ]),
            ("Mixing both", [
                "Nothing stops you from doing both: a separate macOS user for a strictly managed work setup, and app copies inside your own session for a second Slack or Discord.",
            ]),
        ],
        "steps": [],
        "images": ["main"],
        "tips": [],
        "faqs": [
            ("Do app copies share my files?", "Yes. Copies run in your normal session, so they can open the same files as the original. Only the app's own data (logins, settings, caches) is separate."),
            ("Do copies share passwords in Keychain?", "They share your Keychain like any app does, but each copy has its own app identity, so apps that store sign-ins per app keep them apart."),
        ],
        "related": ["keep-work-and-personal-apps-separate-on-mac", "open-the-same-app-twice-on-mac", "how-much-disk-space-a-clone-uses"],
    },
    {
        "slug": "clones-and-app-updates", "category": "How it works",
        "title": "What happens to clones when the original app updates",
        "seo_title": "What Happens to App Clones When the Original Mac App Updates",
        "description": "Mitosis rebuilds clones automatically when the original app updates, keeping logins and data. Here's how it works, and what happens if a clone is open.",
        "keywords": "app clone update mac, clone app updates automatically, keep cloned app up to date mac",
        "intro": [
            "Apps update all the time. A copy made last month still contains last month's version, so it needs updating too.",
            "Mitosis does that for you.",
        ],
        "answer": "When the original app updates, Mitosis rebuilds its clones from the new version in the background, keeping each clone's login and data. A clone that's open is rebuilt after you quit it.",
        "sections": [
            ("How it works", [
                "<strong>While Mitosis is open:</strong> it notices the new version and rebuilds outdated clones that aren't running.",
                "<strong>While Mitosis is closed:</strong> a small macOS background job watches the original apps (it uses no CPU while waiting) and runs the same update.",
                "<strong>Safely:</strong> the new clone is built next to the old one and swapped in only when it's complete. If an app is still installing its update, Mitosis waits and tries again later.",
                "<strong>Your data stays:</strong> logins, settings, and caches live outside the clone, so rebuilding it doesn't touch them.",
            ]),
            ("Prefer to update by hand?", [
                "Turn off <strong>Settings › General › Update clones automatically</strong>. Clones then show <em>Update available</em>, and <strong>Refresh</strong> rebuilds one. A manual refresh opens the clone once to check that it starts, and puts the previous version back if it doesn't.",
            ]),
        ],
        "steps": [],
        "images": ["main"],
        "tips": [],
        "faqs": [
            ("Do clones update themselves?", "No. The original app updates as usual; Mitosis then rebuilds its clones from it."),
            ("Will I have to sign in again after an update?", "No. Your login lives in the clone's data folder, which an update doesn't touch."),
        ],
        "related": ["how-much-disk-space-a-clone-uses", "a-clone-wont-open", "open-the-same-app-twice-on-mac"],
    },
    {
        "slug": "sign-in-links-and-clones", "category": "How it works",
        "title": "Why a clone gets signed in to the wrong copy, and the fix",
        "seo_title": "Sign-In Links and App Clones on Mac: Why the Wrong Copy Gets Signed In",
        "description": "Apps that sign in through the browser send a link back that macOS gives to the original app. Here's why that signs in the wrong copy, and how Mitosis routes it.",
        "keywords": "sign in opens wrong app mac, url scheme multiple instances mac, oauth login second app instance",
        "intro": [
            "Many apps sign you in through your browser. When you're done, the website sends a link back, such as <code>slack://…</code>, and macOS opens the app that owns that kind of link.",
            "With two copies of the app, macOS picks the original. So signing in from the copy can sign in the original instead.",
        ],
        "answer": "Turn on <strong>Send sign-in links to the right copy</strong> on the clone's page in Mitosis. Mitosis then receives those links and passes each one to the copy you signed in from, asking when several copies are open.",
        "sections": [
            ("How Mitosis routes the link", [
                "When you turn it on, Mitosis sets up a tiny helper, <em>Mitosis Link Router</em>, as the handler for that app's sign-in links. When a link arrives, the helper sends it to the one copy that's open, or shows a small chooser if both are, with the most recent one preselected.",
                "Turning it off, or deleting the app's last clone, gives the links back to the original app.",
            ]),
        ],
        "steps": [
            ("Open the clone's page", "In Mitosis, select the clone in the sidebar."),
            ("Turn it on", "Under <strong>Sign-in links</strong>, switch on <strong>Send sign-in links to the right copy</strong>."),
            ("Sign in again", "Start the sign-in from the copy; the link comes back to it."),
        ],
        "images": ["links"],
        "tips": [
            "Only full clones can receive their own links; compatibility-mode clones run as the original app.",
            "From Terminal: <code>mitosis links on Slack</code>, <code>mitosis links off Slack</code>, and <code>mitosis links</code> to see what's routed.",
        ],
        "faqs": [
            ("Does Mitosis see my sign-in details?", "The link passes through the helper on your Mac straight to the app. Nothing is stored or sent anywhere."),
            ("Which apps need this?", "Apps that finish signing in through your browser, such as Slack, Claude, Figma, and many Electron apps. If the clone signs in inside its own window, you don't need it."),
        ],
        "related": ["two-slack-accounts-on-mac", "two-claude-accounts-on-mac", "a-clone-wont-open"],
    },
    {
        "slug": "how-much-disk-space-a-clone-uses", "category": "How it works",
        "title": "How much disk space does an app clone use?",
        "seo_title": "How Much Disk Space Does an App Clone Use on Mac? (APFS Clones)",
        "description": "A Mitosis clone usually adds a couple of megabytes, because it shares the original app's files on disk through APFS. Here's how, and when it uses more.",
        "keywords": "app clone disk space mac, apfs clone size, duplicate app disk usage mac",
        "intro": [
            "Copying a big app like Slack or VS Code normally doubles its size, often hundreds of megabytes.",
            "Mitosis clones are much smaller.",
        ],
        "answer": "Usually a couple of megabytes. Mitosis makes an APFS clone, which shares the original app's files on disk; only the few files it changes (the name, badge icon, and launcher) take new space. The clone's data, like logins and caches, grows as you use it.",
        "sections": [
            ("When a clone uses more", [
                "<strong>The original is on another drive.</strong> APFS can only share files on the same drive, so a clone of an app on an external disk is a full copy. Mitosis tells you the size before it copies.",
                "<strong>Data grows with use.</strong> Messages, caches, and downloaded updates live in the clone's data folder. <strong>Clean Caches</strong> on the clone's page frees space without signing you out.",
            ]),
            ("Seeing the numbers", [
                "Each clone's page shows <em>Extra disk</em> (what the clone adds), <em>Data</em>, and, while it runs, memory and CPU. In Terminal, <code>mitosis stats \"Slack (Work)\"</code> shows the same.",
            ]),
        ],
        "steps": [],
        "images": ["main"],
        "tips": [],
        "faqs": [
            ("Does Finder show the real size?", "Finder shows the full size of the app, because it can't tell which files are shared. The clone's page in Mitosis shows what it really adds."),
            ("Does a clone use more memory?", "A running clone uses about as much memory as the app normally does; a clone that isn't open uses none."),
        ],
        "related": ["clones-and-app-updates", "open-the-same-app-twice-on-mac", "uninstall-mitosis"],
    },
    {
        "slug": "a-clone-wont-open", "category": "Help",
        "title": "A clone won't open: what to try",
        "seo_title": "Mac App Clone Won't Open? How to Fix It (Mitosis Help)",
        "description": "If a Mitosis clone won't open, try Refresh, then compatibility mode. Here's what each step does and how to report a problem.",
        "keywords": "app clone not opening mac, mitosis clone won't open, cloned app crashes mac",
        "intro": [
            "Mitosis checks that every new clone starts before it says it's ready, so a clone that won't open usually means something changed since: the original app updated, moved, or macOS blocked something.",
        ],
        "answer": "Choose <strong>Refresh</strong> on the clone. If it still won't open, delete it and create it again with <strong>New Clone › Advanced › Mode › Compatibility mode</strong>. If that fails too, use <strong>Help › Report a Problem</strong>.",
        "sections": [
            ("Common causes", [
                "<strong>Original missing.</strong> The clone shows <em>Original missing</em> when the app moved. Choose <strong>Find App…</strong> and pick it in its new place.",
                "<strong>macOS blocked the clone.</strong> Clones are signed on your Mac. <strong>Refresh</strong> signs the clone again.",
                "<strong>The app refuses a new identity.</strong> A few apps check that they're the original. Compatibility mode runs the original app with the copy's own data instead.",
            ]),
        ],
        "steps": [
            ("Refresh", "Select the clone and choose <strong>Refresh</strong> (or <strong>⋯ › Refresh</strong>). Logins and data stay."),
            ("Try compatibility mode", "Delete the clone (keep its data if you like), then create it again with <strong>Advanced › Mode › Compatibility mode</strong>."),
            ("Report it", "Choose <strong>Help › Report a Problem</strong>. Mitosis fills in the details without your name or files."),
        ],
        "images": ["main"],
        "tips": [],
        "faqs": [
            ("Is my data lost if I delete the clone?", "No. Choose Move to Trash (not with Data) and the clone's data folder stays in ~/Library/Mitosis/Data."),
        ],
        "related": ["clones-and-app-updates", "macos-asks-for-permissions-again", "uninstall-mitosis"],
    },
    {
        "slug": "macos-asks-for-permissions-again", "category": "Help",
        "title": "Why macOS asks a clone for permissions again",
        "seo_title": "Why macOS Asks a Cloned App for Permissions Again",
        "description": "macOS treats each clone as a new app, so it asks again for notifications, files, the camera, and microphone. Here's why, and where to manage it.",
        "keywords": "cloned app permissions mac, mac asks permission again copied app, privacy permissions clone",
        "intro": [
            "The first time a clone wants to send notifications, open your Documents folder, or use the camera, macOS asks, even if you already allowed the original app.",
        ],
        "answer": "That's expected: each clone has its own app identity, so macOS keeps its permissions separate from the original's. Allow what the clone needs, and review it anytime in <strong>System Settings › Privacy & Security</strong>.",
        "sections": [
            ("Why it's a good thing", [
                "Separate permissions mean you can let your work copy use the camera for meetings while your personal copy can't, or silence notifications from one copy only.",
            ]),
        ],
        "steps": [],
        "images": ["main"],
        "tips": [
            "Notifications for each copy are set in <strong>System Settings › Notifications</strong>, where each copy appears under its own name.",
        ],
        "faqs": [
            ("Do I have to allow everything again after an update?", "No. Updates keep the clone's identity, so its permissions stay."),
        ],
        "related": ["a-clone-wont-open", "keep-work-and-personal-apps-separate-on-mac", "clones-and-app-updates"],
    },
    {
        "slug": "uninstall-mitosis", "category": "Help",
        "title": "How to remove clones and uninstall Mitosis",
        "seo_title": "How to Uninstall Mitosis and Remove App Clones on Mac",
        "description": "Delete clones (with or without their data), turn off the background updater, and remove Mitosis and its command completely.",
        "keywords": "uninstall mitosis mac, remove app clone mac, delete cloned app data",
        "intro": [
            "Mitosis keeps everything in a few known places, and deleting always goes to the Trash, so removing it is simple and reversible.",
        ],
        "answer": "Delete each clone from Mitosis (choose whether its data goes too), turn off <strong>Update clones automatically</strong>, quit Mitosis, move <em>Mitosis.app</em> to the Trash, and run <code>rm -f ~/.local/bin/mitosis</code>.",
        "sections": [
            ("Where things live", [
                "<strong>Clones:</strong> <code>~/Applications/Mitosis</code>",
                "<strong>Clone data</strong> (logins, settings, caches): <code>~/Library/Mitosis/Data</code>",
                "<strong>Mitosis settings:</strong> <code>~/Library/Application Support/Mitosis</code>",
                "<strong>Background updater:</strong> <code>~/Library/LaunchAgents/com.mitosis-mac.autorefresh.plist</code>, removed when you turn off automatic updates.",
            ]),
        ],
        "steps": [
            ("Delete clones", "Select a clone and choose <strong>Delete…</strong>: <em>Move to Trash</em> keeps its data, <em>Move to Trash with Data</em> removes both."),
            ("Turn off the updater", "In Settings › General, turn off <strong>Update clones automatically</strong>."),
            ("Remove Mitosis", "Quit Mitosis, move it to the Trash, and run <code>rm -f ~/.local/bin/mitosis</code>. With Homebrew: <code>brew uninstall --cask mitosis</code>."),
        ],
        "images": [],
        "tips": [],
        "faqs": [
            ("Does uninstalling Mitosis delete my clones?", "No. Clones are normal apps in ~/Applications/Mitosis and keep working; delete them first if you don't want them."),
        ],
        "related": ["a-clone-wont-open", "how-much-disk-space-a-clone-uses", "open-the-same-app-twice-on-mac"],
    },
]


PARALL = {
    "slug": "parall-alternative",
    "title": "A free Parall alternative for running apps twice on a Mac",
    "seo_title": "Free Parall Alternative for Mac: Run Multiple Copies of Apps (Mitosis)",
    "description": "Looking for a free alternative to Parall? Mitosis runs separate copies of your Mac apps, each with its own login, data, and Dock icon, and it's free and source-available.",
    "keywords": "parall alternative, free parall alternative, parall mac free, app cloner mac free, multiple instances mac",
    "intro": [
        "Parall is a paid Mac app for opening apps as separate instances. If you want the same idea for free, with the source code open to read, Mitosis does the job.",
        "Here's what Mitosis does, so you can decide what fits.",
    ],
    "answer": "Mitosis is a free, source-available app cloner for macOS. It makes copies of your apps, such as <em>Slack (Work)</em>, each with its own login, data, Dock icon, and notifications, and it keeps them updated when the original app updates.",
    "sections": [
        ("What Mitosis does", [
            "<strong>Real copies.</strong> A clone gets its own app identity, so macOS gives it its own Dock icon, notifications, and permissions. Apps that can't take a new identity run in a compatibility mode instead.",
            "<strong>Checked before it says done.</strong> Every new copy is opened once to make sure it starts; one that doesn't is removed.",
            "<strong>Updates on its own.</strong> When the original app updates, Mitosis rebuilds its copies in the background, even while Mitosis is closed.",
            "<strong>Sign-in links go to the right copy.</strong> When you sign in through the browser, the link back goes to the copy you signed in from.",
            "<strong>Stats per copy.</strong> See the extra disk, data, memory, and CPU each copy uses, and clean its caches.",
            "<strong>Free and private.</strong> No account, no analytics, source code on GitHub, and a <code>mitosis</code> command for Terminal.",
        ]),
        ("Good to know", [
            "Mitosis needs macOS 15 or later on Apple silicon. It isn't notarized (it's built without a paid Apple Developer account), so it installs with a one-line command or Homebrew instead of a downloaded file.",
            "Parall is a trademark of its developer. Mitosis isn't affiliated with Parall; this page only describes Mitosis.",
        ]),
    ],
    "steps": [
        ("Install Mitosis", INSTALL_STEP),
        ("Make a clone", "Click <strong>New Clone</strong>, pick an app, and add a label like <strong>Work</strong>."),
        ("Use both", "Open the clone, sign in with your other account, and keep it in the Dock."),
    ],
    "images": ["main", "pick"],
    "tips": [],
    "faqs": [
        ("Is Mitosis free?", "Yes. Mitosis is free for any use, including at work. Its source code is available under the PolyForm Noncommercial license."),
        ("Which apps work?", "Most Electron and Chromium apps work well, including Slack, Discord, Signal, VS Code, Notion, Claude, and Codex. Apple's own apps aren't supported, and sandboxed apps that need iCloud (like WhatsApp) aren't supported yet."),
        ("Can I move my Parall setup over?", "Make a Mitosis clone for each app and sign in again inside it. Your original apps aren't changed."),
    ],
    "related": ["open-the-same-app-twice-on-mac", "two-slack-accounts-on-mac", "two-signal-accounts-on-mac"],
    "category": "Getting started",
}

# ---------------------------------------------------------------------------------------------------------------
# Templates

def head(title, description, path, keywords="", extra_ld=(), og_type="article"):
    url = SITE + path
    ld = "".join(f'\n  <script type="application/ld+json">{json.dumps(x, ensure_ascii=False)}</script>' for x in extra_ld)
    kw = f'\n  <meta name="keywords" content="{html.escape(keywords)}">' if keywords else ""
    return f"""<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="color-scheme" content="light dark">
  <meta name="theme-color" content="#ffffff" media="(prefers-color-scheme: light)">
  <meta name="theme-color" content="#142337" media="(prefers-color-scheme: dark)">
  <title>{html.escape(title)}</title>
  <meta name="description" content="{html.escape(description)}">{kw}
  <link rel="canonical" href="{url}">
  <meta property="og:type" content="{og_type}">
  <meta property="og:site_name" content="Mitosis">
  <meta property="og:title" content="{html.escape(title)}">
  <meta property="og:description" content="{html.escape(description)}">
  <meta property="og:url" content="{url}">
  <meta property="og:image" content="{SITE}/assets/social-preview.png">
  <meta property="og:image:width" content="1280">
  <meta property="og:image:height" content="640">
  <meta name="twitter:card" content="summary_large_image">
  <link rel="icon" type="image/png" sizes="32x32" href="/assets/favicon-32.png">
  <link rel="apple-touch-icon" href="/assets/apple-touch-icon.png">
  <link rel="manifest" href="/site.webmanifest">
  <link rel="stylesheet" href="/styles.css">{ld}
</head>"""


HEADER = """<body>
  <a class="skip-link" href="#main">Skip to content</a>
  <header class="site-header">
    <div class="wrap header-inner">
      <a class="brand" href="/" aria-label="Mitosis home">
        <img src="/assets/favicon.png" width="36" height="36" alt="">
        <span>Mitosis</span>
      </a>
      <nav class="site-nav" aria-label="Main">
        <a href="/guides/">Guides</a>
        <a href="/#install">Install</a>
        <a href="https://github.com/deadhearth01/Mitosis">GitHub</a>
      </nav>
    </div>
  </header>"""

FOOTER = """  <footer class="site-footer">
    <div class="wrap footer-inner">
      <p>A <a href="https://theavni.studio/labs">The Avni Studio Labs</a> project</p>
      <nav aria-label="Footer"><a href="/guides/">Guides</a><a href="/parall-alternative/">Free Parall alternative</a><a href="https://github.com/deadhearth01/Mitosis">GitHub</a><a href="https://github.com/deadhearth01/Mitosis/blob/main/LICENSE">License</a></nav>
    </div>
  </footer>
</body>
</html>
"""


def figure(key):
    src, w, h, alt = IMG[key]
    return f'<figure class="article-figure"><img src="{src}" width="{w}" height="{h}" loading="lazy" decoding="async" alt="{html.escape(alt)}"></figure>'


def install_box():
    return f"""<aside class="install-box" aria-labelledby="install-box-title">
        <h2 id="install-box-title">Get Mitosis</h2>
        <p>Free for macOS 15 or later on Apple silicon. Paste in Terminal:</p>
        <code class="command">{html.escape(INSTALL)}</code>
        <p class="install-box-alt">Or with Homebrew: <code>{BREW}</code></p>
        <a class="button primary" href="/#install">All install options</a>
      </aside>"""


def strip_tags(s):
    return re.sub(r"<[^>]+>", "", s)


def page_for(g, path, crumb_name, crumb_parent=("Guides", "/guides/")):
    url = SITE + path
    breadcrumbs = [("Mitosis", "/")] + ([crumb_parent] if crumb_parent else []) + [(crumb_name, path)]
    ld = [
        {"@context": "https://schema.org", "@type": "TechArticle", "headline": g["title"], "description": g["description"],
         "url": url, "datePublished": "2026-10-09", "dateModified": TODAY, "inLanguage": "en",
         "author": ORG, "publisher": {**ORG, "logo": {"@type": "ImageObject", "url": SITE + "/assets/icon-512.png"}},
         "image": SITE + (IMG[g["images"][0]][0] if g["images"] else "/assets/social-preview.png"), "mainEntityOfPage": url,
         "about": {"@type": "SoftwareApplication", "name": "Mitosis", "operatingSystem": "macOS", "applicationCategory": "UtilitiesApplication"}},
        {"@context": "https://schema.org", "@type": "BreadcrumbList", "itemListElement": [
            {"@type": "ListItem", "position": i + 1, "name": n, "item": SITE + p} for i, (n, p) in enumerate(breadcrumbs)]},
    ]
    if g["faqs"]:
        ld.append({"@context": "https://schema.org", "@type": "FAQPage", "mainEntity": [
            {"@type": "Question", "name": q, "acceptedAnswer": {"@type": "Answer", "text": a}} for q, a in g["faqs"]]})

    crumbs_html = " <span aria-hidden=\"true\">›</span> ".join(
        f'<a href="{p}">{html.escape(n)}</a>' if p != path else f'<span aria-current="page">{html.escape(n)}</span>' for n, p in breadcrumbs)
    intro = "\n".join(f"        <p class=\"lede\">{p}</p>" if i == 0 else f"        <p>{p}</p>" for i, p in enumerate(g["intro"]))
    sections = ""
    for heading, paras in g["sections"]:
        body = "\n".join(f"        <p>{p}</p>" for p in paras)
        sections += f"\n        <h2>{heading}</h2>\n{body}"
    steps = "\n".join(f"          <li><h3>{html.escape(t)}</h3><p>{d}</p></li>" for t, d in g["steps"])
    steps_html = f"\n        <h2>Step by step</h2>\n        <ol class=\"howto\">\n{steps}\n        </ol>" if g["steps"] else ""
    figs = "\n        ".join(figure(k) for k in g["images"])
    tips = ""
    if g["tips"]:
        items = "\n".join(f"          <li>{t}</li>" for t in g["tips"])
        tips = f"\n        <h2>Good to know</h2>\n        <ul class=\"tips\">\n{items}\n        </ul>"
    faqs = ""
    if g["faqs"]:
        items = "\n".join(f"          <div class=\"faq-item\"><h3>{html.escape(q)}</h3><p>{a}</p></div>" for q, a in g["faqs"])
        faqs = f"\n        <h2>Questions</h2>\n        <div class=\"faq-list\">\n{items}\n        </div>"
    related = "\n".join(f'          <li><a href="{link_of(s)}">{html.escape(title_of(s))}</a></li>' for s in g["related"])

    return f"""{head(g['seo_title'], g['description'], path, g['keywords'], ld)}
{HEADER}

  <main id="main" class="article-page">
    <article class="wrap article">
      <nav class="breadcrumbs" aria-label="Breadcrumb">{crumbs_html}</nav>
      <h1>{html.escape(g['title'])}</h1>
{intro}
        <div class="answer"><p class="answer-label">Quick answer</p><p>{g['answer']}</p></div>{sections}{steps_html}
        {figs}{tips}{faqs}
      {install_box()}
      <section class="related" aria-labelledby="related-title">
        <h2 id="related-title">Related guides</h2>
        <ul>
{related}
        </ul>
      </section>
    </article>
  </main>

{FOOTER}"""


def link_of(slug):
    return "/parall-alternative/" if slug == PARALL["slug"] else f"/guides/{slug}/"


def title_of(slug):
    return next(g["title"] for g in GUIDES + [PARALL] if g["slug"] == slug)


def guides_index():
    path = "/guides/"
    everything = GUIDES + [PARALL]
    blocks = ""
    for cat in CATEGORIES:
        items = "\n".join(
            f"""          <li class="guide-card"><a href="{link_of(g['slug'])}"><h3>{html.escape(g['title'])}</h3><p>{html.escape(g['description'])}</p></a></li>"""
            for g in everything if g.get("category") == cat)
        anchor = cat.lower().replace(" ", "-")
        blocks += f"""
      <section class="guide-category" aria-labelledby="{anchor}">
        <h2 id="{anchor}">{cat}</h2>
        <ul class="guide-grid">
{items}
        </ul>
      </section>"""
    jump = " · ".join(f'<a href="#{c.lower().replace(" ", "-")}">{c}</a>' for c in CATEGORIES)
    ld = [{"@context": "https://schema.org", "@type": "CollectionPage", "name": "Mitosis guides", "url": SITE + path,
           "hasPart": [{"@type": "TechArticle", "headline": g["title"], "url": SITE + link_of(g["slug"])} for g in everything]},
          {"@context": "https://schema.org", "@type": "BreadcrumbList", "itemListElement": [
              {"@type": "ListItem", "position": 1, "name": "Mitosis", "item": SITE + "/"},
              {"@type": "ListItem", "position": 2, "name": "Guides", "item": SITE + path}]}]
    return f"""{head("Guides: Two Accounts in Mac Apps · Mitosis",
                  "Guides for running two copies of Mac apps, each with its own account: Slack, Signal, Discord, Claude, Codex, Notion, Cursor, VS Code, Figma, Chrome, and more, plus how clones work.",
                  path, "run two copies of an app mac, multiple accounts mac apps, mac app cloner guides", ld, og_type="website")}
{HEADER}

  <main id="main" class="article-page">
    <div class="wrap">
      <nav class="breadcrumbs" aria-label="Breadcrumb"><a href="/">Mitosis</a> <span aria-hidden="true">›</span> <span aria-current="page">Guides</span></nav>
      <h1>Guides</h1>
      <p class="lede guides-lede">How to run two copies of the same Mac app, each with its own login and data, and how clones work.</p>
      <p class="guide-jump">{jump}</p>{blocks}
    </div>
  </main>

{FOOTER}"""


# ---------------------------------------------------------------------------------------------------------------
# Home page structured data (kept in index.html between markers so the hand-written page stays editable)

HOME_FAQS = [
    ("Which apps work?", "Slack, Discord, Signal, VS Code, Claude, Codex, Notion, and many Electron or Chromium apps work well. Some apps have limits; Apple's own apps aren't supported. See compatibility details."),
    ("Where does clone data live?", "Clone data lives in ~/Library/Mitosis/Data/. The clone apps live in ~/Applications/Mitosis/."),
    ("What happens when the original app updates?", "Mitosis updates its clones on its own, in the background. Your logins and data stay in place."),
    ("Does Mitosis change the original app?", "No. The original app stays as it is."),
    ("How do I uninstall a clone?", "Move it to the Trash. Its data stays unless you choose to remove it."),
    ("Can I sign in to two accounts of the same app?", "Yes. Each clone is a separate app with its own login. For apps that sign in through your browser, turn on “Send sign-in links to the right copy” so the link reaches the copy you signed in from."),
    ("Is Mitosis a free alternative to Parall?", "Yes. Mitosis is free and source-available, and runs separate copies of your Mac apps with their own logins, data, and Dock icons. See what it does."),
    ("Can I use it at work?", "Yes. Mitosis is free for any use, including work. Its source code can't be used in commercial or paid products."),
]


def home_ld():
    return [
        {"@context": "https://schema.org", "@type": "SoftwareApplication", "name": "Mitosis",
         "description": "Free macOS app that runs separate copies of your Mac apps, each with its own login, data, and Dock icon.",
         "url": SITE + "/", "applicationCategory": "UtilitiesApplication", "operatingSystem": "macOS 15 or later (Apple silicon)",
         "softwareVersion": VERSION, "downloadUrl": REPO + "/releases/latest", "installUrl": SITE + "/#install",
         "image": SITE + "/assets/icon-512.png", "screenshot": SITE + "/assets/screen-light.webp",
         "offers": {"@type": "Offer", "price": "0", "priceCurrency": "USD"},
         "author": ORG, "publisher": ORG, "license": REPO + "/blob/main/LICENSE", "codeRepository": REPO},
        {"@context": "https://schema.org", "@type": "WebSite", "name": "Mitosis", "url": SITE + "/", "publisher": ORG},
        {"@context": "https://schema.org", "@type": "FAQPage", "mainEntity": [
            {"@type": "Question", "name": q, "acceptedAnswer": {"@type": "Answer", "text": a}} for q, a in HOME_FAQS]},
    ]


def update_home():
    index = PUBLIC / "index.html"
    text = index.read_text()
    block = "  <!-- ld:start -->" + "".join(
        f'\n  <script type="application/ld+json">{json.dumps(x, ensure_ascii=False)}</script>' for x in home_ld()) + "\n  <!-- ld:end -->"
    if "<!-- ld:start -->" in text:
        text = re.sub(r"  <!-- ld:start -->.*?<!-- ld:end -->", lambda _: block, text, flags=re.S)
    else:
        text = text.replace("</head>", block + "\n</head>")
    index.write_text(text)


def sitemap(paths):
    urls = "\n".join(f"  <url><loc>{SITE}{p}</loc><lastmod>{TODAY}</lastmod></url>" for p in paths)
    return f'<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n{urls}\n</urlset>\n'


def llms_txt():
    lines = [
        "# Mitosis",
        "",
        "> Free, source-available macOS app that runs separate copies of your Mac apps (for example Slack (Work) next to Slack), each with its own login, data, Dock icon, and notifications. macOS 15+ on Apple silicon.",
        "",
        f"Current version: {VERSION}. Made by The Avni Studio (https://theavni.studio). Source: {REPO}",
        "",
        "Key facts:",
        "- Clones share the original app's files on disk (APFS), so a clone usually adds a few megabytes.",
        "- Clones update automatically when the original app updates; logins and data are kept.",
        "- Sign-in links (custom URL schemes like slack://) can be routed to the copy that started the sign-in.",
        "- Codex clones get their own CODEX_HOME, so each signs in to its own account.",
        "- Works with most Electron/Chromium apps (Slack, Discord, Signal, VS Code, Notion, Claude, Codex). Not Apple's own apps; sandboxed iCloud apps like WhatsApp aren't supported yet.",
        f"- Install: `{INSTALL}` or `{BREW}`",
        "- Free for any use, including work (PolyForm Noncommercial 1.0.0 plus a permission to use unmodified releases for any purpose).",
        "",
        "## Pages",
        f"- [Home]({SITE}/): what Mitosis does and how to install it",
        f"- [Guides]({SITE}/guides/): step-by-step guides",
    ]
    lines += [f"- [{g['title']}]({SITE}{link_of(g['slug'])}): {g['description']}" for g in GUIDES + [PARALL]]
    lines += ["", "## Optional", f"- [README and changelog]({REPO})", ""]
    return "\n".join(lines)


def manifest():
    return json.dumps({
        "name": "Mitosis", "short_name": "Mitosis", "start_url": "/", "display": "browser",
        "background_color": "#ffffff", "theme_color": "#1450A8",
        "icons": [
            {"src": "/assets/icon-192.png", "sizes": "192x192", "type": "image/png"},
            {"src": "/assets/icon-512.png", "sizes": "512x512", "type": "image/png"},
            {"src": "/assets/icon-maskable-512.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable"},
        ],
    }, indent=2) + "\n"


def main():
    paths = ["/", "/guides/"]
    for g in GUIDES:
        path = f"/guides/{g['slug']}/"
        out = PUBLIC / "guides" / g["slug"] / "index.html"
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(page_for(g, path, g["title"]))
        paths.append(path)
    out = PUBLIC / "parall-alternative" / "index.html"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(page_for(PARALL, "/parall-alternative/", "Free Parall alternative", crumb_parent=None))
    paths.append("/parall-alternative/")
    (PUBLIC / "guides" / "index.html").write_text(guides_index())
    (PUBLIC / "sitemap.xml").write_text(sitemap(paths))
    (PUBLIC / "robots.txt").write_text(f"User-agent: *\nAllow: /\n\nSitemap: {SITE}/sitemap.xml\n")
    (PUBLIC / "llms.txt").write_text(llms_txt())
    (PUBLIC / "site.webmanifest").write_text(manifest())
    update_home()
    print(f"Built {len(paths)} pages (version {VERSION}).")


if __name__ == "__main__":
    main()
