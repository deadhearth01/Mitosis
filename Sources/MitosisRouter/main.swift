import AppKit
import MitosisCore
import SwiftUI

// Mitosis Link Router: receives sign-in links (for example slack://…) for apps that have clones, and delivers each
// one to the copy that started the sign-in. Asks when that isn't clear.

@MainActor
final class Router: NSObject, NSApplicationDelegate {
    private var inFlight = 0
    private var panel: NSPanel?

    func applicationDidFinishLaunching(_ notification: Notification) {
        // Opened without a link (for example double-clicked): there's nothing to do.
        DispatchQueue.main.asyncAfter(deadline: .now() + 8) { [weak self] in self?.quitIfIdle() }
    }

    func application(_ application: NSApplication, open urls: [URL]) {
        for url in urls { route(url) }
    }

    private func route(_ url: URL) {
        let registryFile = (Bundle.main.object(forInfoDictionaryKey: LinkRouterSetup.registryKey) as? String).map(URL.init(fileURLWithPath:))
            ?? FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/Mitosis/clones.json")
        let entries = (try? CloneRegistry(fileURL: registryFile).load()) ?? []
        let state = LinkRouterState.load(from: registryFile.deletingLastPathComponent().appendingPathComponent("link-router.json"))
        let targets = LinkRouter.targets(for: url, entries: entries, state: state) { bundleID in
            !NSRunningApplication.runningApplications(withBundleIdentifier: bundleID).isEmpty
        }
        if targets.isEmpty {
            deliverToPreviousHandler(url, state: state)
        } else if let target = LinkRouter.autoTarget(targets) {
            deliver(url, to: target.bundleURL)
        } else {
            ask(url, targets: targets)
        }
    }

    private func deliver(_ url: URL, to app: URL) {
        inFlight += 1
        let config = NSWorkspace.OpenConfiguration()
        config.activates = true
        NSWorkspace.shared.open([url], withApplicationAt: app, configuration: config) { _, _ in
            Task { @MainActor in
                self.inFlight -= 1
                self.quitIfIdle()
            }
        }
    }

    /// Nobody routes this scheme any more: hand the link to whoever handled it before Mitosis.
    private func deliverToPreviousHandler(_ url: URL, state: LinkRouterState) {
        let scheme = url.scheme?.lowercased() ?? ""
        let previous = state.apps.values.compactMap { $0.previousHandlers[scheme] }.first
        if let previous, let app = NSWorkspace.shared.urlForApplication(withBundleIdentifier: previous) {
            deliver(url, to: app)
        } else {
            NSSound.beep()
            quitIfIdle()
        }
    }

    // MARK: Chooser

    private func ask(_ url: URL, targets: [LinkTarget]) {
        inFlight += 1
        let ordered = Self.byRecency(targets)
        let view = ChooserView(url: url, targets: ordered) { [weak self] choice in
            guard let self else { return }
            self.panel?.close()
            self.panel = nil
            self.inFlight -= 1
            if let choice { self.deliver(url, to: choice.bundleURL) } else { self.quitIfIdle() }
        }
        let panel = NSPanel(contentRect: NSRect(x: 0, y: 0, width: 380, height: 300), styleMask: [.titled, .closable],
                            backing: .buffered, defer: false)
        panel.title = "Mitosis"
        panel.level = .floating
        panel.isReleasedWhenClosed = false
        panel.contentViewController = NSHostingController(rootView: view)
        panel.center()
        NSApp.activate()
        panel.makeKeyAndOrderFront(nil)
        self.panel = panel
    }

    /// Running apps first, ordered by how recently their windows were in front (the app that started the sign-in
    /// is usually the one right behind the browser), then the rest by name.
    static func byRecency(_ targets: [LinkTarget]) -> [LinkTarget] {
        let windows = (CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]]) ?? []
        var order: [String: Int] = [:]
        for (index, window) in windows.enumerated() {
            guard let pid = window[kCGWindowOwnerPID as String] as? pid_t,
                  let id = NSRunningApplication(processIdentifier: pid)?.bundleIdentifier, order[id] == nil else { continue }
            order[id] = index
        }
        return targets.sorted { a, b in
            switch (order[a.bundleID], order[b.bundleID]) {
            case let (x?, y?): return x < y
            case (_?, nil): return true
            case (nil, _?): return false
            case (nil, nil): return a.isRunning != b.isRunning ? a.isRunning : a.name < b.name
            }
        }
    }

    private func quitIfIdle() {
        if inFlight == 0 && panel == nil { NSApp.terminate(nil) }
    }
}

struct ChooserView: View {
    let url: URL
    let targets: [LinkTarget]
    let done: (LinkTarget?) -> Void
    @State private var selection = 0

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            VStack(alignment: .leading, spacing: 4) {
                Text("Open this sign-in link in…")
                    .font(.headline)
                Text("Choose the copy you're signing in to.")
                    .font(.callout)
                    .foregroundStyle(.secondary)
            }
            VStack(spacing: 4) {
                ForEach(Array(targets.enumerated()), id: \.offset) { index, target in
                    Button {
                        done(target)
                    } label: {
                        HStack(spacing: 10) {
                            Image(nsImage: NSWorkspace.shared.icon(forFile: target.bundleURL.path))
                                .resizable()
                                .frame(width: 32, height: 32)
                            VStack(alignment: .leading, spacing: 1) {
                                Text(target.name)
                                Text(target.isRunning ? "Open" : "Not running")
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                            }
                            Spacer()
                        }
                        .padding(8)
                        .contentShape(Rectangle())
                        .background(index == selection ? Color.accentColor.opacity(0.16) : .clear,
                                    in: RoundedRectangle(cornerRadius: 8, style: .continuous))
                    }
                    .buttonStyle(.plain)
                    .onHover { if $0 { selection = index } }
                    .accessibilityLabel("Open in \(target.name)")
                }
            }
            HStack {
                Spacer()
                Button("Cancel") { done(nil) }
                    .keyboardShortcut(.cancelAction)
                Button("Open") { done(targets[selection]) }
                    .keyboardShortcut(.defaultAction)
            }
        }
        .padding(20)
        .frame(width: 380)
        .focusable()
        .focusEffectDisabled()
        .onMoveCommand { direction in
            if direction == .down { selection = min(selection + 1, targets.count - 1) }
            if direction == .up { selection = max(selection - 1, 0) }
        }
    }
}

MainActor.assumeIsolated {
    let router = Router()
    let app = NSApplication.shared
    app.setActivationPolicy(.accessory)
    app.delegate = router
    app.run()
}
