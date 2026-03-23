using System.IO;

namespace GerSDK;

/// <summary>
/// 특정 폴더에 사용자 지정 복사 규칙을 적용할 때 사용하는 델리게이트입니다.
/// </summary>
/// <param name="sourceDirectoryInfo">원본 폴더 정보입니다.</param>
/// <param name="destinationDirectoryPath">대상 폴더 경로입니다.</param>
public delegate void SymbolClientCustomDirectoryHandler(DirectoryInfo sourceDirectoryInfo, string destinationDirectoryPath);
