import AppKit
import MitosisCore

/// Knows which clones are running without polling: NSWorkspace tells us when any app starts or quits.
@MainActor
final class RunningTracker {
    var onChange: (() -> Void)?
    private var observers: [NSObjectProtocol] = []

    init() {
        let center = NSWorkspace.shared.notificationCenter
        for name in [NSWorkspace.didLaunchApplicationNotification, NSWorkspace.didTerminateApplicationNotification] {
            observers.append(center.addObserver(forName: name, object: nil, queue: .main) { [weak self] _ in
                MainActor.assumeIsolated { self?.changed() }
            })
        }
    }

    private func changed() {
        onChange?()
        // A compatibility-mode clone writes its instance file a moment after the app appears.
        Task { [weak self] in
            try? await Task.sleep(for: .seconds(1.5))
            self?.onChange?()
        }
    }

    func running(_ entries: [RegistryEntry]) -> Set<UUID> {
        let bundleIDs = Set(NSWorkspace.shared.runningApplications.compactMap(\.bundleIdentifier))
        return Set(entries.filter { e in
            switch e.manifest.mode {
            case .identity: return bundleIDs.contains(e.manifest.cloneBundleID)
            case .fallback: return RunningMonitor.instancePID(of: e.manifest) != nil
            }
        }.map(\.id))
    }

    /// Asks the clone to quit the normal way (so it can save), and falls back to SIGTERM by pid.
    static func quit(_ entry: RegistryEntry) {
        if entry.manifest.mode == .identity {
            let apps = NSRunningApplication.runningApplications(withBundleIdentifier: entry.manifest.cloneBundleID)
            if !apps.isEmpty { apps.forEach { $0.terminate() }; return }
        }
        CloneLauncher.terminate(entry)
    }
}
