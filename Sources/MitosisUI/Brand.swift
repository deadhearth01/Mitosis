import AppKit
import SwiftUI

/// Brand tokens (docs/brand/BRAND.md). Brand blue is only an accent; windows, text and materials stay system.
enum Brand {
    /// Contrast-safe control tint derived from Primary blue #41AEF8 (light #1A7FE0, dark #3B9EF0).
    static let accent = Color(nsColor: NSColor(name: "MitosisAccent") { appearance in
        appearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua
            ? NSColor(srgbRed: 0x3B / 255, green: 0x9E / 255, blue: 0xF0 / 255, alpha: 1)
            : NSColor(srgbRed: 0x1A / 255, green: 0x7F / 255, blue: 0xE0 / 255, alpha: 1)
    })
    /// Primary blue, for illustrations and the completed-step glint only.
    static let primaryBlue = Color(.sRGB, red: 0x41 / 255, green: 0xAE / 255, blue: 0xF8 / 255)

    /// 8 pt rhythm.
    enum Space {
        static let xs: CGFloat = 4
        static let s: CGFloat = 8
        static let m: CGFloat = 16
        static let l: CGFloat = 24
        static let xl: CGFloat = 32
    }

    static let cardRadius: CGFloat = 12
}

/// Mito, the mascot. One pose per view at most.
enum Mascot: String, CaseIterable, Sendable {
    case wave, split, empty, success, oops, thinking, search, building, key, notyet, refresh, clean, stats, goodbye, guide, sleeping, point

    @MainActor private static var cache: [Mascot: NSImage] = [:]

    @MainActor var nsImage: NSImage? {
        if let hit = Self.cache[self] { return hit }
        guard let url = Bundle.module.url(forResource: rawValue, withExtension: "png", subdirectory: "Mascot"),
              let image = NSImage(contentsOf: url) else { return nil }
        Self.cache[self] = image
        return image
    }

    @MainActor var image: Image {
        nsImage.map { Image(nsImage: $0) } ?? Image(systemName: "square.dashed")
    }
}

/// A mascot illustration at a fixed size, hidden from VoiceOver (the text next to it carries the meaning).
struct MascotView: View {
    let pose: Mascot
    var size: CGFloat = 120

    var body: some View {
        pose.image
            .resizable()
            .interpolation(.high)
            .aspectRatio(contentMode: .fit)
            .frame(width: size, height: size)
            .accessibilityHidden(true)
    }
}
