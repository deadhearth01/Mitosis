import SwiftUI

/// The built-in guide (Help ▸ Mitosis Help, ⌘?). Plain, short, and true to how Mitosis actually behaves.
struct HelpView: View {
    @State private var topic: HelpTopic? = .start

    var body: some View {
        NavigationSplitView {
            List(HelpTopic.allCases, selection: $topic) { t in
                Label(t.title, systemImage: t.symbol).tag(t)
            }
            .navigationSplitViewColumnWidth(min: 200, ideal: 220, max: 280)
        } detail: {
            if let topic {
                HelpPage(topic: topic)
            }
        }
        .frame(minWidth: 700, minHeight: 500)
    }
}

enum HelpTopic: String, CaseIterable, Identifiable {
    case start, apps, permissions, links, updates, stats, deleting, trouble, cli
    var id: String { rawValue }

    var title: String {
        switch self {
        case .start: return "Getting started"
        case .apps: return "Which apps work"
        case .permissions: return "Permissions"
        case .links: return "Sign-in links"
        case .updates: return "App updates"
        case .stats: return "Stats and caches"
        case .deleting: return "Deleting clones"
        case .trouble: return "Troubleshooting"
        case .cli: return "Command line"
        }
    }

    var symbol: String {
        switch self {
        case .start: return "sparkles"
        case .apps: return "checkmark.seal"
        case .permissions: return "lock.shield"
        case .links: return "link"
        case .updates: return "arrow.triangle.2.circlepath"
        case .stats: return "chart.bar"
        case .deleting: return "trash"
        case .trouble: return "wrench.and.screwdriver"
        case .cli: return "terminal"
        }
    }

    var pose: Mascot {
        switch self {
        case .start: return .guide
        case .apps: return .search
        case .permissions: return .key
        case .links: return .point
        case .updates: return .refresh
        case .stats: return .stats
        case .deleting: return .goodbye
        case .trouble: return .oops
        case .cli: return .thinking
        }
    }

    var paragraphs: [String] {
        switch self {
        case .start:
            return [
                "Mitosis makes separate copies of your Mac apps. Each copy, called a clone, has its own login, settings, and data, so you can use Slack (Work) and Slack (Personal) side by side.",
                "To make one, click New Clone (⌘N), choose an app, and add a label like Work. Mitosis creates the clone, opens it once to check that it starts, and then it's ready.",
                "Clones live in ~/Applications/Mitosis and their data in ~/Library/Mitosis/Data. Keep a clone in the Dock like any other app. The original app is never changed.",
            ]
        case .apps:
            return [
                "**Works great:** most apps built with Electron or Chromium, like Slack, Discord, Signal, VS Code, Notion, and Claude. Clones get their own login, data, notifications, and Dock icon.",
                "**Works with limits:** some apps use features tied to their developer. Mitosis clones them in compatibility mode: separate login and data, but they use the original app's Dock icon and notifications. Mac App Store apps may ask you to sign in again.",
                "**Not supported:** Apple's own apps, and apps that need iCloud or shared app data, like WhatsApp. A light mode for these is planned.",
                "The New Clone picker shows a label for every app before you start.",
            ]
        case .permissions:
            return [
                "macOS treats each clone as a new app. The first time a clone needs notifications, your files, the camera, or the microphone, macOS asks again.",
                "That keeps each copy's access separate. You can review it anytime in System Settings ▸ Privacy & Security.",
            ]
        case .links:
            return [
                "Many apps sign you in through your browser. When you're done, the website sends a link back to the app, and macOS normally gives it to the original app, so the wrong copy gets signed in.",
                "Turn on **Send sign-in links to the right copy** on a clone's page. Mitosis then receives those links and passes each one to the copy you signed in from. If several copies are open, it asks which one to use.",
                "Turning it off, or deleting the app's last clone, gives the links back to the original app. Compatibility-mode clones can't receive their own links.",
            ]
        case .updates:
            return [
                "When an original app updates, Mitosis rebuilds its clones automatically in the background, even while Mitosis is closed. Logins and data stay.",
                "A clone that's open when its app updates is rebuilt after you quit it. If an app is still installing its update, Mitosis waits and tries again a little later.",
                "To update by hand instead, turn off Settings ▸ General ▸ Update clones automatically. Clones then show Update available, and Refresh rebuilds one. If the new version doesn't start, Mitosis puts the previous one back.",
            ]
        case .stats:
            return [
                "Select a clone and show the inspector (⌘I) to see the space it takes and, while it's running, its memory and CPU use.",
                "Extra disk is what the clone adds. A clone shares the original's files, so it usually takes only a few megabytes. If the original app is on another drive, the clone is a full copy instead.",
                "Clean Caches deletes temporary files the app can download again, like update downloads. Logins and settings stay.",
            ]
        case .deleting:
            return [
                "Choose Delete… on a clone. Move to Trash removes the clone and keeps its data folder. Move to Trash with Data removes both.",
                "Nothing is erased right away. Everything goes to the Trash, so you can still put it back.",
            ]
        case .trouble:
            return [
                "**A clone won't open:** choose Refresh. If it still fails, delete it and create it again with New Clone ▸ Advanced ▸ Mode ▸ Compatibility mode.",
                "**macOS blocks a clone:** clones are signed on your Mac. Refresh the clone so Mitosis signs it again.",
                "**A clone says Original missing:** the original app moved. Choose Find App… and pick it in its new place.",
                "**Still stuck?** Choose Help ▸ Report a Problem. Error reports from Mitosis leave out your name and files.",
            ]
        case .cli:
            return [
                "Mitosis includes the `mitosis` command. Install it from Settings ▸ General, then try:",
                "`mitosis list` shows your clones. `mitosis clone Slack --label Work` makes Slack (Work). `mitosis refresh --all` refreshes clones after updates. `mitosis help` lists everything.",
            ]
        }
    }
}

struct HelpPage: View {
    let topic: HelpTopic
    @Environment(\.openURL) private var openURL

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Brand.Space.m) {
                HStack(spacing: Brand.Space.m) {
                    MascotView(pose: topic.pose, size: 72)
                    Text(topic.title)
                        .font(.largeTitle.weight(.semibold))
                }
                ForEach(topic.paragraphs, id: \.self) { text in
                    Text(LocalizedStringKey(text))
                        .font(.body)
                        .lineSpacing(3)
                        .fixedSize(horizontal: false, vertical: true)
                        .textSelection(.enabled)
                }
                if topic == .permissions {
                    Button("Open Privacy & Security") { openURL(SystemLinks.privacy) }
                }
                if topic == .trouble {
                    Button("Report a Problem…") { openURL(SystemLinks.newIssue) }
                }
            }
            .padding(Brand.Space.xl)
            .frame(maxWidth: 640, alignment: .leading)
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .navigationTitle(topic.title)
    }
}
