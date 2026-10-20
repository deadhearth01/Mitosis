namespace Mitosis.ReleaseVerifier;

/// <summary>Resource limits for verification, sized against the actual release
/// artifact (≈39 MB compressed, ≈113 MB unpacked, 75 files, largest payload DLL
/// well under 100 MB) with generous headroom: they prevent archive explosions
/// and sidecar floods from becoming resource exhaustion, while never rejecting
/// a legitimate current — or plausibly larger — release. Treat archive inputs
/// as hostile: these limits are the only bound between a crafted ZIP and the
/// verifier's disk, memory, and time.</summary>
public sealed record VerificationLimits
{
    /// <summary>The single-file archive may not exceed this. Guards the hashing
    /// pass, which streams the whole file.</summary>
    public long MaxArchiveBytes { get; init; } = 512L * 1024 * 1024;

    /// <summary>Any one entry's declared (and metered) expanded size.</summary>
    public long MaxEntryUncompressedBytes { get; init; } = 256L * 1024 * 1024;

    /// <summary>The extraction scratch directory's total expanded size. The
    /// declared sizes in the zone/central directory are untrusted, so the real
    /// meter is the number of bytes actually decompressed while extracting.</summary>
    public long MaxTotalUncompressedBytes { get; init; } = 512L * 1024 * 1024;

    /// <summary>Total file entries considered (the current artifact has 75).</summary>
    public int MaxEntries { get; init; } = 1000;

    /// <summary>The .sha256 sidecar is "<hash>  <filename>" (≤ ~100 bytes); a
    /// megabyte is far beyond anything legitimate and shuts down sidecar-flood
    /// reads.</summary>
    public long MaxSidecarBytes { get; init; } = 1024 * 1024;

    public static VerificationLimits Default { get; } = new();
}
