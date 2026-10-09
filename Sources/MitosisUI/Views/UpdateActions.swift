import AppKit
import SwiftUI

/// "Update Now" and its progress, shared by the banner and Settings. Falls back to copying the install command when
/// Mitosis can't replace itself (for example a copy in a folder this user can't write to).
struct UpdateActions: View {
    let updates: UpdateChecker
    let version: String
    let releaseURL: URL
    @State private var copied = false

    var body: some View {
        HStack(spacing: Brand.Space.s) {
            switch updates.install {
            case .idle:
                Link("What's New", destination: releaseURL)
                if updates.canInstallInPlace {
                    Button("Update Now") { Task { await updates.installUpdate(version: version) } }
                        .buttonStyle(.borderedProminent)
                        .controlSize(.small)
                } else {
                    copyCommand
                }
            case .downloading:
                ProgressView().controlSize(.small)
                Text("Downloading…").foregroundStyle(.secondary)
            case .installing:
                ProgressView().controlSize(.small)
                Text("Installing… Mitosis will reopen.").foregroundStyle(.secondary)
            case .failed(let message):
                Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.orange)
                Text(message).foregroundStyle(.secondary).lineLimit(1).help(message)
                Button("Try Again") { Task { await updates.installUpdate(version: version) } }
                    .controlSize(.small)
                copyCommand
            }
        }
    }

    private var copyCommand: some View {
        Button(copied ? "Copied" : "Copy Update Command") {
            NSPasteboard.general.clearContents()
            NSPasteboard.general.setString(UpdateChecker.updateCommand, forType: .string)
            copied = true
        }
        .controlSize(.small)
        .help("Paste in Terminal to update")
    }
}
