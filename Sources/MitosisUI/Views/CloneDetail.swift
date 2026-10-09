import MitosisCore
import SwiftUI

/// One clone's page: who it is, how it's doing, what it costs, and everything you can do with it.
struct CloneDetailView: View {
    let model: AppModel
    let entry: RegistryEntry
    @State private var stats: CloneStats?
    @State private var usage: CloneUsage?

    private var status: CloneStatus { model.status(of: entry) }
    private var running: Bool { model.isRunning(entry) }
    private var busy: String? { model.busy[entry.id] }
    private var appName: String { CloneLibrary.appName(for: entry) }
    private var missing: Bool { status == .originalMissing }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Brand.Space.l + 4) {
                header
                notice
                usageSection
                detailsSection
                maintenanceSection
            }
            .frame(maxWidth: 780, alignment: .leading)
            .padding(.horizontal, Brand.Space.xl)
            .padding(.vertical, Brand.Space.l + 4)
            .frame(maxWidth: .infinity)
        }
        .task(id: "\(entry.id)-\(entry.manifest.refreshedAt.timeIntervalSince1970)") { await loadStats() }
        .task(id: running) { await pollUsage() }
    }

    // MARK: Header

    private var header: some View {
        HStack(alignment: .center, spacing: Brand.Space.l) {
            CloneIconView(entry: entry, size: 104)
                .shadow(color: .black.opacity(0.12), radius: 8, y: 4)
                .opacity(missing ? 0.55 : 1)
            VStack(alignment: .leading, spacing: Brand.Space.s) {
                Text(entry.manifest.name)
                    .font(.largeTitle.weight(.bold))
                    .lineLimit(2)
                    .textSelection(.enabled)
                HStack(spacing: Brand.Space.s) {
                    StatusPill(status: status, running: running, busy: busy)
                    Text("Clone of \(appName) \(entry.manifest.source.version)")
                        .foregroundStyle(.secondary)
                }
                HStack(spacing: Brand.Space.s) {
                    Button {
                        model.open(entry)
                    } label: {
                        Label(running ? "Show" : "Open", systemImage: "arrow.up.forward.app")
                            .padding(.horizontal, 4)
                    }
                    .buttonStyle(.borderedProminent)
                    .disabled(busy != nil || missing)
                    Button("Edit Badge…") { model.restyling = entry }
                        .disabled(busy != nil || missing)
                    Menu {
                        CloneMenu(model: model, entry: entry)
                    } label: {
                        Image(systemName: "ellipsis")
                    }
                    .menuIndicator(.hidden)
                    .fixedSize()
                    .help("More actions")
                    .accessibilityLabel("More actions")
                }
                .controlSize(.large)
                .padding(.top, Brand.Space.xs)
            }
            Spacer(minLength: 0)
        }
    }

    @ViewBuilder private var notice: some View {
        switch status {
        case .updateAvailable(let version):
            if running {
                NoticeBox(symbol: "arrow.triangle.2.circlepath", tint: .orange,
                          text: "\(appName) \(version) is installed. \(model.autoUpdateEnabled ? "This clone updates after you quit it." : "Quit the clone, then refresh it.") Logins and data stay.",
                          button: "Quit and Update") { model.request(.refresh, for: entry) }
            } else if model.autoUpdateEnabled {
                NoticeBox(symbol: "arrow.triangle.2.circlepath", tint: .orange,
                          text: "\(appName) \(version) is installed. Mitosis is updating this clone. Logins and data stay.",
                          button: nil, action: {})
            } else {
                NoticeBox(symbol: "arrow.triangle.2.circlepath", tint: .orange,
                          text: "\(appName) \(version) is installed. Refresh this clone to use it. Logins and data stay.",
                          button: "Refresh") { model.request(.refresh, for: entry) }
                    .disabled(busy != nil)
            }
        case .originalMissing:
            NoticeBox(symbol: "exclamationmark.triangle.fill", tint: .red,
                      text: "\(appName) isn't where it was. Show Mitosis where it is now to use this clone again.",
                      button: "Find App…") { model.findApp(for: entry) }
        case .upToDate:
            EmptyView()
        }
    }

    // MARK: Usage

    private var usageSection: some View {
        VStack(alignment: .leading, spacing: Brand.Space.m - 4) {
            SectionTitle("Usage")
            LazyVGrid(columns: [GridItem(.adaptive(minimum: 160), spacing: Brand.Space.m - 4)], spacing: Brand.Space.m - 4) {
                StatTile(symbol: "internaldrive", tint: .blue, title: "Extra disk",
                         value: stats.map { Format.bytes($0.extraDiskBytes) }, caption: diskNote)
                StatTile(symbol: "folder", tint: .teal, title: "Data",
                         value: stats.map { Format.bytes($0.dataBytes) }, caption: "Logins, settings and caches")
                StatTile(symbol: "memorychip", tint: .purple, title: "Memory",
                         value: running ? usage.map { Format.bytes($0.memoryBytes) } : "—",
                         caption: running ? (usage.map { $0.processCount == 1 ? "1 process" : "\($0.processCount) processes" } ?? "Measuring…") : "Not running")
                StatTile(symbol: "cpu", tint: .orange, title: "CPU",
                         value: running ? usage.map { $0.cpuPercent < 1 ? "<1%" : String(format: "%.0f%%", $0.cpuPercent) } : "—",
                         caption: running ? "Right now" : "Not running")
            }
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

    // MARK: Details

    private var detailsSection: some View {
        VStack(alignment: .leading, spacing: Brand.Space.m - 4) {
            SectionTitle("Details")
            Card {
                DetailRow("Mode") {
                    VStack(alignment: .trailing, spacing: 2) {
                        Text(entry.manifest.mode.displayName)
                        Text(modeNote).font(.caption).foregroundStyle(.secondary)
                    }
                }
                Divider()
                DetailRow("Original") {
                    HStack(spacing: Brand.Space.s) {
                        PathText(entry.manifest.source.path)
                        Button("Show") { model.revealOriginal(entry) }
                            .controlSize(.small)
                            .disabled(missing)
                    }
                }
                Divider()
                DetailRow("Data folder") {
                    HStack(spacing: Brand.Space.s) {
                        PathText(entry.manifest.dataPath)
                        Button("Show") { model.revealData(entry) }
                            .controlSize(.small)
                    }
                }
                Divider()
                DetailRow("Created") { Text(entry.manifest.createdAt.formatted(date: .abbreviated, time: .shortened)) }
                Divider()
                DetailRow("Last updated") { Text(entry.manifest.refreshedAt.formatted(.relative(presentation: .named))) }
            }
        }
    }

    private var modeNote: String {
        switch entry.manifest.mode {
        case .identity: return "Own login, data, notifications and Dock icon"
        case .fallback: return "Own login and data; uses \(appName)'s Dock icon"
        }
    }

    // MARK: Maintenance

    private var maintenanceSection: some View {
        VStack(alignment: .leading, spacing: Brand.Space.m - 4) {
            SectionTitle("Maintenance")
            Card {
                ActionRow(title: "Clean caches",
                          text: "Delete temporary files the app can download again. Logins and settings stay.",
                          button: "Clean…") { model.requestClean(entry) }
                    .disabled(busy != nil)
                Divider()
                ActionRow(title: "Rebuild",
                          text: "Rebuild this clone from the current \(appName). Logins and data stay.",
                          button: "Refresh") { model.request(.refresh, for: entry) }
                    .disabled(busy != nil || missing)
                Divider()
                ActionRow(title: "Delete clone",
                          text: "Move it to the Trash. You choose whether its data goes too.",
                          button: "Delete…", destructive: true) { model.requestDelete(entry) }
                    .disabled(busy != nil)
            }
        }
    }

    // MARK: Loading

    private func loadStats() async {
        if let preview = model.previewStats[entry.id] {
            stats = preview
            usage = preview.usage
            return
        }
        let e = entry
        stats = try? await offMain { StatsCollector.stats(for: e) }
        usage = stats?.usage
    }

    /// Live memory/CPU every 2 s, only while this clone runs and its page is showing.
    private func pollUsage() async {
        guard model.services != nil else { return }
        let e = entry
        while running, !Task.isCancelled {
            usage = try? await offMain { StatsCollector.usage(for: e) }
            try? await Task.sleep(for: .seconds(2))
        }
    }
}

// MARK: Building blocks

struct SectionTitle: View {
    let text: String
    init(_ text: String) { self.text = text }
    var body: some View {
        Text(text)
            .font(.title3.weight(.semibold))
            .accessibilityAddTraits(.isHeader)
    }
}

struct Card<Content: View>: View {
    @ViewBuilder let content: Content
    var body: some View {
        VStack(spacing: 0) { content }
            .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: Brand.cardRadius, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: Brand.cardRadius, style: .continuous).strokeBorder(Color(nsColor: .separatorColor).opacity(0.6), lineWidth: 0.5))
    }
}

struct StatTile: View {
    let symbol: String
    let tint: Color
    let title: String
    let value: String?
    let caption: String

    var body: some View {
        VStack(alignment: .leading, spacing: Brand.Space.s) {
            HStack {
                Image(systemName: symbol)
                    .font(.title3)
                    .foregroundStyle(tint)
                    .frame(width: 30, height: 30)
                    .background(tint.opacity(0.12), in: RoundedRectangle(cornerRadius: 8, style: .continuous))
                Spacer()
            }
            Group {
                if let value {
                    Text(value)
                } else {
                    ProgressView().controlSize(.small)
                }
            }
            .font(.system(.title2, design: .rounded).weight(.semibold))
            .monospacedDigit()
            .frame(height: 30, alignment: .leading)
            VStack(alignment: .leading, spacing: 2) {
                Text(title).font(.callout.weight(.medium))
                Text(caption)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(2, reservesSpace: true)
            }
        }
        .padding(Brand.Space.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous).strokeBorder(Color(nsColor: .separatorColor).opacity(0.6), lineWidth: 0.5))
        .accessibilityElement(children: .combine)
    }
}

struct StatusPill: View {
    let status: CloneStatus
    let running: Bool
    let busy: String?

    var body: some View {
        HStack(spacing: 5) {
            if let busy {
                ProgressView().controlSize(.mini)
                Text(busy)
            } else {
                Image(systemName: symbol)
                    .font(.caption2.weight(.bold))
                Text(StatusLabel.text(status: status, running: running))
            }
        }
        .font(.caption.weight(.semibold))
        .foregroundStyle(busy != nil ? Brand.accent : tint)
        .padding(.horizontal, 9)
        .padding(.vertical, 4)
        .background((busy != nil ? Brand.accent : tint).opacity(0.13), in: Capsule())
    }

    private var symbol: String {
        switch status {
        case .originalMissing: return "exclamationmark.triangle.fill"
        case .updateAvailable: return "arrow.triangle.2.circlepath"
        case .upToDate: return running ? "circle.fill" : "moon.zzz.fill"
        }
    }

    private var tint: Color {
        switch status {
        case .originalMissing: return .red
        case .updateAvailable: return .orange
        case .upToDate: return running ? .green : .secondary
        }
    }
}

struct DetailRow<Value: View>: View {
    let label: String
    @ViewBuilder let value: Value

    init(_ label: String, @ViewBuilder value: () -> Value) {
        self.label = label
        self.value = value()
    }

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: Brand.Space.m) {
            Text(label).foregroundStyle(.secondary)
            Spacer(minLength: Brand.Space.m)
            value.multilineTextAlignment(.trailing)
        }
        .padding(.horizontal, Brand.Space.m)
        .padding(.vertical, Brand.Space.m - 5)
    }
}

struct ActionRow: View {
    let title: String
    let text: String
    let button: String
    var destructive = false
    let action: () -> Void

    var body: some View {
        HStack(spacing: Brand.Space.m) {
            VStack(alignment: .leading, spacing: 2) {
                Text(title).font(.body.weight(.medium))
                Text(text).font(.callout).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
            }
            Spacer(minLength: Brand.Space.m)
            Button(button, role: destructive ? .destructive : nil, action: action)
                .foregroundStyle(destructive ? .red : .primary)
        }
        .padding(.horizontal, Brand.Space.m)
        .padding(.vertical, Brand.Space.m - 4)
    }
}

struct PathText: View {
    let path: String

    init(_ path: String) { self.path = path }

    var body: some View {
        Text(path.replacingOccurrences(of: NSHomeDirectory(), with: "~"))
            .font(.callout)
            .lineLimit(1)
            .truncationMode(.middle)
            .textSelection(.enabled)
            .help(path)
    }
}

struct NoticeBox: View {
    let symbol: String
    let tint: Color
    let text: String
    let button: String?
    let action: () -> Void

    var body: some View {
        HStack(alignment: .center, spacing: Brand.Space.m - 4) {
            Image(systemName: symbol)
                .font(.title3)
                .foregroundStyle(tint)
            Text(text).fixedSize(horizontal: false, vertical: true)
            Spacer(minLength: Brand.Space.s)
            if let button {
                Button(button, action: action)
            }
        }
        .padding(Brand.Space.m - 2)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(tint.opacity(0.10), in: RoundedRectangle(cornerRadius: Brand.cardRadius, style: .continuous))
    }
}
