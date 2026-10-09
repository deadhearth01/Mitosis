import MitosisCore
import SwiftUI

struct AppPickerView: View {
    @Bindable var model: NewCloneModel
    let close: () -> Void
    @AppStorage(Prefs.showTipsKey) private var showTips = true
    @FocusState private var searchFocused: Bool
    @State private var dropTargeted = false

    var body: some View {
        VStack(alignment: .leading, spacing: Brand.Space.m) {
            VStack(alignment: .leading, spacing: Brand.Space.xs) {
                Text("New Clone")
                    .font(.title2.weight(.semibold))
                Text("Choose the app you want a separate copy of.")
                    .foregroundStyle(.secondary)
            }
            HStack(spacing: Brand.Space.m - 4) {
                TextField("Search apps", text: $model.query)
                    .textFieldStyle(.roundedBorder)
                    .focused($searchFocused)
                Picker("Show", selection: $model.filter) {
                    ForEach(NewCloneModel.Filter.allCases) { Text($0.rawValue).tag($0) }
                }
                .pickerStyle(.segmented)
                .labelsHidden()
                .fixedSize()
            }
            list
            if let error = model.dropError {
                Label(error, systemImage: "exclamationmark.triangle.fill")
                    .foregroundStyle(.red)
                    .font(.callout)
            } else if showTips {
                TipRow(pose: .search, text: "Apps marked Works great get their own login, data, notifications, and Dock icon.")
            }
            HStack {
                Text(countText + "You can also drop an app here.")
                    .font(.callout)
                    .foregroundStyle(.secondary)
                Spacer()
                Button("Cancel", role: .cancel, action: close)
                    .keyboardShortcut(.cancelAction)
                Button("Continue") { continueWithSelection() }
                    .keyboardShortcut(.defaultAction)
                    .disabled(model.selectedApp.map { !model.canChoose($0) } ?? true || model.inspecting)
            }
        }
        .padding(Brand.Space.l)
        .onAppear { searchFocused = true }
        .dropDestination(for: URL.self) { urls, _ in
            guard let url = urls.first else { return false }
            Task { await model.choose(url: url) }
            return true
        } isTargeted: { dropTargeted = $0 }
        .overlay {
            if dropTargeted {
                RoundedRectangle(cornerRadius: Brand.cardRadius, style: .continuous)
                    .strokeBorder(Brand.accent, style: StrokeStyle(lineWidth: 2, dash: [6, 4]))
                    .padding(Brand.Space.s)
                    .allowsHitTesting(false)
            }
        }
    }

    @ViewBuilder private var list: some View {
        List(selection: $model.selectedID) {
            ForEach(model.visibleApps) { app in
                AppRow(app: app, decision: model.support[app.id])
                    .tag(app.id)
                    .selectionDisabled(!model.canChoose(app))
                    .listRowSeparator(.hidden)
            }
        }
        .listStyle(.bordered(alternatesRowBackgrounds: false))
        .contextMenu(forSelectionType: String.self, menu: { _ in }, primaryAction: { _ in continueWithSelection() })
        .overlay {
            if model.loadingApps {
                ProgressView("Finding your apps…")
            } else if model.visibleApps.isEmpty && !model.apps.isEmpty {
                ContentUnavailableView.search(text: model.query)
            } else if model.inspecting {
                ProgressView().controlSize(.small)
            }
        }
    }

    private var countText: String {
        guard !model.loadingApps, !model.apps.isEmpty else { return "" }
        let shown = model.visibleApps.count
        return shown == model.apps.count ? "\(shown) apps. " : "\(shown) of \(model.apps.count) apps. "
    }

    private func continueWithSelection() {
        guard let app = model.selectedApp, model.canChoose(app) else { return }
        Task { await model.choose(app) }
    }
}

struct AppRow: View {
    let app: CatalogApp
    let decision: ModeDecision?

    var body: some View {
        HStack(spacing: Brand.Space.m - 4) {
            AppIconView(path: app.url.path, size: 26)
            Text(app.name)
                .lineLimit(1)
            if !app.version.isEmpty {
                Text(app.version)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }
            Spacer()
            if let decision {
                SupportCapsule(support: decision.support)
                    .help(decision.reasons.joined(separator: " "))
            } else {
                ProgressView().controlSize(.small)
                    .accessibilityLabel("Checking")
            }
        }
        .padding(.vertical, 1)
        .opacity(decision?.support == .unsupported ? 0.5 : 1)
        .accessibilityElement(children: .combine)
    }
}
