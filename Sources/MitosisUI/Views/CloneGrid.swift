import MitosisCore
import SwiftUI

struct CloneGridView: View {
    @Bindable var model: AppModel
    @State private var columns = 4
    private let tileWidth: CGFloat = 148
    private let spacing: CGFloat = Brand.Space.m

    var body: some View {
        GeometryReader { proxy in
            ScrollView {
                LazyVGrid(columns: [GridItem(.adaptive(minimum: tileWidth, maximum: tileWidth), spacing: spacing)],
                          alignment: .leading, spacing: spacing) {
                    ForEach(model.visibleEntries) { e in
                        CloneTile(entry: e, status: model.status(of: e), running: model.isRunning(e),
                                  busy: model.busy[e.id], selected: model.selection == e.id)
                            .onTapGesture(count: 2) { model.open(e) }
                            .simultaneousGesture(TapGesture().onEnded { model.selection = e.id })
                            .contextMenu { CloneMenu(model: model, entry: e) }
                    }
                }
                .padding(Brand.Space.l)
                .frame(maxWidth: .infinity, alignment: .leading)
            }
            .contentShape(Rectangle())
            .onTapGesture { model.selection = nil }
            .onAppear { columns = columnCount(width: proxy.size.width) }
            .onChange(of: proxy.size.width) { _, w in columns = columnCount(width: w) }
        }
        .focusable()
        .focusEffectDisabled()
        .onKeyPress(.return) {
            guard let e = model.selectedEntry else { return .ignored }
            model.open(e)
            return .handled
        }
        .onMoveCommand(perform: move)
        .onDeleteCommand {
            if let e = model.selectedEntry { model.requestDelete(e) }
        }
    }

    private func columnCount(width: CGFloat) -> Int {
        max(1, Int((width - 2 * Brand.Space.l + spacing) / (tileWidth + spacing)))
    }

    private func move(_ direction: MoveCommandDirection) {
        let items = model.visibleEntries
        guard !items.isEmpty else { return }
        guard let current = model.selection, let index = items.firstIndex(where: { $0.id == current }) else {
            model.selection = items.first?.id
            return
        }
        var next = index
        switch direction {
        case .left: next = index - 1
        case .right: next = index + 1
        case .up: next = index - columns
        case .down: next = index + columns
        @unknown default: break
        }
        if items.indices.contains(next) { model.selection = items[next].id }
    }
}

/// Context menu and Clone menu items for one clone.
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
