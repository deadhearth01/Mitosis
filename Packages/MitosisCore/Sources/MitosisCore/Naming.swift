import Foundation

public enum Slug {
    public static func make(from name: String, existing: Set<String> = []) -> String {
        let folded = name.folding(options: [.diacriticInsensitive, .caseInsensitive], locale: Locale(identifier: "en_US_POSIX"))
        var out = ""
        var lastWasHyphen = false
        for scalar in folded.lowercased().unicodeScalars {
            let v = scalar.value
            if (97...122).contains(v) || (48...57).contains(v) {
                out.unicodeScalars.append(scalar)
                lastWasHyphen = false
            } else if !out.isEmpty && !lastWasHyphen {
                out.append("-")
                lastWasHyphen = true
            }
        }
        if out.count > 40 { out = String(out.prefix(40)) }
        while out.hasSuffix("-") { out.removeLast() }
        if out.isEmpty { out = "clone" }

        var candidate = out
        var n = 2
        while existing.contains(candidate) {
            candidate = "\(out)-\(n)"
            n += 1
        }
        return candidate
    }
}

public enum CloneNameError: Error, Equatable, CustomStringConvertible, Sendable {
    case empty
    case tooLong
    case leadingDot
    case invalidCharacter(Character)

    public var description: String {
        switch self {
        case .empty: return "Give the clone a name."
        case .tooLong: return "Clone names can be at most \(CloneName.maxLength) characters."
        case .leadingDot: return "Clone names can't start with a dot."
        case .invalidCharacter(let c): return "Clone names can't contain \"\(c)\"."
        }
    }
}

public enum CloneName {
    public static let maxLength = 60

    public static func validate(_ raw: String) throws -> String {
        let name = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !name.isEmpty else { throw CloneNameError.empty }
        guard name.count <= maxLength else { throw CloneNameError.tooLong }
        guard !name.hasPrefix(".") else { throw CloneNameError.leadingDot }
        if let bad = name.first(where: { $0 == "/" || $0 == ":" || $0.isNewline || $0 == "\u{0}" }) {
            throw CloneNameError.invalidCharacter(bad)
        }
        return name
    }

    /// "<App> (<Label>)", e.g. "Slack (Work)", so the label never merges with the app name.
    public static func compose(app: String, label: String) -> String {
        let a = app.trimmingCharacters(in: .whitespacesAndNewlines)
        let l = label.trimmingCharacters(in: .whitespacesAndNewlines)
        return l.isEmpty ? a : "\(a) (\(l))"
    }
}
