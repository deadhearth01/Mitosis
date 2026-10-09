import AppKit
import SwiftUI

public struct MitosisRootApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var delegate
    @State private var model = AppModel.live()

    public init() {}

    public var body: some Scene {
        Window("Mitosis", id: "main") {
            MainWindow(model: model)
                .frame(minWidth: 720, minHeight: 460)
                .tint(Brand.accent)
                .onAppear { AppDelegate.openApps = { urls in model.handleDrop(urls) } }
        }
        .defaultSize(width: 1000, height: 660)
        .commands { MitosisCommands(model: model) }
    }
}

struct MitosisCommands: Commands {
    let model: AppModel

    var body: some Commands {
        CommandGroup(replacing: .newItem) {
            Button("New Clone…") { model.startNewClone() }
                .keyboardShortcut("n")
        }
        CommandGroup(after: .sidebar) {
            Button(model.showInspector ? "Hide Inspector" : "Show Inspector") { model.showInspector.toggle() }
                .keyboardShortcut("i")
        }
    }
}

/// Apps dropped on the Dock icon (Info.plist declares app bundles as a viewable type).
final class AppDelegate: NSObject, NSApplicationDelegate {
    @MainActor static var openApps: (([URL]) -> Void)?

    func application(_ application: NSApplication, open urls: [URL]) {
        MainActor.assumeIsolated { Self.openApps?(urls) }
    }
}

extension AppModel {
    static func live() -> AppModel {
        do {
            return AppModel(services: try AppServices.live())
        } catch {
            let model = AppModel(preview: [])
            model.loadFailed("Mitosis is missing some of its files. Reinstall it, then try again. (\(error))")
            return model
        }
    }
}
