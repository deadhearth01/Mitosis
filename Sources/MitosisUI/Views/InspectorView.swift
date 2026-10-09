import SwiftUI

struct InspectorView: View {
    @Bindable var model: AppModel

    var body: some View {
        Text("Select a clone").foregroundStyle(.secondary)
    }
}
