import Foundation

/// Runs blocking engine work (file copies, codesign, lsappinfo) off the main actor.
func background<T: Sendable>(_ body: @escaping @Sendable () throws -> T) async throws -> T {
    try await Task.detached(priority: .userInitiated, operation: body).value
}
