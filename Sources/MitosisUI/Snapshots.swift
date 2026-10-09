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
            Shot(name: "main-empty", size: CGSize(width: 1000, height: 640)) {
                AnyView(MainWindow(model: AppModel(preview: [])).tint(Brand.accent))
            },
        ]
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
