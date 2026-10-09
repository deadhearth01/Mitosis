import Foundation

/// Semantic-version comparison for update checks ("v0.1.0" > "0.1.0-alpha.1").
enum Versioning {
    struct Parsed {
        var core: [Int]
        var pre: [String]?
    }

    static func parse(_ raw: String) -> Parsed? {
        var text = raw.trimmingCharacters(in: .whitespaces)
        if text.hasPrefix("v") || text.hasPrefix("V") { text.removeFirst() }
        let parts = text.split(separator: "-", maxSplits: 1)
        guard let corePart = parts.first else { return nil }
        let numbers = corePart.split(separator: ".", omittingEmptySubsequences: false).map { Int($0) }
        guard !numbers.isEmpty, numbers.count <= 4, numbers.allSatisfy({ $0 != nil }) else { return nil }
        let pre = parts.count == 2 ? parts[1].split(separator: ".").map(String.init) : nil
        if let pre, pre.isEmpty { return nil }
        return Parsed(core: numbers.compactMap { $0 }, pre: pre)
    }

    static func isNewer(_ candidate: String, than current: String) -> Bool {
        guard let a = parse(candidate), let b = parse(current) else { return false }
        let length = max(a.core.count, b.core.count)
        for i in 0..<length {
            let x = i < a.core.count ? a.core[i] : 0
            let y = i < b.core.count ? b.core[i] : 0
            if x != y { return x > y }
        }
        switch (a.pre, b.pre) {
        case (nil, nil): return false
        case (nil, _?): return true
        case (_?, nil): return false
        case let (pa?, pb?):
            for (x, y) in zip(pa, pb) where x != y {
                switch (Int(x), Int(y)) {
                case let (nx?, ny?): return nx > ny
                case (nil, _?): return true       // alphanumeric sorts above numeric
                case (_?, nil): return false
                case (nil, nil): return x > y
                }
            }
            return pa.count > pb.count
        }
    }
}
