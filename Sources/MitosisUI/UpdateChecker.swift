import Foundation
import MitosisCore
import Observation

/// Looks for a newer Mitosis on GitHub Releases. The only network request Mitosis makes; it can be turned off.
@MainActor
@Observable
final class UpdateChecker {
    struct Release: Decodable, Equatable, Sendable {
        var tagName: String
        var draft: Bool
        var prerelease: Bool
        var htmlURL: URL

        enum CodingKeys: String, CodingKey {
            case tagName = "tag_name", draft, prerelease, htmlURL = "html_url"
        }

        var version: String { tagName.hasPrefix("v") ? String(tagName.dropFirst()) : tagName }
    }

    enum State: Equatable {
        case idle
        case checking
        case upToDate
        case available(version: String, url: URL)
        case failed(String)
    }

    static let endpoint = URL(string: "https://api.github.com/repos/deadhearth01/Mitosis/releases?per_page=10")!
    static let updateCommand = "curl -fsSL https://raw.githubusercontent.com/deadhearth01/Mitosis/main/scripts/install.sh | bash"

    var state: State = .idle
    var dismissed = false

    /// The newest release that is newer than `current`. Prereleases count only for people already on one.
    nonisolated static func pick(_ releases: [Release], current: String) -> Release? {
        let onPrerelease = current.contains("-")
        return releases
            .filter { !$0.draft && (onPrerelease || !$0.prerelease) && Versioning.isNewer($0.version, than: current) }
            .max { Versioning.isNewer($1.version, than: $0.version) }
    }

    /// Checks at most once a day, and only when the setting is on.
    func checkIfDue() {
        let defaults = UserDefaults.standard
        guard defaults.bool(forKey: Prefs.checkUpdatesKey) else { return }
        let last = defaults.double(forKey: Prefs.lastUpdateCheckKey)
        guard Date().timeIntervalSince1970 - last > 86_400 else { return }
        Task { await check() }
    }

    func check() async {
        state = .checking
        var request = URLRequest(url: Self.endpoint, timeoutInterval: 15)
        request.setValue("application/vnd.github+json", forHTTPHeaderField: "Accept")
        request.setValue("Mitosis/\(Mitosis.version)", forHTTPHeaderField: "User-Agent")
        do {
            let (data, response) = try await URLSession.shared.data(for: request)
            guard (response as? HTTPURLResponse)?.statusCode == 200 else {
                state = .failed("GitHub didn't answer. Try again later.")
                return
            }
            let releases = try JSONDecoder().decode([Release].self, from: data)
            UserDefaults.standard.set(Date().timeIntervalSince1970, forKey: Prefs.lastUpdateCheckKey)
            if let newer = Self.pick(releases, current: Mitosis.version) {
                state = .available(version: newer.version, url: newer.htmlURL)
            } else {
                state = .upToDate
            }
        } catch {
            state = .failed("Couldn't reach GitHub. Check your internet connection.")
        }
    }
}
