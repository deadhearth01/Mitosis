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

    /// macOS 26+ shrinks icons whose outline isn't the standard rounded square onto a gray plate, so the badge must
    /// stay inside the shape: within the content square and inside the bottom-right corner's curve.
    @Test func badgeStaysInsideTheStandardIconShape() throws {
        let size = 512
        let s = CGFloat(size)
        let clear = try #require(CGContext(data: nil, width: size, height: size, bitsPerComponent: 8, bytesPerRow: 0,
                                           space: CGColorSpace(name: CGColorSpace.sRGB)!,
                                           bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)?.makeImage())
        let img = try #require(IconRenderer.render(base: clear, badge: Badge(text: "W", color: "#0A84FF"), size: size))
        let ctx = try #require(CGContext(data: nil, width: size, height: size, bitsPerComponent: 8, bytesPerRow: size * 4,
                                         space: CGColorSpace(name: CGColorSpace.sRGB)!,
                                         bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue))
        ctx.draw(img, in: CGRect(x: 0, y: 0, width: s, height: s))
        let pixels = try #require(ctx.data).bindMemory(to: UInt8.self, capacity: size * size * 4)
        var opaque = 0
        // Corner curve of the 824/1024 rounded square: radius ≈ 0.18 of the canvas, centered 0.28 in from the corner.
        let corner = CGPoint(x: s * 0.72, y: s * 0.72)
        for y in 0..<size {
            for x in 0..<size where pixels[(y * size + x) * 4 + 3] > 8 {
                opaque += 1
                let p = CGPoint(x: CGFloat(x) + 0.5, y: CGFloat(y) + 0.5)   // memory rows run top to bottom here
                #expect(p.x >= s * 0.10 && p.x <= s * 0.90 && p.y >= s * 0.10 && p.y <= s * 0.90, "outside at \(x),\(y)")
                if p.x > corner.x && p.y > corner.y {
                    #expect(hypot(p.x - corner.x, p.y - corner.y) <= s * 0.181, "past the corner curve at \(x),\(y)")
                }
                if opaque > 0 && (p.x < s * 0.10 || p.x > s * 0.90) { return }
            }
        }
        #expect(opaque > 0)
    }

    @Test func hexColorParsing() {
        let c = IconRenderer.color(fromHex: "#30D158").components!
        #expect(abs(c[0] - 0x30 / 255.0) < 0.01 && abs(c[1] - 0xD1 / 255.0) < 0.01 && abs(c[2] - 0x58 / 255.0) < 0.01)
        let fallback = IconRenderer.color(fromHex: "nope").components!
        #expect(abs(fallback[2] - 1.0) < 0.01)   // #0A84FF blue
    }
}
