import Testing
@testable import MitosisUI

@Suite struct VersioningTests {
    @Test func comparesReleasesAndPrereleases() {
        #expect(Versioning.isNewer("0.1.0", than: "0.1.0-alpha.1"))
        #expect(Versioning.isNewer("v0.1.1", than: "0.1.0"))
        #expect(Versioning.isNewer("0.2.0-beta.1", than: "0.1.9"))
        #expect(Versioning.isNewer("0.1.0-alpha.10", than: "0.1.0-alpha.2"))
        #expect(Versioning.isNewer("0.1.0-beta", than: "0.1.0-alpha.3"))
        #expect(Versioning.isNewer("1.0", than: "0.9.9"))
        #expect(!Versioning.isNewer("0.1.0", than: "0.1.0"))
        #expect(!Versioning.isNewer("0.1.0-alpha.1", than: "0.1.0"))
        #expect(!Versioning.isNewer("garbage", than: "0.1.0"))
        #expect(!Versioning.isNewer("0.1.0", than: "garbage"))
    }
}
