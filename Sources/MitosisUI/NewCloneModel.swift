import AppKit
import MitosisCore
import Observation
import SwiftUI

/// One run of the New Clone sheet: pick an app, set the label and badge, create, verify.
@MainActor
@Observable
final class NewCloneModel: Identifiable {
    enum Step: Equatable {
        case pick
        case details
        case working(String)
        case done(RegistryEntry)
        case failed(ErrorReport, canRetryCompatible: Bool)
    }

    enum Filter: String, CaseIterable, Identifiable {
        case all = "All"
        case supported = "Works great"
        case recent = "Recently used"
        var id: String { rawValue }
    }

    let id = UUID()
    var step: Step = .pick
    private(set) var apps: [CatalogApp] = []
    private(set) var loadingApps = false
    private(set) var support: [String: ModeDecision] = [:]
    var filter: Filter = .all
    var query = ""
    var selectedID: String?
    private(set) var chosen: CatalogApp?
    private(set) var preflight: ClonePreflight?
    var form: NewCloneForm?
    var askFullCopy = false
    var dropError: String?
    private(set) var inspecting = false

    let services: AppServices?
    let existingNames: Set<String>
    let usedColors: [String: Set<String>]
    /// Tells the main window about a new clone (to reload and select it).
    @ObservationIgnored var onCreated: ((RegistryEntry) -> Void)?

    init(services: AppServices?, existingNames: Set<String>, usedColors: [String: Set<String>]) {
        self.services = services
        self.existingNames = existingNames
        self.usedColors = usedColors
    }

    /// Fixed state for previews and snapshots.
    init(preview apps: [CatalogApp], support: [String: ModeDecision], step: Step = .pick, chosen: CatalogApp? = nil,
         form: NewCloneForm? = nil, preflight: ClonePreflight? = nil) {
        self.services = nil
        self.existingNames = []
        self.usedColors = [:]
        self.apps = apps
        self.support = support
        self.step = step
        self.chosen = chosen
        self.form = form
        self.preflight = preflight
        self.selectedID = chosen?.id
    }

    // MARK: Picker

    var visibleApps: [CatalogApp] {
        let q = query.trimmingCharacters(in: .whitespacesAndNewlines)
        var list = apps.filter { q.isEmpty || $0.name.range(of: q, options: [.caseInsensitive, .diacriticInsensitive]) != nil }
        switch filter {
        case .all: break
        case .supported: list = list.filter { support[$0.id]?.support == .full }
        case .recent:
            let cutoff = Date().addingTimeInterval(-30 * 86_400)
            list = list.filter { ($0.lastUsed ?? .distantPast) > cutoff }.sorted { ($0.lastUsed ?? .distantPast) > ($1.lastUsed ?? .distantPast) }
        }
        return list
    }

    var selectedApp: CatalogApp? { selectedID.flatMap { id in apps.first { $0.id == id } } }

    func canChoose(_ app: CatalogApp) -> Bool { support[app.id]?.support != .unsupported }

    /// Lists installed apps, then works out how well each can be cloned (4 at a time, off the main actor).
    func loadApps() async {
        guard let services, apps.isEmpty, !loadingApps else { return }
        loadingApps = true
        apps = (try? await offMain { AppCatalog.scan() }) ?? []
        loadingApps = false
        let decider = ModeDecider(profiles: services.profiles)
        await withTaskGroup(of: (String, ModeDecision?).self) { group in
            var pending = apps.makeIterator()
            func addNext() -> Bool {
                guard let app = pending.next() else { return false }
                group.addTask {
                    let decision = try? await offMain { decider.decide(for: try AppInspector().inspect(app.url, includeCDHash: false)) }
                    return (app.id, decision)
                }
                return true
            }
            for _ in 0..<4 where addNext() {}
            for await (id, decision) in group {
                if let decision { support[id] = decision }
                _ = addNext()
            }
        }
    }

    /// Why a dropped or chosen file can't be cloned, judged from the path alone (nil = worth inspecting).
    nonisolated static func rejection(for url: URL) -> String? {
        var isDirectory: ObjCBool = false
        guard url.pathExtension.lowercased() == "app",
              FileManager.default.fileExists(atPath: url.path, isDirectory: &isDirectory), isDirectory.boolValue
        else { return "That's not an app." }
        if url.resolvingSymlinksInPath().path.hasPrefix("/System/") {
            return "Apple's own apps are protected by macOS and can't be cloned."
        }
        return nil
    }

    func choose(url: URL) async {
        if let reason = Self.rejection(for: url) {
            dropError = reason
            return
        }
        let found = try? await offMain { () -> (String, String)? in
            let info = try AppInspector.readInfoPlist(url)
            guard let bundleID = info["CFBundleIdentifier"] as? String else { return nil }
            return (bundleID, info["CFBundleShortVersionString"] as? String ?? "")
        }
        guard let (bundleID, version) = found ?? nil else {
            dropError = "That app is missing its details, so Mitosis can't read it."
            return
        }
        let app = CatalogApp(url: url, name: url.deletingPathExtension().lastPathComponent, bundleID: bundleID,
                             version: version, lastUsed: nil)
        await choose(app)
    }

    /// Checks the app (mode, support, drive) and moves to the details step.
    func choose(_ app: CatalogApp) async {
        guard let services else { return }
        dropError = nil
        inspecting = true
        defer { inspecting = false }
        do {
            let builder = services.builder
            let pre = try await offMain { try builder.preflight(source: app.url) }
            support[app.id] = pre.decision
            guard pre.decision.support != .unsupported else {
                dropError = pre.decision.reasons.first ?? "This app can't be cloned yet."
                return
            }
            chosen = app
            preflight = pre
            var form = NewCloneForm(appName: app.name, existingNames: existingNames, usedColors: usedColors[app.bundleID] ?? [])
            form.mode = UserDefaults.standard.string(forKey: Prefs.defaultModeKey).flatMap(CloneMode.init(rawValue:))
            self.form = form
            step = .details
        } catch {
            dropError = ErrorReport.make(error: error, appName: app.name, appVersion: app.version, mode: nil).message
        }
    }

    func back() {
        step = .pick
        askFullCopy = false
    }

    // MARK: Create

    var validationMessage: String? { form?.validationMessage(existingNames: existingNames) }

    /// Creates the clone and opens it once to check that it starts. A clone that fails is removed by the engine.
    func create(confirmedFullCopy: Bool = false) async {
        guard let services, let chosen, let form, validationMessage == nil else { return }
        if preflight?.willCopyFully == true && !confirmedFullCopy {
            askFullCopy = true
            return
        }
        askFullCopy = false
        let name = form.composedName
        step = .working(Self.label(for: "copy"))
        var builder = services.builder
        builder.progress = { [weak self] stepName in
            Task { @MainActor in
                guard let self, case .working = self.step else { return }
                self.step = .working(Self.label(for: stepName))
            }
        }
        let request = CloneRequest(source: chosen.url, name: name, badge: form.badge, modeOverride: form.mode)
        do {
            let entry = try await CloneWorkflow(builder: builder).createVerified(request)
            if !form.openAfter { CloneLauncher.terminate(entry) }
            UserDefaults.standard.set(false, forKey: Prefs.showTipsKey)
            step = .done(entry)
            onCreated?(entry)
        } catch {
            let usedMode = form.mode ?? preflight?.decision.mode
            var retry = false
            if case .failed(let failedStep, _)? = error as? CloneError, failedStep == "launch check", usedMode == .identity { retry = true }
            step = .failed(ErrorReport.make(error: error, appName: chosen.name, appVersion: chosen.version, mode: usedMode),
                           canRetryCompatible: retry)
        }
    }

    func retryInCompatibilityMode() async {
        form?.mode = .fallback
        await create(confirmedFullCopy: true)
    }

    var isWorking: Bool {
        if case .working = step { return true }
        return false
    }

    static func label(for step: String) -> String {
        let text = CloneError.stepLabel(step)
        return text.prefix(1).uppercased() + text.dropFirst() + "…"
    }
}

/// UserDefaults keys shared by Settings and the flows that read them.
enum Prefs {
    static let showTipsKey = "showTips"
    static let defaultModeKey = "defaultMode"
    static let onboardedKey = "onboarded"
    static let menuBarKey = "showMenuBarExtra"
    static let checkUpdatesKey = "checkForUpdates"
    static let lastUpdateCheckKey = "lastUpdateCheck"
}
