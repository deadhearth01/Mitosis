import MitosisCore
import SwiftUI

/// The sidebar: every clone, grouped by its original app, with its status. Selecting one opens its page.
struct CloneListView: View {
    @Bindable var model: AppModel

    var body: some View {
        List(selection: $model.selection) {
            ForEach(CloneLibrary.groups(model.visibleEntries)) { group in
                Section {
                    ForEach(model.visibleEntries.filter { $0.manifest.source.bundleID == group.bundleID }) { e in
                        CloneRow(entry: e, status: model.status(of: e), running: model.isRunning(e), busy: model.busy[e.id])
                            .tag(e.id)
                            .contextMenu { CloneMenu(model: model, entry: e) }
                    }
                } header: {
                    HStack(spacing: 6) {
                        AppIconView(path: group.sourcePath, size: 14)
                        Text(group.name)
                    }
                }
            }
        }
        .listStyle(.sidebar)
        .searchable(text: $model.search, placement: .sidebar, prompt: "Search")
        .onDeleteCommand {
            if let e = model.selectedEntry { model.requestDelete(e) }
        }
        .onKeyPress(.return) {
            guard let e = model.selectedEntry else { return .ignored }
            model.open(e)
            return .handled
        }
        .safeAreaInset(edge: .bottom, spacing: 0) {
            Button {
                model.startNewClone()
            } label: {
                Label("New Clone", systemImage: "plus.circle.fill")
                    .font(.callout.weight(.medium))
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.borderless)
            .padding(.horizontal, Brand.Space.m)
            .padding(.vertical, Brand.Space.m - 4)
            .help("Create a clone (⌘N)")
        }
    }
}

struct CloneRow: View {
    let entry: RegistryEntry
    let status: CloneStatus
    let running: Bool
    let busy: String?

    var body: some View {
        HStack(spacing: Brand.Space.m - 6) {
            CloneIconView(entry: entry, size: 30)
                .opacity(status == .originalMissing ? 0.55 : 1)
            VStack(alignment: .leading, spacing: 1) {
                Text(CloneLibrary.label(for: entry))
                    .lineLimit(1)
                StatusLabel(status: status, running: running, busy: busy)
            }
        }
        .padding(.vertical, 2)
        .help(entry.manifest.name)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(entry.manifest.name)
        .accessibilityValue(busy ?? StatusLabel.text(status: status, running: running))
    }
}

/// Context menu, More menu and Clone menu items for one clone.
struct CloneMenu: View {
    let model: AppModel
    let entry: RegistryEntry

    var body: some View {
        Button("Open") { model.open(entry) }
        Button("Refresh") { model.request(.refresh, for: entry) }
            .disabled(model.status(of: entry) == .originalMissing)
        Button("Edit Badge…") { model.restyling = entry }
            .disabled(model.status(of: entry) == .originalMissing)
        if model.status(of: entry) == .originalMissing {
            Button("Find App…") { model.findApp(for: entry) }
        }
        Divider()
        Button("Show in Finder") { model.reveal(entry) }
        Button("Show Original in Finder") { model.revealOriginal(entry) }
            .disabled(model.status(of: entry) == .originalMissing)
        Button("Show Data Folder") { model.revealData(entry) }
        Button("Clean Caches…") { model.requestClean(entry) }
        Divider()
        Button("Delete…", role: .destructive) { model.requestDelete(entry) }
    }
}
