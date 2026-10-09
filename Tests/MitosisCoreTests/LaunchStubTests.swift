import Foundation
import Testing
@testable import MitosisCore

@Suite struct LaunchStubTests {
    @Test func resolvesDataPathPlaceholders() {
        let c = LaunchConfig.resolve(LaunchSettings(args: ["--user-data-dir={dataPath}"], env: ["HOME": "{dataPath}/home"]),
                                     kind: .exec, target: "App.mitosis-real", dataPath: "/d/1")
        #expect(c == LaunchConfig(kind: .exec, target: "App.mitosis-real", args: ["--user-data-dir=/d/1"], env: ["HOME": "/d/1/home"]))
    }

    @Test func execModeRunsRealExecutableWithArgsAndEnv() throws {
        let dir = try TestSupport.tempDir()
        let log = dir.appendingPathComponent("run.log")
        var o = FixtureOptions(); o.behavior = .exit(code: 0); o.logFile = log; o.signed = false
        let app = try FixtureFactory.makeApp(in: dir, o)
        let macOS = app.appendingPathComponent("Contents/MacOS")
        try FileManager.default.moveItem(at: macOS.appendingPathComponent("Fixture"), to: macOS.appendingPathComponent("Fixture.mitosis-real"))
        try FileManager.default.copyItem(at: TestSupport.stubBinary, to: macOS.appendingPathComponent("Fixture"))
        try LaunchConfig(kind: .exec, target: "Fixture.mitosis-real", args: ["--user-data-dir=/tmp/x y"], env: ["MITOSIS_TEST_VAR": "hello"])
            .write(toResources: app.appendingPathComponent("Contents/Resources"))

        let r = try Shell.run(macOS.appendingPathComponent("Fixture").path, ["extra", "-psn_0_12345"])
        #expect(r.status == 0)
        let text = try String(contentsOf: log, encoding: .utf8)
        #expect(text.contains("Contents/MacOS/Fixture.mitosis-real\n"))   // argv[0] is the real executable (path may be /private-prefixed)
        #expect(text.contains("arg=--user-data-dir=/tmp/x y"))
        #expect(text.contains("arg=extra"))
        #expect(!text.contains("-psn_"))
        #expect(text.contains("env.MITOSIS_TEST_VAR=hello"))
    }

    @Test func missingConfigFailsWith127() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions(); o.signed = false
        let app = try FixtureFactory.makeApp(in: dir, o)
        let stub = app.appendingPathComponent("Contents/MacOS/Stub")
        try FileManager.default.copyItem(at: TestSupport.stubBinary, to: stub)
        let r = try Shell.run(stub.path, [], check: false)
        #expect(r.status == 127)
        #expect(r.stderr.contains("mitosis-launch.json"))
    }
}
