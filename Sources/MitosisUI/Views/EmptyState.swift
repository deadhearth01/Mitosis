import SwiftUI

struct EmptyStateView: View {
    let create: () -> Void

    var body: some View {
        VStack(spacing: Brand.Space.m) {
            MascotView(pose: .empty, size: 168)
            VStack(spacing: Brand.Space.s) {
                Text("No clones yet")
                    .font(.title2.weight(.semibold))
                Text("Choose an app to make your first one.")
                    .foregroundStyle(.secondary)
            }
            Button(action: create) {
                Text("Create Clone").padding(.horizontal, Brand.Space.s)
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.large)
            Text("or drag an app here")
                .font(.callout)
                .foregroundStyle(.tertiary)
        }
        .padding(Brand.Space.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}
