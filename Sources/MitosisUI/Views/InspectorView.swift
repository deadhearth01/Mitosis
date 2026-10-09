import MitosisCore
import SwiftUI

struct InspectorView: View {
    @Bindable var model: AppModel

    var body: some View {
        if let e = model.selectedEntry {
            CloneDetails(model: model, entry: e)
                .id(e.id)
        } else {
            ContentUnavailableView("No clone selected", systemImage: "square.dashed",
                                   description: Text("Select a clone to see its details and stats."))
        }
    }
}

struct CloneDetails: View {
    let model: AppModel
    let entry: RegistryEntry
    @State private var stats: CloneStats?
    @State private var usage: CloneUsage?

    private var status: CloneStatus { model.status(of: entry) }
    private var running: Bool { model.isRunning(entry) }
    private var appName: String { CloneLibrary.appName(for: entry) }
    private var busy: Bool { model.busy[entry.id] != nil }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Brand.Space.l) {
                header
                notice
                usageSection
                originalSection
                cloneSection
            }
            .padding(Brand.Space.m)
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .task(id: "\(entry.id)-\(entry.manifest.refreshedAt.timeIntervalSince1970)") { await loadStats() }
        .task(id: running) { await pollUsage() }
    }

    // MARK: Sections

    private var header: some View {
        VStack(alignment: .leading, spacing: Brand.Space.m) {
            HStack(spacing: Brand.Space.m) {
                CloneIconView(entry: entry, size: 56)
                VStack(alignment: .leading, spacing: Brand.Space.xs) {
                    Text(entry.manifest.name)
                        .font(.title3.weight(.semibold))
                        .lineLimit(2)
                        .textSelection(.enabled)
                    StatusLabel(status: status, running: running, busy: model.busy[entry.id])
                }
            }
            HStack(spacing: Brand.Space.s) {
                Button("Open") { model.open(entry) }
                    .buttonStyle(.borderedProminent)
                    .disabled(busy || status == .originalMissing)
                Button("Edit Badge…") { model.restyling = entry }
                    .disabled(busy || status == .originalMissing)
                Menu {
                    CloneMenu(model: model, entry: entry)
                } label: {
                    Label("More", systemImage: "ellipsis")
                }
                .menuStyle(.button)
                .menuIndicator(.hidden)
                .fixedSize()
                .help("More actions")
                .accessibilityLabel("More actions")
            }
            .controlSize(.regular)
        }
    }

    @ViewBuilder private var notice: some View {
        switch status {
        case .updateAvailable(let version):
            NoticeBox(symbol: "arrow.triangle.2.circlepath", tint: .orange,
                      text: "\(appName) \(version) is installed. Refresh this clone to use it. Logins and data stay.",
                      button: "Refresh") { model.request(.refresh, for: entry) }
                .disabled(busy)
        case .originalMissing:
            NoticeBox(symbol: "exclamationmark.triangle.fill", tint: .red,
                      text: "\(appName) isn't where it was. Show Mitosis where it is now to use this clone again.",
                      button: "Find App…") { model.findApp(for: entry) }
        case .upToDate:
            EmptyView()
        }
    }

    private var usageSection: some View {
        InspectorSection("Usage") {
            if let stats {
                Row("Extra disk", Format.bytes(stats.extraDiskBytes), note: diskNote)
                Row("Data", Format.bytes(stats.dataBytes), note: "Logins, settings and caches")
            } else {
                HStack(spacing: Brand.Space.s) {
                    ProgressView().controlSize(.small)
                    Text("Measuring…").foregroundStyle(.secondary)
                }
            }
            if running, let usage {
                Row("Memory", Format.bytes(usage.memoryBytes),
                    note: usage.processCount == 1 ? "1 process" : "\(usage.processCount) processes")
                Row("CPU", usage.cpuPercent < 1 ? "Under 1%" : String(format: "%.0f%%", usage.cpuPercent), note: nil)
            } else if !running {
                Text("Open the clone to see its memory and CPU use.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Button("Clean Caches…") { model.requestClean(entry) }
                .disabled(busy)
                .padding(.top, Brand.Space.xs)
        }
    }

    private var diskNote: String {
        if entry.manifest.mode == .fallback { return "A small shortcut that opens \(appName)" }
        let source = URL(fileURLWithPath: entry.manifest.source.path)
        if FileManager.default.fileExists(atPath: source.path), !FileCloner.isSameVolume(entry.bundleURL, source) {
            return "A full copy: \(appName) is on another drive"
        }
        return "The rest is shared with \(appName)"
    }

    private var originalSection: some View {
        InspectorSection("Original") {
            HStack(spacing: Brand.Space.s) {
                AppIconView(path: entry.manifest.source.path, size: 20)
                Text("\(appName) \(entry.manifest.source.version)")
                Spacer()
                Button("Show") { model.revealOriginal(entry) }
                    .controlSize(.small)
                    .disabled(status == .originalMissing)
            }
            PathText(entry.manifest.source.path)
        }
    }

    private var cloneSection: some View {
        InspectorSection("Clone") {
            Row("Mode", entry.manifest.mode.displayName, note: modeNote)
            Row("Created", entry.manifest.createdAt.formatted(date: .abbreviated, time: .shortened), note: nil)
            Row("Refreshed", entry.manifest.refreshedAt.formatted(.relative(presentation: .named)), note: nil)
            HStack {
                Text("Data folder").foregroundStyle(.secondary)
                Spacer()
                Button("Show") { model.revealData(entry) }
                    .controlSize(.small)
            }
            PathText(entry.manifest.dataPath)
        }
    }

    private var modeNote: String {
        switch entry.manifest.mode {
        case .identity: return "Own login, data, notifications and Dock icon"
        case .fallback: return "Own login and data; uses \(appName)'s Dock icon and notifications"
        }
    }

    // MARK: Loading

    private func loadStats() async {
        let e = entry
        stats = try? await offMain { StatsCollector.stats(for: e) }
        usage = stats?.usage
    }

    /// Live memory/CPU every 2 s, only while this clone is running and the inspector shows it.
    private func pollUsage() async {
        let e = entry
        while running, !Task.isCancelled {
            usage = try? await offMain { StatsCollector.usage(for: e) }
            try? await Task.sleep(for: .seconds(2))
        }
    }
}

// MARK: Building blocks

struct InspectorSection<Content: View>: View {
    let title: String
    let content: Content

    init(_ title: String, @ViewBuilder content: () -> Content) {
        self.title = title
        self.content = content()
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Brand.Space.s) {
            Text(title)
                .font(.headline)
            content
        }
    }
}

struct Row: View {
    let label: String
    let value: String
    let note: String?

    init(_ label: String, _ value: String, note: String?) {
        self.label = label
        self.value = value
        self.note = note
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack(alignment: .firstTextBaseline) {
                Text(label).foregroundStyle(.secondary)
                Spacer(minLength: Brand.Space.s)
                Text(value).monospacedDigit().multilineTextAlignment(.trailing)
            }
            if let note {
                Text(note)
                    .font(.caption)
                    .foregroundStyle(.tertiary)
            }
        }
        .accessibilityElement(children: .combine)
    }
}

struct PathText: View {
    let path: String

    init(_ path: String) { self.path = path }

    var body: some View {
        Text(path.replacingOccurrences(of: NSHomeDirectory(), with: "~"))
            .font(.caption)
            .foregroundStyle(.secondary)
            .lineLimit(2)
            .truncationMode(.middle)
            .textSelection(.enabled)
            .help(path)
    }
}

struct NoticeBox: View {
    let symbol: String
    let tint: Color
    let text: String
    let button: String
    let action: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Brand.Space.s) {
            HStack(alignment: .top, spacing: Brand.Space.s) {
                Image(systemName: symbol).foregroundStyle(tint)
                Text(text).fixedSize(horizontal: false, vertical: true)
            }
            Button(button, action: action)
                .controlSize(.small)
        }
        .padding(Brand.Space.m - 4)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(tint.opacity(0.10), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
    }
}
