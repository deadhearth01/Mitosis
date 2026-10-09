import Foundation
import MitosisCore

enum SidebarItem: Hashable, Sendable {
    case all
    case running
    case app(bundleID: String)
}

/// One source app in the sidebar's "By App" section.
struct AppGroup: Identifiable, Hashable, Sendable {
    var bundleID: String
    var name: String
    var sourcePath: String
    var count: Int
    var id: String { bundleID }
}

/// Pure filtering and grouping for the main window.
enum CloneLibrary {
    static func appName(for entry: RegistryEntry) -> String {
        URL(fileURLWithPath: entry.manifest.source.path).deletingPathExtension().lastPathComponent
    }

    /// The label part of "<App> (<Label>)", or the whole name for clones named some other way.
    static func label(for entry: RegistryEntry) -> String {
        let name = entry.manifest.name, app = appName(for: entry)
        guard name.hasPrefix(app + " ("), name.hasSuffix(")") else { return name }
        let label = name.dropFirst(app.count + 2).dropLast()
        return label.isEmpty ? name : String(label)
    }

    static func groups(_ entries: [RegistryEntry]) -> [AppGroup] {
        var byID: [String: AppGroup] = [:]
        for e in entries {
            let id = e.manifest.source.bundleID
            byID[id, default: AppGroup(bundleID: id, name: appName(for: e), sourcePath: e.manifest.source.path, count: 0)].count += 1
        }
        return byID.values.sorted { $0.name.localizedStandardCompare($1.name) == .orderedAscending }
    }

    static func filter(_ entries: [RegistryEntry], sidebar: SidebarItem, running: Set<UUID>, search: String) -> [RegistryEntry] {
        let query = search.trimmingCharacters(in: .whitespacesAndNewlines)
        return entries
            .filter { e in
                switch sidebar {
                case .all: return true
                case .running: return running.contains(e.id)
                case .app(let bundleID): return e.manifest.source.bundleID == bundleID
                }
            }
            .filter { e in
                query.isEmpty
                    || e.manifest.name.range(of: query, options: [.caseInsensitive, .diacriticInsensitive]) != nil
                    || appName(for: e).range(of: query, options: [.caseInsensitive, .diacriticInsensitive]) != nil
            }
            .sorted { $0.manifest.name.localizedStandardCompare($1.manifest.name) == .orderedAscending }
    }
}
