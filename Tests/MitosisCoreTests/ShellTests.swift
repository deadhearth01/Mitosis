import Testing
@testable import MitosisCore

@Suite struct ShellTests {
    @Test func capturesStdoutAndStatus() throws {
        let r = try Shell.run("/bin/echo", ["hello", "world"])
        #expect(r.status == 0)
        #expect(r.stdout == "hello world\n")
    }

    @Test func capturesStderrWithoutDeadlockOnLargeOutput() throws {
        // 200 KB on both streams would deadlock a naive pipe implementation.
        let r = try Shell.run("/bin/sh", ["-c", "head -c 200000 /dev/zero | tr '\\0' a; head -c 200000 /dev/zero | tr '\\0' b 1>&2"])
        #expect(r.stdout.count == 200_000)
        #expect(r.stderr.count == 200_000)
    }

    @Test func throwsOnNonZeroExitWhenChecking() {
        let error = #expect(throws: ShellError.self) {
            try Shell.run("/bin/sh", ["-c", "echo boom 1>&2; exit 3"])
        }
        #expect(error?.description.contains("(3)") == true)
        #expect(error?.description.contains("boom") == true)
    }

    @Test func returnsStatusWhenNotChecking() throws {
        let r = try Shell.run("/bin/sh", ["-c", "exit 4"], check: false)
        #expect(r.status == 4)
    }
}
