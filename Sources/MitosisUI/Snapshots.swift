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
        let samples = SampleData.make(in: output.deletingLastPathComponent().appendingPathComponent("snapshot-fixtures"))
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
            Shot(name: "main-grid", size: CGSize(width: 1000, height: 640)) {
                let m = s.model(); m.selection = s.entries[1].id
                return AnyView(MainWindow(model: m).tint(Brand.accent))
            },
            Shot(name: "main-inspector", size: CGSize(width: 1100, height: 680)) {
                let m = s.model(); m.selection = s.entries[0].id; m.showInspector = true
                return AnyView(MainWindow(model: m).tint(Brand.accent))
            },
            Shot(name: "inspector-update", size: CGSize(width: 1100, height: 680)) {
                let m = s.model(); m.selection = s.entries[2].id; m.showInspector = true
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
            Shot(name: "new-pick", size: CGSize(width: 640, height: 540), titled: false) {
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
            Shot(name: "main-update", size: CGSize(width: 1000, height: 640)) {
                let u = UpdateChecker(); u.state = .available(version: "0.1.1", url: SystemLinks.releases)
                return AnyView(MainWindow(model: s.model(), updates: u).tint(Brand.accent))
            },
            Shot(name: "main-empty", size: CGSize(width: 1000, height: 640)) {
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
        guard let rep = view.bitmapImageRepForCachingDisplay(in: view.bounds) else { return }
        view.cacheDisplay(in: view.bounds, to: rep)
        try? rep.representation(using: .png, properties: [:])?.write(to: url)
        window.orderOut(nil)
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

    func model() -> AppModel { AppModel(preview: entries, statuses: statuses, running: running, busy: busy) }

    func newClone(step: NewCloneModel.Step) -> NewCloneModel {
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
        let signal = apps.first { $0.name == "Signal" }!
        var form = NewCloneForm(appName: "Signal", existingNames: ["Signal (Work)"], usedColors: ["#0A84FF"])
        form.setLabel("Family")
        return NewCloneModel(preview: apps, support: support, step: step, chosen: signal, form: form)
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
