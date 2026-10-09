import AppKit
import MitosisCore
import Observation
import SwiftUI

/// What a dialog in the main window is asking about.
enum AppPrompt: Identifiable {
    enum Action: Equatable {
        case refresh
        case restyle(Badge)
        case delete(deleteData: Bool)
        case clean

        var verb: String {
            switch self {
            case .refresh: return "Refresh"
            case .restyle: return "Update Badge"
            case .delete: return "Delete"
            case .clean: return "Clean Caches"
            }
        }
    }

    case quitFirst(RegistryEntry, Action)
    case confirmDelete(RegistryEntry)
    case confirmClean(RegistryEntry)
    case message(title: String, text: String)

    var id: String {
        switch self {
        case .quitFirst(let e, let a): return "quit-\(e.id)-\(a.verb)"
        case .confirmDelete(let e): return "delete-\(e.id)"
        case .confirmClean(let e): return "clean-\(e.id)"
        case .message(let title, _): return "message-\(title)"
        }
    }
}

/// A failed action, shown in a sheet with "Copy Details" and "Report on GitHub".
struct Failure: Identifiable {
    let id = UUID()
    var title: String
    var report: ErrorReport
}

@MainActor
@Observable
final class AppModel {
    private(set) var entries: [RegistryEntry] = []
    private(set) var statuses: [UUID: CloneStatus] = [:]
    private(set) var running: Set<UUID> = []
    /// Clone ID → what Mitosis is doing to it right now ("Refreshing…").
    private(set) var busy: [UUID: String] = [:]
    private(set) var loadError: String?

    var sidebar: SidebarItem = .all
    var search = ""
    var selection: UUID?
    var showInspector = false
    var prompt: AppPrompt?
    var failure: Failure?
    var restyling: RegistryEntry?

    let services: AppServices?
    @ObservationIgnored private var tracker: RunningTracker?

    init(services: AppServices) {
        self.services = services
        let tracker = RunningTracker()
        self.tracker = tracker
        tracker.onChange = { [weak self] in self?.updateRunning() }
        reload()
    }

    /// Fixed data for previews and snapshots; never touches the disk.
    init(preview entries: [RegistryEntry], statuses: [UUID: CloneStatus] = [:], running: Set<UUID> = [], busy: [UUID: String] = [:]) {
        self.services = nil
        self.entries = entries
        self.statuses = statuses
        self.running = running
        self.busy = busy
    }

    // MARK: Derived

    var visibleEntries: [RegistryEntry] { CloneLibrary.filter(entries, sidebar: sidebar, running: running, search: search) }
    var groups: [AppGroup] { CloneLibrary.groups(entries) }
    var selectedEntry: RegistryEntry? { selection.flatMap(entry) }
    var existingNames: Set<String> { Set(entries.map(\.manifest.name)) }

    func entry(_ id: UUID) -> RegistryEntry? { entries.first { $0.id == id } }
    func status(of e: RegistryEntry) -> CloneStatus { statuses[e.id] ?? .upToDate }
    func isRunning(_ e: RegistryEntry) -> Bool { running.contains(e.id) }

    // MARK: Loading

    func reload() {
        guard let services else { return }
        do {
            entries = try CloneRegistry(fileURL: services.environment.registryFile).loadOrRebuild(clonesDir: services.environment.clonesDir)
            loadError = nil
        } catch {
            loadError = ErrorReport.sanitize("\(error)")
        }
        if let selection, entry(selection) == nil { self.selection = nil }
        if case .app(let id) = sidebar, !entries.contains(where: { $0.manifest.source.bundleID == id }) { sidebar = .all }
        updateRunning()
        refreshStatuses()
    }

    func updateRunning() {
        guard let tracker else { return }
        running = tracker.running(entries)
    }

    /// Checks every clone against its original (codesign reads), off the main actor.
    func refreshStatuses() {
        guard let services else { return }
        let snapshot = entries
        Task {
            let result = try? await background {
                let maintenance = CloneMaintenance(builder: services.builder)
                return Dictionary(uniqueKeysWithValues: snapshot.map { ($0.id, maintenance.status(of: $0)) })
            }
            if let result { statuses = result }
        }
    }

    // MARK: Simple actions

    func open(_ e: RegistryEntry) {
        if status(of: e) == .originalMissing {
            prompt = .message(title: "\(CloneLibrary.appName(for: e)) is missing",
                              text: "The original app isn't where it was. Use Find App… to show Mitosis where it is now.")
            return
        }
        NSWorkspace.shared.openApplication(at: e.bundleURL, configuration: NSWorkspace.OpenConfiguration()) { _, error in
            guard let error else { return }
            Task { @MainActor in
                self.failure = Failure(title: "Couldn't open \(e.manifest.name)",
                                       report: ErrorReport.make(error: error, appName: e.manifest.name, appVersion: nil, mode: e.manifest.mode))
            }
        }
    }

    func reveal(_ e: RegistryEntry) {
        NSWorkspace.shared.activateFileViewerSelecting([e.bundleURL])
    }

    func revealOriginal(_ e: RegistryEntry) {
        NSWorkspace.shared.activateFileViewerSelecting([URL(fileURLWithPath: e.manifest.source.path)])
    }

    func revealData(_ e: RegistryEntry) {
        let url = URL(fileURLWithPath: e.manifest.dataPath)
        guard FileManager.default.fileExists(atPath: url.path) else {
            prompt = .message(title: "No data yet", text: "\(e.manifest.name) hasn't saved anything yet. Open it once to create its data folder.")
            return
        }
        NSWorkspace.shared.open(url)
    }

    // MARK: Actions that need the clone closed

    func request(_ action: AppPrompt.Action, for e: RegistryEntry) {
        if isRunning(e) {
            prompt = .quitFirst(e, action)
        } else {
            perform(action, on: e)
        }
    }

    func requestDelete(_ e: RegistryEntry) { prompt = .confirmDelete(e) }
    func requestClean(_ e: RegistryEntry) { prompt = .confirmClean(e) }

    /// Quits the clone, waits up to 10 s for it to close, then runs the action.
    func quitThenPerform(_ action: AppPrompt.Action, on e: RegistryEntry) {
        busy[e.id] = "Quitting…"
        RunningTracker.quit(e)
        Task {
            for _ in 0..<40 {
                updateRunning()
                if !isRunning(e) { break }
                try? await Task.sleep(for: .milliseconds(250))
            }
            busy[e.id] = nil
            if isRunning(e) {
                prompt = .message(title: "\(e.manifest.name) is still open",
                                  text: "It may be asking to save something. Quit it yourself, then try again.")
            } else {
                perform(action, on: e)
            }
        }
    }

    func perform(_ action: AppPrompt.Action, on e: RegistryEntry) {
        guard let services else { return }
        switch action {
        case .refresh:
            run(e, label: "Refreshing…", failureTitle: "Couldn't refresh \(e.manifest.name)") {
                _ = try await CloneWorkflow(builder: services.builder).refreshVerified(e)
            }
        case .restyle(let badge):
            run(e, label: "Updating badge…", failureTitle: "Couldn't update the badge") {
                _ = try await background { try CloneMaintenance(builder: services.builder).restyle(e, badge: badge) }
            }
        case .delete(let deleteData):
            run(e, label: "Moving to Trash…", failureTitle: "Couldn't delete \(e.manifest.name)") {
                try await background { try CloneMaintenance(builder: services.builder).delete(e, deleteData: deleteData) }
            }
        case .clean:
            run(e, label: "Cleaning…", failureTitle: "Couldn't clean caches") {
                let freed = try await background { try StatsCollector.cleanCaches(dataPath: URL(fileURLWithPath: e.manifest.dataPath)) }
                self.prompt = .message(title: "Caches cleaned",
                                       text: freed > 0
                                           ? "Freed \(Format.bytes(freed)) from \(e.manifest.name). Logins and settings stay."
                                           : "\(e.manifest.name) had no caches to clean.")
            }
        }
    }

    func relink(_ e: RegistryEntry, to url: URL) {
        guard let services else { return }
        run(e, label: "Linking…", failureTitle: "Couldn't use that app") {
            _ = try await background { try CloneMaintenance(builder: services.builder).relink(e, to: url) }
        }
    }

    /// Marks the clone busy, runs the work, reloads, and turns errors into a Failure sheet.
    private func run(_ e: RegistryEntry, label: String, failureTitle: String, _ work: @escaping @MainActor () async throws -> Void) {
        busy[e.id] = label
        Task {
            do {
                try await work()
            } catch {
                failure = Failure(title: failureTitle,
                                  report: ErrorReport.make(error: error, appName: CloneLibrary.appName(for: e),
                                                           appVersion: e.manifest.source.version, mode: e.manifest.mode))
            }
            busy[e.id] = nil
            reload()
        }
    }
}

enum Format {
    static func bytes(_ n: Int64) -> String { ByteCountFormatter.string(fromByteCount: n, countStyle: .file) }
}
