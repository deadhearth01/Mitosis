import Foundation
@testable import MitosisCore

enum FixtureBehavior {
    case stayOpen(seconds: Int)
    case exit(code: Int32)
}

struct FixtureOptions {
    var name = "Fixture"
    var bundleID = "com.example.fixture"
    var version = "1.0"
    var build = "1"
    var executable = "Fixture"
    var entitlements: [String: Any] = [:]
    var extraInfo: [String: Any] = [:]
    /// Framework names, e.g. ["Electron Framework", "Sparkle"] → Contents/Frameworks/<name>.framework
    var frameworks: [String] = []
    /// Adds Contents/Frameworks/<name> Framework.framework/Versions/A/Helpers/<name> Helper.app (Chromium layout).
    var chromiumFramework: String? = nil
    /// Helper apps in Contents/Frameworks, e.g. ["Fixture Helper"] (Electron layout).
    var helperApps: [String] = []
    /// Adds Contents/Resources/app.asar.unpacked/addon.node (a Mach-O dylib).
    var nativeModule = false
    var masReceipt = false
    var signed = true
    var behavior: FixtureBehavior = .stayOpen(seconds: 30)
    var logFile: URL? = nil
}

enum FixtureFactory {
    private static let buildDir: URL = {
        let dir = FileManager.default.temporaryDirectory.appendingPathComponent("MitosisFixtureBuild-\(getpid())")
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir
    }()

    /// A tiny AppKit app: reads Contents/Resources/fixture.conf, logs args/env, then stays open or exits.
    private static let mainSource = #"""
    import AppKit
    import Darwin
    let resources = Bundle.main.resourceURL!
    var logPath: String?
    var stay = 30
    var exitCode: Int32?
    if let conf = try? String(contentsOf: resources.appendingPathComponent("fixture.conf"), encoding: .utf8) {
        for line in conf.split(separator: "\n") {
            if line.hasPrefix("log=") { logPath = String(line.dropFirst(4)) }
            if line.hasPrefix("stay=") { stay = Int(line.dropFirst(5)) ?? 30 }
            if line.hasPrefix("exit=") { exitCode = Int32(line.dropFirst(5)) }
        }
    }
    if let logPath {
        let env = ProcessInfo.processInfo.environment
        var out = "pid=\(getpid())\n"
        for a in CommandLine.arguments { out += "arg=\(a)\n" }
        out += "env.HOME=\(env["HOME"] ?? "")\n"
        out += "env.MITOSIS_TEST_VAR=\(env["MITOSIS_TEST_VAR"] ?? "")\n"
        if let h = FileHandle(forWritingAtPath: logPath) { h.seekToEndOfFile(); h.write(Data(out.utf8)); try? h.close() }
        else { FileManager.default.createFile(atPath: logPath, contents: Data(out.utf8)) }
    }
    if let exitCode { exit(exitCode) }
    let app = NSApplication.shared
    app.setActivationPolicy(.accessory)
    DispatchQueue.main.asyncAfter(deadline: .now() + .seconds(stay)) { exit(0) }
    app.run()
    """#

    static let mainBinary: URL = {
        let src = buildDir.appendingPathComponent("fixture-main.swift")
        let out = buildDir.appendingPathComponent("fixture-main")
        try! mainSource.write(to: src, atomically: true, encoding: .utf8)
        try! Shell.run("/usr/bin/xcrun", ["swiftc", "-swift-version", "5", "-O", "-o", out.path, src.path])
        return out
    }()

    static let dylib: URL = {
        let src = buildDir.appendingPathComponent("fixture-lib.c")
        let out = buildDir.appendingPathComponent("libfixture.dylib")
        try! "int fixture_value(void) { return 42; }\n".write(to: src, atomically: true, encoding: .utf8)
        try! Shell.run("/usr/bin/xcrun", ["clang", "-dynamiclib", "-o", out.path, src.path])
        return out
    }()

    static func makeApp(in dir: URL, _ o: FixtureOptions = FixtureOptions()) throws -> URL {
        let fm = FileManager.default
        let app = dir.appendingPathComponent("\(o.name).app")
        let contents = app.appendingPathComponent("Contents")
        let macOS = contents.appendingPathComponent("MacOS")
        let resources = contents.appendingPathComponent("Resources")
        let frameworksDir = contents.appendingPathComponent("Frameworks")
        try fm.createDirectory(at: macOS, withIntermediateDirectories: true)
        try fm.createDirectory(at: resources, withIntermediateDirectories: true)
        try fm.copyItem(at: mainBinary, to: macOS.appendingPathComponent(o.executable))

        var info: [String: Any] = [
            "CFBundleIdentifier": o.bundleID, "CFBundleName": o.name, "CFBundleExecutable": o.executable,
            "CFBundleShortVersionString": o.version, "CFBundleVersion": o.build,
            "CFBundlePackageType": "APPL", "CFBundleInfoDictionaryVersion": "6.0", "LSMinimumSystemVersion": "13.0",
        ]
        for (k, v) in o.extraInfo { info[k] = v }
        try writePlist(info, to: contents.appendingPathComponent("Info.plist"))

        var conf = ""
        switch o.behavior {
        case .stayOpen(let s): conf += "stay=\(s)\n"
        case .exit(let c): conf += "exit=\(c)\n"
        }
        if let log = o.logFile { conf += "log=\(log.path)\n" }
        try conf.write(to: resources.appendingPathComponent("fixture.conf"), atomically: true, encoding: .utf8)

        var nested: [URL] = []
        for name in o.frameworks {
            nested.append(try makeFramework(named: name, in: frameworksDir))
        }
        if let chromium = o.chromiumFramework {
            let fw = try makeFramework(named: "\(chromium) Framework", in: frameworksDir)
            let helpers = fw.appendingPathComponent("Versions/A/Helpers")
            try fm.createDirectory(at: helpers, withIntermediateDirectories: true)
            let helper = try makeHelperApp(named: "\(chromium) Helper", bundleID: "\(o.bundleID).helper", in: helpers)
            nested.insert(helper, at: 0)   // inner helper first…
            nested.append(fw)              // …then the framework that contains it
        }
        for name in o.helperApps {
            nested.append(try makeHelperApp(named: name, bundleID: "\(o.bundleID).helper", in: frameworksDir))
        }
        if o.nativeModule {
            let unpacked = resources.appendingPathComponent("app.asar.unpacked")
            try fm.createDirectory(at: unpacked, withIntermediateDirectories: true)
            let node = unpacked.appendingPathComponent("addon.node")
            try fm.copyItem(at: dylib, to: node)
            nested.append(node)
        }

        if o.signed {
            for item in nested { try Shell.run("/usr/bin/codesign", ["--force", "--sign", "-", item.path]) }
            var args = ["--force", "--sign", "-"]
            if !o.entitlements.isEmpty {
                let ent = dir.appendingPathComponent("\(UUID().uuidString).entitlements")
                try writePlist(o.entitlements, to: ent)
                args += ["--entitlements", ent.path]
            }
            try Shell.run("/usr/bin/codesign", args + [app.path])
        }
        if o.masReceipt {
            let receiptDir = contents.appendingPathComponent("_MASReceipt")
            try fm.createDirectory(at: receiptDir, withIntermediateDirectories: true)
            try Data("fake-receipt".utf8).write(to: receiptDir.appendingPathComponent("receipt"))
        }
        return app
    }

    static func writePlist(_ dict: [String: Any], to url: URL) throws {
        let data = try PropertyListSerialization.data(fromPropertyList: dict, format: .xml, options: 0)
        try data.write(to: url)
    }

    private static func makeFramework(named name: String, in frameworksDir: URL) throws -> URL {
        let fm = FileManager.default
        let fw = frameworksDir.appendingPathComponent("\(name).framework")
        let versionA = fw.appendingPathComponent("Versions/A")
        try fm.createDirectory(at: versionA.appendingPathComponent("Resources"), withIntermediateDirectories: true)
        try fm.createDirectory(at: versionA.appendingPathComponent("Libraries"), withIntermediateDirectories: true)
        try fm.copyItem(at: dylib, to: versionA.appendingPathComponent(name))
        try fm.copyItem(at: dylib, to: versionA.appendingPathComponent("Libraries/libextra.dylib"))
        try writePlist([
            "CFBundleIdentifier": "com.example.framework.\(Slug.make(from: name))", "CFBundleExecutable": name,
            "CFBundlePackageType": "FMWK", "CFBundleInfoDictionaryVersion": "6.0",
        ], to: versionA.appendingPathComponent("Resources/Info.plist"))
        try fm.createSymbolicLink(atPath: fw.appendingPathComponent("Versions/Current").path, withDestinationPath: "A")
        try fm.createSymbolicLink(atPath: fw.appendingPathComponent(name).path, withDestinationPath: "Versions/Current/\(name)")
        try fm.createSymbolicLink(atPath: fw.appendingPathComponent("Resources").path, withDestinationPath: "Versions/Current/Resources")
        // Libraries inside the framework must be signed before the framework itself.
        try Shell.run("/usr/bin/codesign", ["--force", "--sign", "-", versionA.appendingPathComponent("Libraries/libextra.dylib").path])
        return fw
    }

    private static func makeHelperApp(named name: String, bundleID: String, in dir: URL) throws -> URL {
        let fm = FileManager.default
        let app = dir.appendingPathComponent("\(name).app")
        let macOS = app.appendingPathComponent("Contents/MacOS")
        try fm.createDirectory(at: macOS, withIntermediateDirectories: true)
        try fm.copyItem(at: mainBinary, to: macOS.appendingPathComponent(name))
        try writePlist([
            "CFBundleIdentifier": bundleID, "CFBundleExecutable": name, "CFBundleName": name,
            "CFBundlePackageType": "APPL", "CFBundleInfoDictionaryVersion": "6.0",
        ], to: app.appendingPathComponent("Contents/Info.plist"))
        return app
    }
}
