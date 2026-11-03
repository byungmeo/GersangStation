namespace GersangStation.Modules;

internal static class ClientDirectoryValidator {
    public static bool TryValidateName(string name, out string reason) {
        reason = string.Empty;
        if(string.IsNullOrWhiteSpace(name)) {
            reason = "폴더명을 입력해주세요.";
            return false;
        }

        if(name is "." or "..") {
            reason = "현재 폴더(.) 또는 상위 폴더(..)를 폴더명으로 사용할 수 없습니다.";
            return false;
        }

        if(name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
            reason = "폴더명에 사용할 수 없는 문자 또는 경로 구분자가 포함되어 있습니다.";
            return false;
        }

        if(name.EndsWith('.') || name.EndsWith(' ')) {
            reason = "폴더명은 마침표 또는 공백으로 끝날 수 없습니다.";
            return false;
        }

        if(name.Length > 255) {
            reason = "폴더명은 255자를 초과할 수 없습니다.";
            return false;
        }

        // 확장자가 붙거나 대소문자가 달라도 Windows 장치 이름은 사용할 수 없습니다.
        string deviceName = name.Split('.')[0].TrimEnd(' ');
        if(deviceName.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || deviceName.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || deviceName.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || deviceName.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || (deviceName.Length == 4
                && (deviceName.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || deviceName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && "123456789¹²³".Contains(deviceName[3]))) {
            reason = "Windows 예약 이름은 폴더명으로 사용할 수 없습니다.";
            return false;
        }

        return true;
    }

    public static bool TryGetSiblingPaths(string orgPath, string secondName, string thirdName,
        out string secondPath, out string thirdPath, out string reason) {
        secondPath = thirdPath = string.Empty;
        if(!TryValidateName(secondName, out reason)) {
            reason = "2클라: " + reason;
            return false;
        }
        if(!TryValidateName(thirdName, out reason)) {
            reason = "3클라: " + reason;
            return false;
        }
        if(!TryNormalizeDirectoryPath(orgPath, out string orgFullPath, out reason)) {
            return false;
        }

        string? parentPath = Path.GetDirectoryName(orgFullPath);
        if(parentPath == null) {
            reason = "본클라의 상위 폴더를 확인할 수 없습니다.";
            return false;
        }

        return TryValidatePaths(orgFullPath, Path.Combine(parentPath, secondName), Path.Combine(parentPath, thirdName),
            out secondPath, out thirdPath, out reason);
    }

    public static bool TryValidatePaths(string orgPath, string secondPath, string thirdPath,
        out string secondFullPath, out string thirdFullPath, out string reason) {
        secondFullPath = thirdFullPath = string.Empty;
        if(!TryNormalizeDirectoryPath(orgPath, out _, out reason)) {
            return false;
        }
        // 패치 후 재구성에서는 설정하지 않은 클라이언트 경로를 생략할 수 있습니다.
        if(!string.IsNullOrWhiteSpace(secondPath)
            && !TryValidateDestinationPath(orgPath, secondPath, out secondFullPath, out reason)) {
            reason = "2클라: " + reason;
            return false;
        }
        if(!string.IsNullOrWhiteSpace(thirdPath)
            && !TryValidateDestinationPath(orgPath, thirdPath, out thirdFullPath, out reason)) {
            reason = "3클라: " + reason;
            return false;
        }
        if(secondFullPath.Length > 0 && secondFullPath.Equals(thirdFullPath, StringComparison.OrdinalIgnoreCase)) {
            reason = "2클라와 3클라에 동일한 폴더를 사용할 수 없습니다.";
            return false;
        }
        return true;
    }

    public static bool TryValidateDestinationPath(string orgPath, string destPath, out string destFullPath, out string reason) {
        destFullPath = string.Empty;
        if(!TryNormalizeDirectoryPath(orgPath, out string orgFullPath, out reason)
            || !TryNormalizeDirectoryPath(destPath, out destFullPath, out reason)) {
            return false;
        }
        if(orgFullPath.Equals(destFullPath, StringComparison.OrdinalIgnoreCase)) {
            reason = "본클라와 동일한 경로에는 다클라를 생성할 수 없습니다.";
            return false;
        }
        return true;
    }

    private static bool TryNormalizeDirectoryPath(string path, out string fullPath, out string reason) {
        fullPath = reason = string.Empty;
        if(string.IsNullOrWhiteSpace(path)) {
            reason = "클라이언트 경로가 비어있습니다.";
            return false;
        }
        if(path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal)) {
            reason = "탐색기에서 사용할 수 있는 일반 Windows 경로를 지정해주세요.";
            return false;
        }

        try {
            string root = Path.GetPathRoot(path) ?? string.Empty;
            // 정규화 전에 원래 구성요소를 검사하여 끝의 점/공백이 숨겨지지 않게 합니다.
            foreach(string component in path[root.Length..].Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)) {
                if(component is "." or "..") continue;
                if(!TryValidateName(component, out reason)) return false;
            }
            fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            return true;
        } catch(Exception ex) when(ex is ArgumentException or NotSupportedException or PathTooLongException) {
            reason = "클라이언트 경로가 올바르지 않거나 너무 깁니다.";
            return false;
        }
    }
}
