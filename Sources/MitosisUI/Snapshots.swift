#if DEBUG
import AppKit
import MitosisCore
import SwiftUI

/// Developer tool behind `swift run MitosisSnapshot`: renders screens with sample data to PNG, light and dark.
public enum SnapshotRunner {
    struct Shot {
        var name: String
        var size: CGSize
        var titled: Bool = true
        var make: @MainActor () -> AnyView
    }

    @MainActor public static func run(output: URL, filter: String?) {
        NSApplication.shared.setActivationPolicy(.accessory)
        try? FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)
        writeMenuBarIcon(to: output.appendingPathComponent("menubar-icon.png"))
        // Sample clones live on the startup disk (like real ones), so disk captions read as they do for users.
        let samples = SampleData.make(in: FileManager.default.temporaryDirectory.appendingPathComponent("mitosis-snapshot-fixtures"))
        for shot in shots(samples) where filter.map({ shot.name.contains($0) }) ?? true {
            for dark in [false, true] {
                let url = output.appendingPathComponent("\(shot.name)-\(dark ? "dark" : "light").png")
                render(shot, dark: dark, to: url)
                print("wrote \(url.lastPathComponent)")
            }
        }
    }

    @MainActor static func shots(_ s: SampleData) -> [Shot] {
        [
            Shot(name: "main-detail", size: CGSize(width: 1060, height: 720)) {
                let m = s.model(); m.selection = s.entries.first { $0.manifest.name == "Signal (Work)" }?.id
                return AnyView(MainWindow(model: m).tint(Brand.accent))
            },
            Shot(name: "detail-links", size: CGSize(width: 1060, height: 1100)) {
                let m = s.model(); m.selection = s.entries.first { $0.manifest.name == "Claude (Research)" }?.id
                return AnyView(MainWindow(model: m).tint(Brand.accent))
            },
            Shot(name: "detail-update", size: CGSize(width: 1060, height: 720)) {
                let m = s.model(); m.selection = s.entries[2].id
                return AnyView(MainWindow(model: m).tint(Brand.accent))
            },
            Shot(name: "detail-missing", size: CGSize(width: 1060, height: 720)) {
                let m = s.model(); m.selection = s.entries[5].id
                return AnyView(MainWindow(model: m).tint(Brand.accent))
            },
            Shot(name: "sheet-restyle", size: CGSize(width: 460, height: 250), titled: false) {
                AnyView(RestyleSheet(model: s.model(), entry: s.entries[1]).tint(Brand.accent).background(.windowBackground))
            },
            Shot(name: "sheet-failure", size: CGSize(width: 480, height: 400), titled: false) {
                let report = ErrorReport.make(error: CloneError.failed(step: "launch check", message: "\"Signal (Work)\" didn't stay open, so it was removed. Try compatibility (fallback) mode."),
                                              appName: "Signal", appVersion: "8.2.1", mode: .identity)
                return AnyView(FailureView(title: "Couldn't create Signal (Work)", report: report, retryTitle: "Try Compatibility Mode", retry: {}, close: {})
                    .tint(Brand.accent).background(.windowBackground))
            },
            Shot(name: "new-pick", size: CGSize(width: 700, height: 680), titled: false) {
                let m = s.newClone(step: .pick); m.selectedID = m.apps[5].id
                return sheet(NewCloneSheet(model: m, close: {}))
            },
            Shot(name: "new-details", size: CGSize(width: 640, height: 440), titled: false) {
                sheet(NewCloneSheet(model: s.newClone(step: .details), close: {}))
            },
            Shot(name: "new-working", size: CGSize(width: 640, height: 440), titled: false) {
                sheet(NewCloneSheet(model: s.newClone(step: .working(NewCloneModel.label(for: "launch check"))), close: {}))
            },
            Shot(name: "new-done", size: CGSize(width: 640, height: 440), titled: false) {
                sheet(NewCloneSheet(model: s.newClone(step: .done(s.entries[0])), close: {}))
            },
            Shot(name: "onboard-1", size: CGSize(width: 580, height: 460), titled: false) { sheet(OnboardingView { _ in }) },
            Shot(name: "help-apps", size: CGSize(width: 820, height: 580)) {
                AnyView(HelpTopicPreview(topic: .apps).tint(Brand.accent))
            },
            Shot(name: "help-trouble", size: CGSize(width: 820, height: 580)) {
                AnyView(HelpTopicPreview(topic: .trouble).tint(Brand.accent))
            },
            Shot(name: "settings-general", size: CGSize(width: 540, height: 460)) {
                let u = UpdateChecker(); u.state = .available(version: "0.1.1", url: SystemLinks.releases)
                return AnyView(SettingsView(model: s.model(), updates: u))
            },
            Shot(name: "settings-advanced", size: CGSize(width: 540, height: 320)) {
                AnyView(AdvancedSettings().tint(Brand.accent))
            },
            Shot(name: "main-update", size: CGSize(width: 1060, height: 720)) {
                let u = UpdateChecker(); u.state = .available(version: "0.1.1", url: SystemLinks.releases)
                return AnyView(MainWindow(model: s.model(), updates: u).tint(Brand.accent))
            },
            Shot(name: "guide-codex-details", size: CGSize(width: 640, height: 440), titled: false) {
                sheet(NewCloneSheet(model: s.newClone(step: .details, app: "Codex", label: "Work"), close: {}))
            },
            Shot(name: "guide-claude-details", size: CGSize(width: 640, height: 440), titled: false) {
                sheet(NewCloneSheet(model: s.newClone(step: .details, app: "Claude", label: "Personal"), close: {}))
            },
            Shot(name: "main-empty", size: CGSize(width: 1060, height: 720)) {
                AnyView(MainWindow(model: AppModel(preview: [])).tint(Brand.accent))
            },
        ]
    }

    @MainActor static func sheet<V: View>(_ view: V) -> AnyView {
        AnyView(view.tint(Brand.accent).background(.windowBackground))
    }

    /// Hosts the view in a real (invisible) window so toolbars, titles and sidebars render, then draws the frame.
    @MainActor static func render(_ shot: Shot, dark: Bool, to url: URL) {
        let controller = NSHostingController(rootView: shot.make().frame(width: shot.size.width, height: shot.size.height)
            .environment(\.controlActiveState, .key))
        controller.sceneBridgingOptions = [.toolbars, .title]
        let window = SnapshotWindow(contentViewController: controller)
        window.styleMask = shot.titled ? [.titled, .closable, .miniaturizable, .resizable, .fullSizeContentView] : [.borderless]
        window.appearance = NSAppearance(named: dark ? .darkAqua : .aqua)
        window.title = "Mitosis"
        window.setContentSize(shot.size)
        window.alphaValue = 0
        window.orderFront(nil)
        spin(0.8)
        let view = window.contentView?.superview ?? controller.view
        // Always 2x, so screenshots stay sharp on Retina displays whatever screen this runs on.
        guard let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: Int(view.bounds.width * 2), pixelsHigh: Int(view.bounds.height * 2),
                                         bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
                                         colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0) else { return }
        rep.size = view.bounds.size
        view.cacheDisplay(in: view.bounds, to: rep)
        try? rep.representation(using: .png, properties: [:])?.write(to: url)
        window.orderOut(nil)
    }

    /// The menu bar glyph at 8x on light and dark strips, to check it reads at its real size.
    @MainActor static func writeMenuBarIcon(to url: URL) {
        let scale: CGFloat = 8, w: CGFloat = 18 * scale
        let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: Int(w * 2 + 3 * scale), pixelsHigh: Int(w), bitsPerSample: 8,
                                   samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
        NSGraphicsContext.saveGraphicsState()
        NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
        for (i, (bg, fg)) in [(NSColor(white: 0.93, alpha: 1), NSColor.black), (NSColor(white: 0.16, alpha: 1), NSColor.white)].enumerated() {
            let r = NSRect(x: CGFloat(i) * (w + 3 * scale), y: 0, width: w, height: w)
            bg.setFill(); r.fill()
            let tinted = NSImage(size: MenuBarIcon.image.size, flipped: false) { rect in
                MenuBarIcon.image.draw(in: rect)
                fg.set(); rect.fill(using: .sourceAtop)
                return true
            }
            tinted.draw(in: r)
        }
        NSGraphicsContext.restoreGraphicsState()
        try? rep.representation(using: .png, properties: [:])?.write(to: url)
    }

    @MainActor static func spin(_ seconds: Double) {
        let end = Date().addingTimeInterval(seconds)
        while Date() < end { RunLoop.current.run(mode: .default, before: Date().addingTimeInterval(0.05)) }
    }
}

/// `MitosisSnapshot --live-create <app>`: runs the real New Clone flow (MITOSIS_HOME should point at a scratch folder),
/// checks the clone exists and started, then deletes it.
public enum LiveCheck {
    @MainActor public static func run(app: URL) async -> Bool {
        guard let services = try? AppServices.live() else { print("FAIL: services"); return false }
        let model = NewCloneModel(services: services, existingNames: [], usedColors: [:])
        await model.choose(url: app)
        guard model.step == .details, model.form != nil else { print("FAIL: choose → \(model.step) \(model.dropError ?? "")"); return false }
        model.form?.setLabel("Live Check")
        var steps: [String] = []
        let watcher = Task { @MainActor in
            while !Task.isCancelled {
                if case .working(let label) = model.step, steps.last != label { steps.append(label) }
                try? await Task.sleep(for: .milliseconds(20))
            }
        }
        await model.create(confirmedFullCopy: true)
        watcher.cancel()
        print("steps: \(steps.joined(separator: " → "))")
        guard case .done(let entry) = model.step else { print("FAIL: create → \(model.step)"); return false }
        let running = RunningMonitor.isRunning(clone: entry.manifest)
        print("created \(entry.manifest.name) at \(entry.bundlePath), running: \(running)")
        RunningTracker.quit(entry)
        try? await Task.sleep(for: .seconds(1))
        do { try CloneMaintenance(builder: services.builder).delete(entry, deleteData: true) } catch { print("FAIL: delete \(error)"); return false }
        print(FileManager.default.fileExists(atPath: entry.bundlePath) ? "FAIL: still there" : "deleted")
        return running && !FileManager.default.fileExists(atPath: entry.bundlePath)
    }
}

struct HelpTopicPreview: View {
    let topic: HelpTopic
    @State private var selection: HelpTopic?
    var body: some View {
        NavigationSplitView {
            List(HelpTopic.allCases, selection: $selection) { t in Label(t.title, systemImage: t.symbol).tag(t) }
                .navigationSplitViewColumnWidth(min: 200, ideal: 220, max: 280)
        } detail: { HelpPage(topic: topic) }
        .onAppear { selection = topic }
    }
}

/// `MitosisSnapshot --catalog`: what the New Clone picker would list on this Mac, and each app's support level.
public enum CatalogReport {
    @MainActor public static func run() {
        guard let services = try? AppServices.live() else { print("no services"); return }
        let apps = AppCatalog.scan()
        let decider = ModeDecider(profiles: services.profiles)
        var counts: [String: Int] = [:]
        for app in apps {
            let decision = (try? AppInspector().inspect(app.url, includeCDHash: false)).map { decider.decide(for: $0) }
            let level = decision?.support.displayName ?? "unreadable"
            counts[level, default: 0] += 1
            print("\(level.padding(toLength: 18, withPad: " ", startingAt: 0)) \(app.name)  —  \(decision?.reasons.first?.prefix(110) ?? "")")
        }
        print("TOTAL \(apps.count): \(counts)")
    }
}

/// `MitosisSnapshot --live-autoupdate <app>`: makes a clone, "updates" the original (new version, re-signed), and
/// checks that the app model rebuilds the clone on its own. MITOSIS_HOME should point at a scratch folder.
public enum LiveAutoUpdateCheck {
    @MainActor public static func run(app: URL) async -> Bool {
        UserDefaults.standard.set(true, forKey: Prefs.autoUpdateKey)
        guard let services = try? AppServices.live() else { print("FAIL: services"); return false }
        let builder = services.builder
        guard let entry = try? builder.create(CloneRequest(source: app, name: "Fixture (Auto)", badge: Badge(text: "A", color: "#30D158"))) else {
            print("FAIL: create"); return false
        }
        let plist = app.appendingPathComponent("Contents/Info.plist")
        guard var info = try? InfoPlistEditor.read(plist) else { print("FAIL: plist"); return false }
        let newVersion = "9.\(Int.random(in: 1...999))"
        info["CFBundleShortVersionString"] = newVersion
        try? InfoPlistEditor.write(info, to: plist)
        _ = try? Shell.run("/usr/bin/codesign", ["--force", "--sign", "-", app.path])
        print("original now \(newVersion); starting app model")
        let model = AppModel(services: services)
        var ok = false
        for _ in 0..<60 {
            try? await Task.sleep(for: .milliseconds(500))
            if let e = model.entries.first(where: { $0.id == entry.id }), e.manifest.source.version == newVersion { ok = true; break }
        }
        print(ok ? "clone rebuilt automatically to \(newVersion)" : "FAIL: clone not rebuilt (status \(String(describing: model.statuses[entry.id])))")
        try? CloneMaintenance(builder: builder).delete(entry, deleteData: true)
        return ok
    }
}

final class SnapshotWindow: NSWindow {
    override func constrainFrameRect(_ frameRect: NSRect, to screen: NSScreen?) -> NSRect { frameRect }
    // Render controls the way they look in the active window (accent-colored default buttons).
    override var isKeyWindow: Bool { true }
    override var isMainWindow: Bool { true }
}

/// Clone-like bundles (only their badged icons) made from apps installed on this Mac.
@MainActor
struct SampleData {
    var entries: [RegistryEntry]
    var statuses: [UUID: CloneStatus]
    var running: Set<UUID>
    var busy: [UUID: String]

    func model() -> AppModel {
        let m = AppModel(preview: entries, statuses: statuses, running: running, busy: busy)
        for (i, e) in entries.enumerated() {
            m.previewStats[e.id] = CloneStats(extraDiskBytes: Int64(1_300_000 + i * 410_000), appBytes: 600_000_000,
                                              dataBytes: Int64(48_000_000 + i * 91_000_000),
                                              usage: running.contains(e.id) ? CloneUsage(processCount: 6, cpuPercent: 3.4, memoryBytes: 412_000_000) : nil)
        }
        return m
    }

    func newClone(step: NewCloneModel.Step) -> NewCloneModel { newClone(step: step, app: "Signal", label: "Family") }

    /// The New Clone sheet for any installed app (guide screenshots).
    func newClone(step: NewCloneModel.Step, app appName: String, label: String) -> NewCloneModel {
        let names = ["Arc", "Claude", "Cursor", "Figma", "Google Chrome", "Notion", "Obsidian", "Pages", "Postman", "Proton Pass", "Signal", "Safari"]
        let apps = names.map { name in
            CatalogApp(url: URL(fileURLWithPath: name == "Safari" ? "/Applications/Safari.app" : "/Applications/\(name).app"), name: name,
                       bundleID: "com.example.\(name.lowercased())", version: name == "Signal" ? "8.2.1" : "1.4.2", lastUsed: Date())
        }
        var support: [String: ModeDecision] = [:]
        for app in apps {
            switch app.name {
            case "Safari", "Pages":
                support[app.id] = ModeDecision(mode: nil, support: .unsupported, reasons: ["Apple's own apps are protected by macOS and can't be cloned."])
            case "Proton Pass":
                support[app.id] = ModeDecision(mode: .identity, support: .limited, reasons: ["Mac App Store app: the clone may ask you to sign in again or refuse to start."])
            case "Postman":
                break   // still checking
            default:
                support[app.id] = ModeDecision(mode: .identity, support: .full, reasons: ["Electron app. Link each clone as a separate device."])
            }
        }
        let chosen = apps.first { $0.name == appName }
            ?? CatalogApp(url: URL(fileURLWithPath: "/Applications/\(appName).app"), name: appName, bundleID: "com.example.\(appName.lowercased())",
                          version: "1.0", lastUsed: Date())
        if support[chosen.id] == nil {
            support[chosen.id] = ModeDecision(mode: .identity, support: .full, reasons: ["Electron app. Each clone signs in separately."])
        }
        var form = NewCloneForm(appName: appName, existingNames: ["\(appName) (Work)"], usedColors: ["#0A84FF"])
        form.setLabel(label)
        return NewCloneModel(preview: apps, support: support, step: step, chosen: chosen, form: form)
    }

    static func make(in dir: URL) -> SampleData {
        let specs: [(app: String, label: String, badge: String, color: String)] = [
            ("Signal", "Work", "W", "#0A84FF"), ("Signal", "Family", "F", "#30D158"), ("Notion", "Client A", "A", "#FF9F0A"),
            ("Claude", "Research", "R", "#BF5AF2"), ("Cursor", "Side Project", "S", "#FF375F"), ("Obsidian", "Journal", "J", "#8E8E93"),
        ]
        var entries: [RegistryEntry] = []
        for (i, spec) in specs.enumerated() {
            let source = URL(fileURLWithPath: "/Applications/\(spec.app).app")
            let name = CloneName.compose(app: spec.app, label: spec.label)
            let bundle = dir.appendingPathComponent("Clones/\(name).app")
            let resources = bundle.appendingPathComponent("Contents/Resources")
            try? FileManager.default.createDirectory(at: resources, withIntermediateDirectories: true)
            let badge = Badge(text: spec.badge, color: spec.color)
            if let base = IconRenderer.baseIcon(forApp: source), let data = try? IconRenderer.icnsData(base: base, badge: badge) {
                try? data.write(to: resources.appendingPathComponent("\(IconRenderer.iconFileBaseName).icns"))
            }
            let id = UUID(uuidString: String(format: "00000000-0000-4000-8000-%012d", i + 1))!
            let manifest = CloneManifest(
                id: id, name: name, badge: badge, mode: spec.app == "Notion" ? .fallback : .identity,
                cloneBundleID: "com.example.\(spec.app.lowercased()).mitosis.\(Slug.make(from: name))",
                source: .init(path: source.path, bundleID: "com.example.\(spec.app.lowercased())", version: "8.2.1", cdhash: nil),
                profile: nil, dataPath: NSHomeDirectory() + "/Library/Mitosis/Data/\(id.uuidString.prefix(8).lowercased())",
                createdAt: Date().addingTimeInterval(-86_400 * Double(12 - i)), refreshedAt: Date().addingTimeInterval(-3_600 * Double(i + 2)))
            entries.append(RegistryEntry(manifest: manifest, bundlePath: bundle.path))
        }
        return SampleData(
            entries: entries,
            statuses: [entries[2].id: .updateAvailable(currentVersion: "3.1"), entries[5].id: .originalMissing],
            running: [entries[0].id, entries[3].id],
            busy: [entries[4].id: "Refreshing…"])
    }
}
#endif
