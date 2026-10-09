import Foundation
import Testing
@testable import MitosisCore

@Suite struct ModeDeciderTests {
    static func info(bundleID: String = "com.example.app", apple: Bool = false, clone: Bool = false,
                     receipt: Bool = false, sandboxed: Bool = false, restricted: [String] = []) -> AppInfo {
        AppInfo(url: URL(fileURLWithPath: "/Applications/App.app"), bundleID: bundleID, name: "App", version: "1", build: "1",
                executableName: "App", isSandboxed: sandboxed, hasMASReceipt: receipt, isAppleApp: apple, isMitosisClone: clone,
                frameworks: [], restrictedEntitlements: restricted, cdhash: nil)
    }
    static let decider = try! ModeDecider(profiles: ProfileStore(profiles: [
        AppProfile(id: "p", bundleIDs: ["com.example.profiled"], version: 1, mode: .fallback, support: .limited,
                   launch: .none, notes: "Profile says fallback."),
        AppProfile(id: "u", bundleIDs: ["com.example.blocked"], version: 1, mode: .identity, support: .unsupported,
                   launch: .none, notes: "Known not to work."),
    ]))

    @Test func appleAppsAreUnsupported() {
        let d = Self.decider.decide(for: Self.info(apple: true))
        #expect(d == ModeDecision(mode: nil, support: .unsupported, reasons: ["Apple's own apps are protected by macOS and can't be cloned."]))
    }

    // Review Focus: cloning a clone is refused.
    @Test func mitosisClonesAreUnsupported() {
        let d = Self.decider.decide(for: Self.info(clone: true))
        #expect(d.support == .unsupported)
        #expect(d.mode == nil)
        #expect(d.reasons == ["This is already a Mitosis clone. Clone the original app instead."])
    }

    @Test func profileWins() {
        #expect(Self.decider.decide(for: Self.info(bundleID: "com.example.profiled", restricted: ["aps-environment"]))
                == ModeDecision(mode: .fallback, support: .limited, reasons: ["Profile says fallback."]))
        #expect(Self.decider.decide(for: Self.info(bundleID: "com.example.blocked"))
                == ModeDecision(mode: nil, support: .unsupported, reasons: ["Known not to work."]))
    }

    // A2 (spike S5): sandboxed apps that need iCloud/app groups crash when re-signed, and fallback can't separate their data.
    @Test func sandboxedAppsWithRestrictedEntitlementsAreUnsupported() {
        let d = Self.decider.decide(for: Self.info(sandboxed: true, restricted: ["com.apple.security.application-groups"]))
        #expect(d.support == .unsupported)
        #expect(d.mode == nil)
        #expect(d.reasons.first?.contains("Light mode") == true)
    }

    @Test func restrictedEntitlementsUseFallback() {
        let d = Self.decider.decide(for: Self.info(restricted: ["aps-environment", "keychain-access-groups"]))
        #expect(d.mode == .fallback)
        #expect(d.support == .limited)
        #expect(d.reasons.first?.contains("aps-environment, keychain-access-groups") == true)
    }

    @Test func appStoreAppsAreLimitedIdentity() {
        let d = Self.decider.decide(for: Self.info(receipt: true))
        #expect(d.mode == .identity)
        #expect(d.support == .limited)
    }

    @Test func plainAppsAreFullIdentity() {
        #expect(Self.decider.decide(for: Self.info())
                == ModeDecision(mode: .identity, support: .full, reasons: ["Fully supported: separate data, notifications and Dock icon."]))
    }
}
