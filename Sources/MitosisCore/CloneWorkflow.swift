import Foundation

/// Create/refresh with a launch check (spec §14 decision 8): a clone is only reported as ready once it actually
/// starts. Shared by the app and the CLI so both behave the same way.
public struct CloneWorkflow: Sendable {
    public let builder: CloneBuilder

    public init(builder: CloneBuilder) {
        self.builder = builder
    }

    private var registry: CloneRegistry { CloneRegistry(fileURL: builder.environment.registryFile) }

    /// Creates the clone, opens it, and keeps it only if it is still running after `window`.
    /// A clone that doesn't start is removed completely (bundle, fresh data, registry entry).
    public func createVerified(_ request: CloneRequest, window: Duration = .seconds(10)) async throws -> RegistryEntry {
        let entry = try builder.create(request)
        builder.progress?("launch check")
        let alive: Bool
        do {
            alive = try await CloneLauncher.openAndCheck(entry, window: window)
        } catch {
            try removeFailed(entry)
            throw CloneError.failed(step: "launch check", message: "couldn't open \"\(entry.manifest.name)\" (\(error)), so it was removed.")
        }
        guard alive else {
            try removeFailed(entry)
            throw CloneError.failed(step: "launch check",
                                    message: "\"\(entry.manifest.name)\" didn't stay open, so it was removed. Try compatibility (fallback) mode.")
        }
        return entry
    }

    /// Refreshes the clone, opens it, and restores the previous version if the new one doesn't start.
    public func refreshVerified(_ entry: RegistryEntry, window: Duration = .seconds(10)) async throws -> RegistryEntry {
        let fm = FileManager.default
        let maintenance = CloneMaintenance(builder: builder)
        guard !RunningMonitor.isRunning(clone: entry.manifest) else { throw CloneError.cloneRunning(entry.manifest.name) }
        // APFS clone of the current bundle: an instant, nearly free backup.
        let backup = builder.environment.clonesDir.appendingPathComponent(".mitosis-backup-\(UUID().uuidString).app")
        try FileCloner.cloneOrCopy(from: entry.bundleURL, to: backup)
        defer { try? fm.removeItem(at: backup) }

        let updated = try maintenance.refresh(entry)
        builder.progress?("launch check")
        let alive = (try? await CloneLauncher.openAndCheck(updated, window: window)) ?? false
        if alive { return updated }

        CloneLauncher.terminate(updated)
        try await Task.sleep(for: .milliseconds(500))
        _ = try fm.replaceItemAt(entry.bundleURL, withItemAt: backup)
        if builder.environment.registerWithLaunchServices { try? LaunchServices.register(entry.bundleURL) }
        try registry.upsert(entry)
        throw CloneError.failed(step: "launch check",
                                message: "the updated \"\(entry.manifest.name)\" didn't start, so the previous version was restored.")
    }

    /// Removes a clone that never became usable. It holds no user data yet, so it is deleted rather than trashed.
    private func removeFailed(_ entry: RegistryEntry) throws {
        let fm = FileManager.default
        CloneLauncher.terminate(entry)
        if builder.environment.registerWithLaunchServices { LaunchServices.unregister(entry.bundleURL) }
        var problems: [String] = []
        for url in [entry.bundleURL, URL(fileURLWithPath: entry.manifest.dataPath)] where fm.fileExists(atPath: url.path) {
            do { try fm.removeItem(at: url) } catch { problems.append("\(url.path): \(error.localizedDescription)") }
        }
        try registry.remove(id: entry.id)
        if !problems.isEmpty {
            throw CloneError.failed(step: "launch check",
                                    message: "\"\(entry.manifest.name)\" didn't start, and some files couldn't be removed: \(problems.joined(separator: "; "))")
        }
    }
}
