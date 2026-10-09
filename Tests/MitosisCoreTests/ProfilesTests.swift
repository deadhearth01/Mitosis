import Foundation
import Testing
@testable import MitosisCore

@Suite struct ProfilesTests {
    static func profile(args: [String] = [], env: [String: String] = [:]) -> AppProfile {
        AppProfile(id: "test", bundleIDs: ["com.example.fixture"], version: 1, mode: .identity, support: .full,
                   launch: LaunchSettings(args: args, env: env), notes: "Test profile.")
    }

    @Test func lookupByBundleID() throws {
        let store = try ProfileStore(profiles: [Self.profile()])
        #expect(store.profile(forBundleID: "com.example.fixture")?.id == "test")
        #expect(store.profile(forBundleID: "com.example.other") == nil)
    }

    @Test func validatesAllowlist() {
        #expect(throws: ProfileValidationError.argNotAllowed(profile: "test", arg: "--remote-debugging-port=9222")) {
            try ProfileStore(profiles: [Self.profile(args: ["--remote-debugging-port=9222"])])
        }
        #expect(throws: ProfileValidationError.envNotAllowed(profile: "test", key: "DYLD_INSERT_LIBRARIES")) {
            try ProfileStore(profiles: [Self.profile(env: ["DYLD_INSERT_LIBRARIES": "/tmp/x.dylib"])])
        }
        #expect(throws: ProfileValidationError.envNotAllowed(profile: "test", key: "HOME")) {
            try ProfileStore(profiles: [Self.profile(env: ["HOME": "/etc"])])   // value must be {dataPath}-based
        }
    }

    @Test func rejectsUnknownSchema() {
        #expect(throws: ProfileValidationError.unknownSchema(2)) {
            try ProfileStore.decode(Data(#"{"schema":2,"profiles":[]}"#.utf8))
        }
    }

    @Test func bundledProfilesLoadAndAreValid() throws {
        let store = try ProfileStore.bundled()
        #expect(store.profile(forBundleID: "com.google.Chrome") != nil)
        #expect(store.profile(forBundleID: "com.anthropic.claudefordesktop") != nil)
        #expect(store.profile(forBundleID: "net.whatsapp.WhatsApp")?.support == .unsupported)   // A1 (spike S5)
        #expect(store.profile(forBundleID: "ru.keepcoder.Telegram") == nil)                       // A1
    }

    @Test func defaultLaunchSettings() {
        #expect(DefaultLaunchSettings.forFrameworks([.electron]) == LaunchSettings(args: ["--user-data-dir={dataPath}"], env: [:]))
        #expect(DefaultLaunchSettings.forFrameworks([.chromium, .sparkle]) == LaunchSettings(args: ["--user-data-dir={dataPath}"], env: [:]))
        #expect(DefaultLaunchSettings.forFrameworks([.sparkle]).isEmpty)
        #expect(DefaultLaunchSettings.fallback == LaunchSettings(args: [], env: ["HOME": "{dataPath}"]))
    }
}
