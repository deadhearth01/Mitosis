import Foundation
import Testing
@testable import MitosisCore

@Suite struct SelfUpdaterTests {
    /// A tiny signed "Mitosis.app" with the given version and bundle ID.
    static func fakeApp(in dir: URL, version: String, bundleID: String = Mitosis.bundleID) throws -> URL {
        let app = dir.appendingPathComponent("Mitosis.app")
        let macOS = app.appendingPathComponent("Contents/MacOS")
        try FileManager.default.createDirectory(at: macOS, withIntermediateDirectories: true)
        try FileManager.default.copyItem(at: TestSupport.stubBinary, to: macOS.appendingPathComponent("Mitosis"))
        try InfoPlistEditor.write(["CFBundleIdentifier": bundleID, "CFBundleExecutable": "Mitosis", "CFBundlePackageType": "APPL",
                                   "CFBundleShortVersionString": version, "CFBundleInfoDictionaryVersion": "6.0"],
                                  to: app.appendingPathComponent("Contents/Info.plist"))
        try Shell.run("/usr/bin/codesign", ["--force", "--sign", "-", app.path])
        return app
    }

    /// Zips the app the way package-release.sh does and writes "<sha256>  <name>" next to it.
    static func release(_ app: URL, in dir: URL, tamper: Bool = false) throws -> (zip: URL, checksum: URL) {
        let zip = dir.appendingPathComponent("Mitosis-9.9.9.zip")
        try Shell.run("/usr/bin/ditto", ["-c", "-k", "--sequesterRsrc", "--keepParent", app.path, zip.path])
        var digest = try SelfUpdater.sha256(of: zip)
        if tamper { digest = String(repeating: "0", count: 64) }
        let checksum = dir.appendingPathComponent("Mitosis-9.9.9.zip.sha256")
        try "\(digest)  Mitosis-9.9.9.zip\n".write(to: checksum, atomically: true, encoding: .utf8)
        return (zip, checksum)
    }

    @Test func releaseAssetURLs() {
        let urls = SelfUpdater.assetURLs(version: "0.2.1")
        #expect(urls.zip.absoluteString == "https://github.com/deadhearth01/Mitosis/releases/download/v0.2.1/Mitosis-0.2.1.zip")
        #expect(urls.checksum.absoluteString == "https://github.com/deadhearth01/Mitosis/releases/download/v0.2.1/Mitosis-0.2.1.zip.sha256")
    }

    @Test func installsAVerifiedUpdateInPlace() throws {
        let dir = try TestSupport.tempDir()
        let installed = try Self.fakeApp(in: dir.appendingPathComponent("Applications"), version: "1.0")
        let new = try Self.fakeApp(in: dir.appendingPathComponent("build"), version: "9.9.9")
        let (zip, checksum) = try Self.release(new, in: dir)

        let result = try SelfUpdater.install(zip: zip, checksumFile: checksum, replacing: installed, registerWithLaunchServices: false)
        #expect(result.path == installed.path)
        #expect(try InfoPlistEditor.read(installed.appendingPathComponent("Contents/Info.plist"))["CFBundleShortVersionString"] as? String == "9.9.9")
        try Signer.verify(installed)
    }

    @Test func refusesABadChecksumAndKeepsTheInstalledApp() throws {
        let dir = try TestSupport.tempDir()
        let installed = try Self.fakeApp(in: dir.appendingPathComponent("Applications"), version: "1.0")
        let new = try Self.fakeApp(in: dir.appendingPathComponent("build"), version: "9.9.9")
        let (zip, checksum) = try Self.release(new, in: dir, tamper: true)
        #expect(throws: SelfUpdaterError.checksumMismatch) {
            try SelfUpdater.install(zip: zip, checksumFile: checksum, replacing: installed, registerWithLaunchServices: false)
        }
        #expect(try InfoPlistEditor.read(installed.appendingPathComponent("Contents/Info.plist"))["CFBundleShortVersionString"] as? String == "1.0")
    }

    @Test func refusesSomethingThatIsNotMitosis() throws {
        let dir = try TestSupport.tempDir()
        let installed = try Self.fakeApp(in: dir.appendingPathComponent("Applications"), version: "1.0")
        let impostor = try Self.fakeApp(in: dir.appendingPathComponent("build"), version: "9.9.9", bundleID: "com.example.other")
        let (zip, checksum) = try Self.release(impostor, in: dir)
        #expect(throws: SelfUpdaterError.notMitosis) {
            try SelfUpdater.install(zip: zip, checksumFile: checksum, replacing: installed, registerWithLaunchServices: false)
        }
        #expect(try InfoPlistEditor.read(installed.appendingPathComponent("Contents/Info.plist"))["CFBundleIdentifier"] as? String == Mitosis.bundleID)
    }
}
