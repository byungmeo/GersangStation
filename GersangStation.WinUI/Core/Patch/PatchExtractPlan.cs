namespace Core.Patch;

public sealed record PatchFile(
    int SourceVersion,
    string RelativeDir,          // 예: "\Online\"
    string CompressedFileName    // 예: "_patchdata_33807.gsz"
);

public sealed class PatchExtractPlan
{
    public SortedDictionary<int, List<PatchFile>> ByVersion { get; } = new();
}