import AppKit
import MitosisCore
import SwiftUI

struct WorkingView: View {
    let name: String
    let label: String

    var body: some View {
        VStack(spacing: Brand.Space.m) {
            MascotView(pose: .building, size: 140)
            Text("Creating \(name)…")
                .font(.title3.weight(.semibold))
            Text(label)
                .foregroundStyle(.secondary)
                .contentTransition(.opacity)
            ProgressView()
                .progressViewStyle(.linear)
                .frame(width: 280)
            Text(label == NewCloneModel.label(for: "launch check")
                 ? "Mitosis opens the clone once to make sure it starts."
                 : "This usually takes a few seconds.")
                .font(.caption)
                .foregroundStyle(.tertiary)
        }
        .padding(Brand.Space.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
    }
}

struct DoneView: View {
    let entry: RegistryEntry
    let opened: Bool
    let close: () -> Void
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var appeared = false

    var body: some View {
        VStack(spacing: Brand.Space.m) {
            MascotView(pose: .success, size: 150)
                .scaleEffect(appeared || reduceMotion ? 1 : 0.92)
                .opacity(appeared || reduceMotion ? 1 : 0.6)
            Text("\(entry.manifest.name) is ready.")
                .font(.title2.weight(.semibold))
                .multilineTextAlignment(.center)
            Text(opened
                 ? "It's open now with its own login and data. Sign in inside the clone to keep it separate."
                 : "It has its own login and data. Open it from the Mitosis window or your Applications folder.")
                .multilineTextAlignment(.center)
                .foregroundStyle(.secondary)
                .frame(maxWidth: 420)
            HStack(spacing: Brand.Space.m - 4) {
                Button("Show in Finder") { NSWorkspace.shared.activateFileViewerSelecting([entry.bundleURL]) }
                Button("Done", action: close)
                    .keyboardShortcut(.defaultAction)
            }
            .padding(.top, Brand.Space.s)
        }
        .padding(Brand.Space.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .onAppear { withAnimation(.spring(duration: 0.35)) { appeared = true } }
    }
}
