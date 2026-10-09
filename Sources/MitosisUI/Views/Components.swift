import AppKit
import MitosisCore
import SwiftUI

/// Icons are read once per path (and per clone refresh, since a restyle redraws the badge).
@MainActor
enum IconCache {
    private static var images: [String: NSImage] = [:]

    static func appIcon(path: String) -> NSImage {
        if let hit = images[path] { return hit }
        let image = NSWorkspace.shared.icon(forFile: path)
        images[path] = image
        return image
    }

    /// The clone's own badged icon (Contents/Resources/MitosisIcon.icns); Finder's icon as a fallback.
    static func cloneIcon(_ e: RegistryEntry) -> NSImage {
        let key = e.bundlePath + "#\(e.manifest.refreshedAt.timeIntervalSince1970)#\(e.manifest.badge.text)\(e.manifest.badge.color)"
        if let hit = images[key] { return hit }
        let icns = e.bundleURL.appendingPathComponent("Contents/Resources/\(IconRenderer.iconFileBaseName).icns")
        let image = NSImage(contentsOf: icns) ?? NSWorkspace.shared.icon(forFile: e.bundlePath)
        images[key] = image
        return image
    }
}

struct AppIconView: View {
    let path: String
    var size: CGFloat = 32

    var body: some View {
        Image(nsImage: IconCache.appIcon(path: path))
            .resizable()
            .interpolation(.high)
            .frame(width: size, height: size)
            .accessibilityHidden(true)
    }
}

struct CloneIconView: View {
    let entry: RegistryEntry
    var size: CGFloat = 72

    var body: some View {
        Image(nsImage: IconCache.cloneIcon(entry))
            .resizable()
            .interpolation(.high)
            .frame(width: size, height: size)
            .accessibilityHidden(true)
    }
}

/// A clone's state as symbol + color + text (never color alone).
struct StatusLabel: View {
    let status: CloneStatus
    let running: Bool
    var busy: String?

    var body: some View {
        HStack(spacing: 5) {
            if let busy {
                ProgressView().controlSize(.mini)
                Text(busy)
            } else {
                switch status {
                case .originalMissing:
                    Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.red)
                    Text("Original missing")
                case .updateAvailable:
                    Image(systemName: "arrow.triangle.2.circlepath").foregroundStyle(.orange)
                    Text("Update available")
                case .upToDate:
                    if running {
                        Circle().fill(.green).frame(width: 7, height: 7)
                        Text("Running")
                    } else {
                        Circle().strokeBorder(.secondary, lineWidth: 1).frame(width: 7, height: 7)
                        Text("Not running")
                    }
                }
            }
        }
        .font(.caption)
        .foregroundStyle(.secondary)
        .lineLimit(1)
    }

    static func text(status: CloneStatus, running: Bool) -> String {
        switch status {
        case .originalMissing: return "Original missing"
        case .updateAvailable: return "Update available"
        case .upToDate: return running ? "Running" : "Not running"
        }
    }
}

/// A support level as a small capsule: symbol + text, color as a hint only.
struct SupportCapsule: View {
    let support: SupportLevel

    var body: some View {
        Label(support.displayName, systemImage: symbol)
            .font(.caption.weight(.medium))
            .foregroundStyle(color)
            .padding(.horizontal, 7)
            .padding(.vertical, 2)
            .background(color.opacity(0.12), in: Capsule())
    }

    var symbol: String {
        switch support {
        case .full: return "checkmark.circle.fill"
        case .limited: return "exclamationmark.circle.fill"
        case .unsupported: return "nosign"
        }
    }

    var color: Color {
        switch support {
        case .full: return .green
        case .limited: return .orange
        case .unsupported: return .secondary
        }
    }
}

/// Something went wrong: what happened, details to copy, and a prefilled GitHub issue.
struct FailureView: View {
    let title: String
    let report: ErrorReport
    var retryTitle: String?
    var retry: (() -> Void)?
    let close: () -> Void
    @Environment(\.openURL) private var openURL
    @State private var copied = false

    var body: some View {
        VStack(spacing: Brand.Space.m) {
            MascotView(pose: .oops, size: 104)
            Text(title)
                .font(.title2.weight(.semibold))
                .multilineTextAlignment(.center)
            Text(report.message)
                .multilineTextAlignment(.center)
                .foregroundStyle(.secondary)
                .textSelection(.enabled)
                .fixedSize(horizontal: false, vertical: true)
            DisclosureGroup("Details") {
                ScrollView {
                    Text(report.details)
                        .font(.system(.caption, design: .monospaced))
                        .textSelection(.enabled)
                        .frame(maxWidth: .infinity, alignment: .leading)
                }
                .frame(maxHeight: 110)
            }
            HStack {
                Button(copied ? "Copied" : "Copy Details") {
                    NSPasteboard.general.clearContents()
                    NSPasteboard.general.setString(report.message + "\n\n" + report.details, forType: .string)
                    copied = true
                }
                Button("Report on GitHub") { openURL(report.issueURL) }
                Spacer()
                if let retry, let retryTitle {
                    Button(retryTitle, action: retry)
                }
                Button("Close", action: close)
                    .keyboardShortcut(.defaultAction)
            }
        }
        .padding(Brand.Space.l)
        .frame(width: 480)
    }
}
