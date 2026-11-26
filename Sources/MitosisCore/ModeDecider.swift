import Foundation

public struct ModeDecider: Sendable {
    public let profiles: ProfileStore

    public init(profiles: ProfileStore) {
        self.profiles = profiles
    }

    public func decide(for app: AppInfo) -> ModeDecision {
        if app.isAppleApp {
            return ModeDecision(mode: nil, support: .unsupported,
                                reasons: ["Apple's own apps are protected by macOS and can't be cloned."])
        }
        if app.isMitosisClone {
            return ModeDecision(mode: nil, support: .unsupported,
                                reasons: ["This is already a Mitosis clone. Clone the original app instead."])
        }
        if let profile = profiles.profile(forBundleID: app.bundleID) {
            return ModeDecision(mode: profile.support == .unsupported ? nil : profile.mode,
                                support: profile.support, reasons: [profile.notes])
        }
        if app.isSandboxed && !app.restrictedEntitlements.isEmpty {
            return ModeDecision(mode: nil, support: .unsupported, reasons: [
                "This app relies on iCloud or shared app data that only works for the original app, so it can't be cloned yet. Light mode (coming soon) will support it.",
            ])
        }
        // A full clone even here: compatibility mode runs the original app, which can't help apps that allow one
        // copy per bundle ID or ignore HOME (Muse). If the full clone won't start, CloneWorkflow tries it anyway.
        if !app.restrictedEntitlements.isEmpty {
            let list = app.restrictedEntitlements.joined(separator: ", ")
            return ModeDecision(mode: .identity, support: .limited, reasons: [
                "Uses features tied to its developer (\(list)). The clone gets its own data and Dock icon, but those features may not work in it, so you may need to sign in again when you open it.",
            ])
        }
        if app.hasMASReceipt {
            return ModeDecision(mode: .identity, support: .limited, reasons: [
                "Mac App Store app: the clone may ask you to sign in again or refuse to start.",
            ])
        }
        return ModeDecision(mode: .identity, support: .full,
                            reasons: ["Fully supported: separate data, notifications and Dock icon."])
    }
}
