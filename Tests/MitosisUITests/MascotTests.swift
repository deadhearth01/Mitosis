import AppKit
import Testing
@testable import MitosisUI

@Suite struct MascotTests {
    @MainActor @Test func everyPoseShipsSmall() throws {
        for pose in Mascot.allCases {
            let image = try #require(pose.nsImage, "\(pose)")
            let rep = try #require(image.representations.first)
            #expect(rep.pixelsWide > 0 && rep.pixelsWide <= 480, "\(pose)")
        }
    }
}
