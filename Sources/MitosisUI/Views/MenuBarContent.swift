import AppKit
import SwiftUI

/// The optional menu bar menu: open any clone quickly, or start a new one.
struct MenuBarContent: View {
    let model: AppModel
    var updates: UpdateChecker? = nil
    @Environment(\.openWindow) private var openWindow

    var body: some View {
        if model.entries.isEmpty {
            Text("No clones yet")
        } else {
            ForEach(CloneLibrary.filter(model.entries, sidebar: .all, running: model.running, search: "")) { e in
                Button {
                    model.open(e)
                } label: {
                    Label {
                        Text(model.isRunning(e) ? "\(e.manifest.name)  ·  Running" : e.manifest.name)
                    } icon: {
                        Image(nsImage: menuIcon(IconCache.cloneIcon(e)))
                    }
                }
            }
        }
        Divider()
        if let updates, case .available(let version, _) = updates.state, updates.canInstallInPlace, updates.install == .idle {
            Button("Update Mitosis to \(version)") { Task { await updates.installUpdate(version: version) } }
            Divider()
        }
        Button("New Clone…") {
            showMain()
            model.startNewClone()
        }
        Button("Open Mitosis") { showMain() }
        Divider()
        Button("Quit Mitosis") { NSApp.terminate(nil) }
            .keyboardShortcut("q")
    }

    private func showMain() {
        NSApp.activate()
        openWindow(id: "main")
    }

    private func menuIcon(_ image: NSImage) -> NSImage {
        let small = NSImage(size: NSSize(width: 16, height: 16), flipped: false) { rect in
            image.draw(in: rect)
            return true
        }
        return small
    }
}
