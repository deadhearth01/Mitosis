import AppKit
import Foundation
import MitosisUI

#if DEBUG
// swift run MitosisSnapshot [output-dir] [name-filter]
// swift run MitosisSnapshot --live-create <path-to-app>
let args = CommandLine.arguments
if args.count > 1, args[1] == "--catalog" {
    MainActor.assumeIsolated { CatalogReport.run() }
} else if args.count > 2, args[1] == "--live-create" {
    let app = URL(fileURLWithPath: args[2])
    Task { @MainActor in
        let ok = await LiveCheck.run(app: app)
        print(ok ? "LIVE CHECK PASSED" : "LIVE CHECK FAILED")
        exit(ok ? 0 : 1)
    }
    NSApplication.shared.setActivationPolicy(.accessory)
    NSApplication.shared.run()
} else {
    let output = URL(fileURLWithPath: args.count > 1 ? args[1] : ".build/snapshots")
    MainActor.assumeIsolated { SnapshotRunner.run(output: output, filter: args.count > 2 ? args[2] : nil) }
}
#else
print("Build in debug to render snapshots.")
#endif
