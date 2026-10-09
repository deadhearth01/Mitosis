import Foundation

public enum Mitosis {
    public static let version = "0.1.0"
    public static let bundleID = "com.mitosis-mac.Mitosis"
}

extension Date {
    /// Current time truncated to whole seconds, so ISO-8601 round-trips are exact.
    public static var mitosisNow: Date {
        Date(timeIntervalSince1970: Date().timeIntervalSince1970.rounded(.down))
    }
}
