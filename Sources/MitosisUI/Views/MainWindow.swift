import AppKit
import MitosisCore
import SwiftUI

struct MainWindow: View {
    @Bindable var model: AppModel
    @State private var columns: NavigationSplitViewVisibility = .all
    @AppStorage(Settings.onboardedKey) private var onboarded = false
    @State private var showOnboarding = false

    var body: some View {
        NavigationSplitView(columnVisibility: $columns) {
            SidebarView(model: model)
                .navigationSplitViewColumnWidth(min: 180, ideal: 210, max: 300)
        } detail: {
            detail
                .inspector(isPresented: $model.showInspector) {
                    InspectorView(model: model)
                        .inspectorColumnWidth(min: 270, ideal: 300, max: 420)
                }
        }
        .navigationTitle(title)
        .navigationSubtitle(subtitle)
        .searchable(text: $model.search, placement: .toolbar, prompt: "Search clones")
        .toolbar {
            ToolbarItem(placement: .primaryAction) {
                Button {
                    model.startNewClone()
                } label: {
                    Label("New Clone", systemImage: "plus")
                }
                .help("Create a clone (⌘N)")
            }
            ToolbarItem(placement: .primaryAction) {
                Button {
                    model.showInspector.toggle()
                } label: {
                    Label("Inspector", systemImage: "sidebar.trailing")
                }
                .help("Show or hide details (⌘I)")
                .disabled(model.entries.isEmpty)
            }
        }
        .dropDestination(for: URL.self) { urls, _ in
            model.handleDrop(urls)
            return true
        }
        .alert(promptTitle, isPresented: promptShown, presenting: model.prompt) { prompt in
            promptActions(prompt)
        } message: { prompt in
            Text(promptMessage(prompt))
        }
        .sheet(item: $model.newClone) { session in
            NewCloneSheet(model: session) { model.newClone = nil }
        }
        .sheet(item: $model.restyling) { entry in
            RestyleSheet(model: model, entry: entry)
        }
        .sheet(item: $model.failure) { failure in
            FailureView(title: failure.title, report: failure.report) { model.failure = nil }
        }
        .sheet(isPresented: $showOnboarding) {
            OnboardingView { startFirstClone in
                onboarded = true
                showOnboarding = false
                if startFirstClone {
                    Task { try? await Task.sleep(for: .milliseconds(400)); model.startNewClone() }
                }
            }
            .tint(Brand.accent)
            .interactiveDismissDisabled()
        }
        .onAppear {
            columns = model.entries.isEmpty ? .detailOnly : .all
            if !onboarded && model.services != nil { showOnboarding = true }
        }
        .onChange(of: model.entries.isEmpty) { _, empty in columns = empty ? .detailOnly : .all }
        .onReceive(NotificationCenter.default.publisher(for: NSApplication.didBecomeActiveNotification)) { _ in
            model.refreshStatuses()
            model.updateRunning()
        }
    }

    @ViewBuilder private var detail: some View {
        if let error = model.loadError {
            ContentUnavailableView("Couldn't load your clones", systemImage: "exclamationmark.triangle", description: Text(error))
        } else if model.entries.isEmpty {
            EmptyStateView { model.startNewClone() }
        } else if model.visibleEntries.isEmpty {
            if !model.search.trimmingCharacters(in: .whitespaces).isEmpty {
                ContentUnavailableView.search(text: model.search)
            } else {
                ContentUnavailableView("Nothing running", systemImage: "play.circle",
                                       description: Text("Clones you open show up here."))
            }
        } else {
            CloneGridView(model: model)
        }
    }

    private var title: String {
        switch model.sidebar {
        case .all: return "All Clones"
        case .running: return "Running"
        case .app(let id): return model.groups.first { $0.bundleID == id }?.name ?? "Mitosis"
        }
    }

    private var subtitle: String {
        let n = model.visibleEntries.count
        return model.entries.isEmpty ? "" : (n == 1 ? "1 clone" : "\(n) clones")
    }

    // MARK: Prompts

    private var promptShown: Binding<Bool> {
        Binding(get: { model.prompt != nil }, set: { if !$0 { model.prompt = nil } })
    }

    private var promptTitle: String {
        switch model.prompt {
        case .quitFirst(let e, _)?: return "Quit \(e.manifest.name) first?"
        case .confirmDelete(let e)?: return "Delete \(e.manifest.name)?"
        case .confirmClean(let e)?: return "Clean caches for \(e.manifest.name)?"
        case .message(let title, _)?: return title
        case nil: return ""
        }
    }

    private func promptMessage(_ prompt: AppPrompt) -> String {
        switch prompt {
        case .quitFirst(let e, let action):
            let then: String
            switch action {
            case .refresh: then = "rebuild it from the current \(CloneLibrary.appName(for: e)). Your logins and data stay."
            case .restyle: then = "update its badge."
            case .delete: then = "move it to the Trash."
            case .clean: then = "clean its caches."
            }
            return "\(e.manifest.name) is open. Mitosis will ask it to quit, then \(then)"
        case .confirmDelete:
            return "The clone goes to the Trash. Its logins and settings stay unless you also delete its data."
        case .confirmClean:
            return "This deletes temporary files the app can download again. Logins and settings stay."
        case .message(_, let text):
            return text
        }
    }

    @ViewBuilder private func promptActions(_ prompt: AppPrompt) -> some View {
        switch prompt {
        case .quitFirst(let e, let action):
            Button("Quit and \(action.verb)") { model.quitThenPerform(action, on: e) }
            Button("Cancel", role: .cancel) {}
        case .confirmDelete(let e):
            Button("Move to Trash") { model.request(.delete(deleteData: false), for: e) }
            Button("Move to Trash with Data", role: .destructive) { model.request(.delete(deleteData: true), for: e) }
            Button("Cancel", role: .cancel) {}
        case .confirmClean(let e):
            Button("Clean Caches") { model.request(.clean, for: e) }
            Button("Cancel", role: .cancel) {}
        case .message:
            Button("OK", role: .cancel) {}
        }
    }
}
