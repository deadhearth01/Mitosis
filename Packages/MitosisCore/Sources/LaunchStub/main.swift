import Darwin
import Foundation

struct StubConfig: Decodable {
    let kind: String
    let target: String
    let args: [String]
    let env: [String: String]
}

func fail(_ message: String) -> Never {
    FileHandle.standardError.write(Data("mitosis-launch: \(message)\n".utf8))
    exit(127)
}

guard let configURL = Bundle.main.url(forResource: "mitosis-launch", withExtension: "json"),
      let data = try? Data(contentsOf: configURL),
      let config = try? JSONDecoder().decode(StubConfig.self, from: data)
else { fail("missing or invalid mitosis-launch.json") }

let passthrough = CommandLine.arguments.dropFirst().filter { !$0.hasPrefix("-psn_") }

switch config.kind {
case "exec":
    guard let macOSDir = Bundle.main.executableURL?.deletingLastPathComponent() else { fail("cannot locate executable") }
    let target = macOSDir.appendingPathComponent(config.target).path
    for (key, value) in config.env { setenv(key, value, 1) }
    let argv = [target] + config.args + passthrough
    var cArgs: [UnsafeMutablePointer<CChar>?] = argv.map { strdup($0) } + [nil]
    execv(target, &cArgs)
    fail("execv \(target) failed: \(String(cString: strerror(errno)))")

case "open":
    var openArgs = ["-n", "-a", config.target]
    for (key, value) in config.env.sorted(by: { $0.key < $1.key }) { openArgs += ["--env", "\(key)=\(value)"] }
    let launchArgs = config.args + passthrough
    if !launchArgs.isEmpty { openArgs += ["--args"] + launchArgs }
    let p = Process()
    p.executableURL = URL(fileURLWithPath: "/usr/bin/open")
    p.arguments = openArgs
    do {
        try p.run()
        p.waitUntilExit()
        exit(p.terminationStatus)
    } catch {
        fail("open failed: \(error)")
    }

default:
    fail("unknown launch kind \(config.kind)")
}
