import Foundation
import Testing
@testable import MitosisUI

@Suite struct UpdateCheckerTests {
    static let json = """
    [
      {"tag_name": "v0.3.0-beta.1", "draft": false, "prerelease": true, "html_url": "https://example.com/beta"},
      {"tag_name": "v0.2.0", "draft": true, "prerelease": false, "html_url": "https://example.com/draft"},
      {"tag_name": "v0.1.1", "draft": false, "prerelease": false, "html_url": "https://example.com/011"},
      {"tag_name": "v0.1.0", "draft": false, "prerelease": false, "html_url": "https://example.com/010"}
    ]
    """

    @Test func picksTheNewestEligibleRelease() throws {
        let releases = try JSONDecoder().decode([UpdateChecker.Release].self, from: Data(Self.json.utf8))
        #expect(UpdateChecker.pick(releases, current: "0.1.0")?.tagName == "v0.1.1")
        #expect(UpdateChecker.pick(releases, current: "0.1.0-alpha.1")?.tagName == "v0.3.0-beta.1")
        #expect(UpdateChecker.pick(releases, current: "0.1.1") == nil)
        #expect(UpdateChecker.pick([], current: "0.1.0") == nil)
    }
}
