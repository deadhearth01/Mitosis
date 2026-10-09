import AppKit
import SwiftUI

public struct MitosisRootApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var delegate
    @State private var model = AppModel.live()
    @State private var updates = UpdateChecker()
    @AppStorage(Prefs.menuBarKey) private var showMenuBar = false

    public init() {
        UserDefaults.standard.register(defaults: [Prefs.checkUpdatesKey: true, Prefs.showTipsKey: true, Prefs.autoUpdateKey: true])
    }

    public var body: some Scene {
        Window("Mitosis", id: "main") {
            MainWindow(model: model, updates: updates)
                .frame(minWidth: 720, minHeight: 460)
                .tint(Brand.accent)
                .onAppear {
                    AppDelegate.openApps = { urls in model.handleDrop(urls) }
                    updates.checkIfDue()
                }
        }
        .defaultSize(width: 1000, height: 660)
        .commands { MitosisCommands(model: model) }

        Window("Mitosis Help", id: "help") {
            HelpView()
                .tint(Brand.accent)
        }
        .defaultSize(width: 820, height: 580)

        Settings {
            SettingsView(model: model, updates: updates)
        }

        MenuBarExtra("Mitosis", systemImage: "square.split.2x1", isInserted: $showMenuBar) {
            MenuBarContent(model: model)
        }
    }
}

struct MitosisCommands: Commands {
    let model: AppModel
    @Environment(\.openWindow) private var openWindow
    @Environment(\.openURL) private var openURL

    var body: some Commands {
        CommandGroup(replacing: .help) {
            Button("Mitosis Help") { openWindow(id: "help") }
                .keyboardShortcut("?", modifiers: .command)
            Divider()
            Button("Report a Problem…") { openURL(SystemLinks.newIssue) }
            Button("Mitosis on GitHub") { openURL(SystemLinks.repository) }
        }
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

    /// With the menu bar extra on, closing the window keeps Mitosis running in the menu bar.
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        !UserDefaults.standard.bool(forKey: Prefs.menuBarKey)
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
