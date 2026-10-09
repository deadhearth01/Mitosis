import AppKit
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

    enum InstallPhase: Equatable {
        case idle
        case downloading
        case installing
        case failed(String)
    }

    var state: State = .idle
    var dismissed = false
    var install: InstallPhase = .idle

    /// In-place updates need a real Mitosis.app in a folder this user can write to.
    var canInstallInPlace: Bool {
        let app = Bundle.main.bundleURL
        return app.pathExtension == "app" && FileManager.default.isWritableFile(atPath: app.deletingLastPathComponent().path)
    }

    /// Downloads the release, verifies it, replaces this Mitosis.app, and relaunches.
    func installUpdate(version: String) async {
        await installUpdate(version: version, replacing: Bundle.main.bundleURL, relaunch: true)
    }

    func installUpdate(version: String, replacing app: URL, relaunch: Bool) async {
        install = .downloading
        let (zipURL, checksumURL) = SelfUpdater.assetURLs(version: version)
        let work = FileManager.default.temporaryDirectory.appendingPathComponent("mitosis-update-\(UUID().uuidString)")
        do {
            try FileManager.default.createDirectory(at: work, withIntermediateDirectories: true)
            let zip = work.appendingPathComponent(zipURL.lastPathComponent)
            let checksum = work.appendingPathComponent(checksumURL.lastPathComponent)
            for (remote, local) in [(zipURL, zip), (checksumURL, checksum)] {
                let (file, response) = try await URLSession.shared.download(from: remote)
                guard (response as? HTTPURLResponse)?.statusCode == 200 else {
                    throw SelfUpdaterError.cantReplace("GitHub didn't send the update. Try again later.")
                }
                try FileManager.default.moveItem(at: file, to: local)
            }
            install = .installing
            _ = try await offMain { try SelfUpdater.install(zip: zip, checksumFile: checksum, replacing: app) }
            try? FileManager.default.removeItem(at: work)
            if relaunch { Self.relaunch(app) } else { install = .idle }
        } catch {
            try? FileManager.default.removeItem(at: work)
            install = .failed((error as? SelfUpdaterError)?.description ?? "Couldn't download the update. Check your connection and try again.")
        }
    }

    /// Opens the new version a moment after this one quits.
    static func relaunch(_ app: URL) {
        let p = Process()
        p.executableURL = URL(fileURLWithPath: "/bin/sh")
        p.arguments = ["-c", "sleep 1; /usr/bin/open \"$0\"", app.path]
        try? p.run()
        NSApplication.shared.terminate(nil)
    }

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
