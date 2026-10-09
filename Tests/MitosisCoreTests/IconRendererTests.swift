import CoreGraphics
import Foundation
import ImageIO
import Testing
@testable import MitosisCore

@Suite struct IconRendererTests {
    @Test func baseIconExistsForAnyApp() throws {
        let dir = try TestSupport.tempDir()
        let icon = IconRenderer.baseIcon(forApp: try FixtureFactory.makeApp(in: dir))
        #expect((icon?.width ?? 0) >= 512)
    }

    @Test func renderProducesRequestedSize() throws {
        let dir = try TestSupport.tempDir()
        let base = try #require(IconRenderer.baseIcon(forApp: try FixtureFactory.makeApp(in: dir)))
        let img = try #require(IconRenderer.render(base: base, badge: Badge(text: "W", color: "#0A84FF"), size: 256))
        #expect(img.width == 256 && img.height == 256)
    }

    @Test func icnsContainsSmallAndLargeImages() throws {
        let dir = try TestSupport.tempDir()
        let base = try #require(IconRenderer.baseIcon(forApp: try FixtureFactory.makeApp(in: dir)))
        let data = try IconRenderer.icnsData(base: base, badge: Badge(text: "Wk", color: "#30D158"))
        let src = try #require(CGImageSourceCreateWithData(data as CFData, nil))
        let widths = (0..<CGImageSourceGetCount(src)).compactMap { CGImageSourceCreateImageAtIndex(src, $0, nil)?.width }
        #expect(widths.contains(1024), "widths: \(widths)")
        #expect(widths.contains(16))
    }

    @Test func hexColorParsing() {
        let c = IconRenderer.color(fromHex: "#30D158").components!
        #expect(abs(c[0] - 0x30 / 255.0) < 0.01 && abs(c[1] - 0xD1 / 255.0) < 0.01 && abs(c[2] - 0x58 / 255.0) < 0.01)
        let fallback = IconRenderer.color(fromHex: "nope").components!
        #expect(abs(fallback[2] - 1.0) < 0.01)   // #0A84FF blue
    }
}
