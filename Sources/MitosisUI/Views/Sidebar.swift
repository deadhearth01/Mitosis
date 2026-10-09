import SwiftUI

struct SidebarView: View {
    @Bindable var model: AppModel

    var body: some View {
        List(selection: Binding(get: { model.sidebar }, set: { if let v = $0 { model.sidebar = v } })) {
            Section("Library") {
                Label("All Clones", systemImage: "square.grid.2x2")
                    .badge(model.entries.count)
                    .tag(SidebarItem.all)
                Label("Running", systemImage: "play.circle")
                    .badge(model.running.count)
                    .tag(SidebarItem.running)
            }
            if !model.groups.isEmpty {
                Section("By App") {
                    ForEach(model.groups) { group in
                        Label {
                            Text(group.name)
                        } icon: {
                            AppIconView(path: group.sourcePath, size: 16)
                        }
                        .badge(group.count)
                        .tag(SidebarItem.app(bundleID: group.bundleID))
                    }
                }
            }
        }
        .listStyle(.sidebar)
    }
}
