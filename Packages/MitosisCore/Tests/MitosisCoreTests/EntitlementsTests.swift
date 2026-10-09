import Foundation
import Testing
@testable import MitosisCore

@Suite struct EntitlementsTests {
    @Test func readsAndClassifiesEntitlements() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions()
        o.entitlements = [
            "com.apple.security.app-sandbox": true,
            "com.apple.security.network.client": true,
            "com.apple.developer.icloud-container-identifiers": ["iCloud.com.example"],
            "keychain-access-groups": ["ABCDE12345.com.example"],
            "com.apple.security.application-groups": ["group.com.example"],
        ]
        let app = try FixtureFactory.makeApp(in: dir, o)

        let ents = try Entitlements.read(from: app)
        #expect(ents.count == 5)
        #expect(Entitlements.restricted(in: ents) == [
            "com.apple.developer.icloud-container-identifiers",
            "com.apple.security.application-groups",
            "keychain-access-groups",
        ])
        let kept = Entitlements.sanitized(ents)
        #expect(Set(kept.keys) == ["com.apple.security.app-sandbox", "com.apple.security.network.client"])
    }

    // Review Focus: unsigned sources must not break inspection.
    @Test func unsignedAppHasNoEntitlements() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions()
        o.signed = false
        let app = try FixtureFactory.makeApp(in: dir, o)
        #expect(try Entitlements.read(from: app).isEmpty)
    }

    @Test func xmlDataIsAValidPlist() throws {
        let data = try Entitlements.xmlData(["com.apple.security.app-sandbox": true])
        let back = try PropertyListSerialization.propertyList(from: data, format: nil) as? [String: Any]
        #expect(back?["com.apple.security.app-sandbox"] as? Bool == true)
    }
}
