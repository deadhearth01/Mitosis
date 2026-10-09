import Foundation
import MitosisUI

#if DEBUG
// swift run MitosisSnapshot [output-dir] [name-filter]
let args = CommandLine.arguments
let output = URL(fileURLWithPath: args.count > 1 ? args[1] : ".build/snapshots")
MainActor.assumeIsolated { SnapshotRunner.run(output: output, filter: args.count > 2 ? args[2] : nil) }
#else
print("Build in debug to render snapshots.")
#endif
