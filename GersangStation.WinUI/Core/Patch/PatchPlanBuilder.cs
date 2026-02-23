namespace Core.Patch;

public static class PatchPlanBuilder_StringRows
{
    /// <summary>
    /// entriesByVersion[v]는 "row 배열" 리스트. (탭 split 결과)
    /// row[1] = compressed filename (*.gsz)
    /// row[3] = relative dir (항상 "\...\" 또는 "\" 형태)
    ///
    /// 중복 키 = row[3] + row[1]
    /// 최신 우선 덮어쓰기(입력 버전 오름차순 전제)
    /// </summary>
    public static PatchExtractPlan BuildExtractPlan(
        IReadOnlyDictionary<int, List<string[]>> entriesByVersion)
    {
        if (entriesByVersion is null) throw new ArgumentNullException(nameof(entriesByVersion));

        // key -> PatchFile (최신이 승자)
        var latestByKey = new Dictionary<string, PatchFile>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in entriesByVersion.OrderBy(kv => kv.Key)) // 오름차순
        {
            int version = kv.Key;
            var rows = kv.Value;

            foreach (var row in rows)
            {
                // 최소 방어 (요청 외 기능 추가는 아니고, 입력 깨졌을 때 즉시 죽는 방지)
                if (row.Length <= 3) continue;
                if (row[0] == ";EOF") break;

                string comp = row[1];
                string relDir = row[3];

                if (string.IsNullOrEmpty(relDir)) relDir = @"\";
                if (!relDir.EndsWith("\\", StringComparison.Ordinal)) relDir += "\\";

                string key = relDir + comp;

                latestByKey[key] = new PatchFile(
                    SourceVersion: version,
                    RelativeDir: relDir,
                    CompressedFileName: comp);
            }
        }

        var plan = new PatchExtractPlan();
        foreach (var pf in latestByKey.Values)
        {
            if (!plan.ByVersion.TryGetValue(pf.SourceVersion, out var list))
            {
                list = new List<PatchFile>();
                plan.ByVersion.Add(pf.SourceVersion, list);
            }
            list.Add(pf);
        }

        return plan;
    }
}