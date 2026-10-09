import ArgumentParser
import Foundation
import MitosisCore

@main
struct MitosisCLI: AsyncParsableCommand {
    static let configuration = CommandConfiguration(
        commandName: "mitosis",
        abstract: "Run separate copies of your Mac apps.",
        version: Mitosis.version,
        subcommands: [List.self, Doctor.self, Clone.self, Stats.self, Clean.self, Refresh.self, Delete.self, Open.self]
    )
}

struct CLIError: Error, CustomStringConvertible {
    let description: String
}

enum Context {
    static func environment() -> MitosisEnvironment {
        let vars = ProcessInfo.processInfo.environment
        let exeDir = (Bundle.main.executableURL ?? URL(fileURLWithPath: CommandLine.arguments[0]))
            .resolvingSymlinksInPath().deletingLastPathComponent()
        let stub = vars["MITOSIS_STUB"].map { URL(fileURLWithPath: $0) } ?? exeDir.appendingPathComponent("LaunchStub")
        var env = MitosisEnvironment.standard(stubBinary: stub, root: vars["MITOSIS_HOME"].map { URL(fileURLWithPath: $0) })
        if let trash = vars["MITOSIS_TRASH_DIR"] { env.trashOverride = URL(fileURLWithPath: trash) }
        if vars["MITOSIS_NO_LSREGISTER"] == "1" { env.registerWithLaunchServices = false }
        return env
    }

    static func builder() throws -> CloneBuilder {
        CloneBuilder(environment: environment(), profiles: try ProfileStore.bundled())
    }

    static func entries() throws -> [RegistryEntry] {
        let env = environment()
        return try CloneRegistry(fileURL: env.registryFile).loadOrRebuild(clonesDir: env.clonesDir)
    }

    static func findClone(_ query: String) throws -> RegistryEntry {
        let all = try entries()
        if let hit = all.first(where: { $0.manifest.name.lowercased() == query.lowercased() }) { return hit }
        if query.count >= 4, let hit = all.first(where: { $0.id.uuidString.lowercased().hasPrefix(query.lowercased()) }) { return hit }
        throw CLIError(description: CloneError.notFound(query).description)
    }

    static func resolveApp(_ query: String) throws -> URL {
        guard let url = AppResolver.resolve(query) else {
            throw CLIError(description: "Couldn't find an app called \"\(query)\".")
        }
        return url
    }

    static func describe(_ status: CloneStatus) -> String {
        switch status {
        case .upToDate: return "ok"
        case .updateAvailable(let v): return "update available (\(v))"
        case .originalMissing: return "original missing"
        }
    }

    static func bytes(_ n: Int64) -> String {
        ByteCountFormatter.string(fromByteCount: n, countStyle: .file)
    }
}

/// Runs a throwing body and turns any error into a clean one-line message.
func friendly<T>(_ body: () throws -> T) throws -> T {
    do { return try body() } catch let e as CLIError { throw e } catch { throw CLIError(description: String(describing: error)) }
}

struct List: ParsableCommand {
    static let configuration = CommandConfiguration(abstract: "List your clones.")

    func run() throws {
        try friendly {
            let entries = try Context.entries()
            guard !entries.isEmpty else {
                print("No clones yet. Try: mitosis clone Slack --label Work")
                return
            }
            let maintenance = CloneMaintenance(builder: try Context.builder())
            for e in entries.sorted(by: { $0.manifest.name.localizedStandardCompare($1.manifest.name) == .orderedAscending }) {
                print("\(e.manifest.name)\t\(e.manifest.source.bundleID)\t\(e.manifest.mode.rawValue)\t\(Context.describe(maintenance.status(of: e)))")
            }
        }
    }
}

struct Doctor: ParsableCommand {
    static let configuration = CommandConfiguration(abstract: "Check whether an app can be cloned and how.")
    @Argument(help: "App name, bundle ID, or path.") var app: String

    func run() throws {
        try friendly {
            let url = try Context.resolveApp(app)
            let info = try AppInspector().inspect(url)
            let decision = ModeDecider(profiles: try ProfileStore.bundled()).decide(for: info)
            print("\(info.name) \(info.version) (\(info.bundleID))")
            print("Path: \(url.path)")
            print("Mode: \(decision.mode?.rawValue ?? "none") · Support: \(decision.support.rawValue)")
            for reason in decision.reasons { print("Why: \(reason)") }
            print("Frameworks: \(info.frameworks.isEmpty ? "none" : info.frameworks.map(\.rawValue).sorted().joined(separator: ", "))")
            print("Sandboxed: \(info.isSandboxed ? "yes" : "no") · App Store receipt: \(info.hasMASReceipt ? "yes" : "no")")
            print("Restricted entitlements: \(info.restrictedEntitlements.isEmpty ? "none" : info.restrictedEntitlements.joined(separator: ", "))")
        }
    }
}

struct Clone: AsyncParsableCommand {
    static let configuration = CommandConfiguration(abstract: "Create a clone of an app, e.g. `mitosis clone Slack --label Work` → \"Slack (Work)\".")
    @Argument(help: "App name, bundle ID, or path.") var app: String
    @Option(help: "Label shown in brackets after the app name, e.g. Work → \"Slack (Work)\".") var label: String?
    @Option(help: "Full custom name instead of a label.") var name: String?
    @Option(help: "Badge text (1–2 characters). Defaults to the first letter of the label or name.") var badge: String?
    @Option(help: "Badge color: blue, green, orange, red, purple, pink, yellow, gray, or #RRGGBB.") var color: String = "blue"
    @Option(help: "auto, identity, or fallback.") var mode: String = "auto"
    @Flag(help: "Don't open the clone afterwards to check that it starts.") var noVerify = false
    @Flag(help: "Allow a full copy when the app is on a different drive than ~/Applications.") var allowFullCopy = false

    func run() async throws {
        let request = try friendly { () throws -> CloneRequest in
            guard (label == nil) != (name == nil) else {
                throw CLIError(description: "Use --label (e.g. --label Work → \"Slack (Work)\") or --name, but not both.")
            }
            let url = try Context.resolveApp(app)
            let hex = Badge.presets[color.lowercased()] ?? (color.hasPrefix("#") && color.count == 7 ? color.uppercased() : nil)
            guard let hex else { throw CLIError(description: "Unknown color \"\(color)\".") }
            let modeOverride: CloneMode?
            switch mode.lowercased() {
            case "auto": modeOverride = nil
            case "identity": modeOverride = .identity
            case "fallback": modeOverride = .fallback
            default: throw CLIError(description: "Unknown mode \"\(mode)\". Use auto, identity, or fallback.")
            }
            let preflight = try Context.builder().preflight(source: url)
            if preflight.willCopyFully && !allowFullCopy {
                throw CLIError(description: "\(preflight.info.name) is on a different drive, so the clone would be a full copy (\(Context.bytes(preflight.copyBytes))) instead of sharing disk space. Re-run with --allow-full-copy to continue.")
            }
            let finalName = label.map { CloneName.compose(app: preflight.info.name, label: $0) } ?? name ?? ""
            let initialSource = (label ?? name ?? "").trimmingCharacters(in: .whitespaces)
            let text = String((badge ?? initialSource.first.map(String.init) ?? "?").prefix(2)).uppercased()
            return CloneRequest(source: url, name: finalName, badge: Badge(text: text, color: hex), modeOverride: modeOverride)
        }
        let builder = try friendly { try Context.builder() }
        let entry: RegistryEntry
        do {
            if noVerify {
                entry = try builder.create(request)
            } else {
                print("Creating \"\(request.name)\" and checking that it starts…")
                entry = try await CloneWorkflow(builder: builder).createVerified(request)
            }
        } catch {
            throw CLIError(description: String(describing: error))
        }
        print("Created \"\(entry.manifest.name)\" (\(entry.manifest.mode.rawValue) mode) at \(entry.bundlePath)")
    }
}

struct Stats: ParsableCommand {
    static let configuration = CommandConfiguration(abstract: "Show disk, data, RAM and CPU used by a clone.")
    @Argument(help: "Clone name.") var clone: String

    func run() throws {
        try friendly {
            let entry = try Context.findClone(clone)
            let s = StatsCollector.stats(for: entry)
            let original = URL(fileURLWithPath: entry.manifest.source.path).deletingPathExtension().lastPathComponent
            print(entry.manifest.name)
            print("Extra disk: about \(Context.bytes(s.extraDiskBytes)) (the rest is shared with \(original))")
            print("App size: \(Context.bytes(s.appBytes))")
            print("Data: \(Context.bytes(s.dataBytes))")
            if let u = s.usage {
                print("Running: \(u.processCount) process\(u.processCount == 1 ? "" : "es") · \(String(format: "%.1f", u.cpuPercent))% CPU · \(Context.bytes(u.memoryBytes))")
            } else {
                print("Not running")
            }
        }
    }
}

struct Clean: ParsableCommand {
    static let configuration = CommandConfiguration(abstract: "Delete a clone's caches (logins and settings are kept).")
    @Argument(help: "Clone name.") var clone: String

    func run() throws {
        try friendly {
            let entry = try Context.findClone(clone)
            let freed = try StatsCollector.cleanCaches(dataPath: URL(fileURLWithPath: entry.manifest.dataPath))
            print("Freed \(Context.bytes(freed)) of caches from \"\(entry.manifest.name)\".")
        }
    }
}

struct Refresh: AsyncParsableCommand {
    static let configuration = CommandConfiguration(abstract: "Rebuild clones from the current version of their original app.")
    @Argument(help: "Clone name.") var clone: String?
    @Flag(help: "Refresh every clone that has an update.") var all = false
    @Flag(help: "Don't open the refreshed clone to check that it starts.") var noVerify = false

    func run() async throws {
        let builder = try friendly { try Context.builder() }
        let targets = try friendly { () throws -> [RegistryEntry] in
            let maintenance = CloneMaintenance(builder: builder)
            let targets: [RegistryEntry]
            if all {
                targets = try Context.entries().filter {
                    if case .updateAvailable = maintenance.status(of: $0) { return true } else { return false }
                }
            } else {
                guard let clone else { throw CLIError(description: "Name a clone or use --all.") }
                targets = [try Context.findClone(clone)]
            }
            return targets
        }
        if targets.isEmpty { print("Everything is up to date.") }
        for t in targets {
            do {
                let updated = noVerify ? try CloneMaintenance(builder: builder).refresh(t)
                                       : try await CloneWorkflow(builder: builder).refreshVerified(t)
                print("Refreshed \"\(updated.manifest.name)\" to \(updated.manifest.source.version)")
            } catch {
                throw CLIError(description: String(describing: error))
            }
        }
    }
}

struct Delete: ParsableCommand {
    static let configuration = CommandConfiguration(abstract: "Move a clone to the Trash.")
    @Argument(help: "Clone name.") var clone: String
    @Flag(help: "Also move the clone's data (logins, settings) to the Trash.") var deleteData = false

    func run() throws {
        try friendly {
            let entry = try Context.findClone(clone)
            try CloneMaintenance(builder: try Context.builder()).delete(entry, deleteData: deleteData)
            print("Moved \"\(entry.manifest.name)\" to the Trash\(deleteData ? " with its data" : " (data kept)").")
        }
    }
}

struct Open: ParsableCommand {
    static let configuration = CommandConfiguration(abstract: "Open a clone.")
    @Argument(help: "Clone name.") var clone: String

    func run() throws {
        try friendly {
            try CloneLauncher.open(try Context.findClone(clone).bundleURL)
        }
    }
}
