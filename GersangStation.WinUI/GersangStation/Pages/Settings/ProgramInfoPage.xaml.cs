using Microsoft.UI.Xaml.Controls;
using System;
using System.Net;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;

namespace GersangStation.Main.Setting;

/// <summary>
/// 프로그램 정보, 오픈소스 라이선스, 개인정보처리방침을 안내합니다.
/// </summary>
public sealed partial class ProgramInfoPage : Page
{
    public string AppDisplayName { get; } = Package.Current.DisplayName;
    public string AppDescription { get; } = "게임 설치, 빠른 패치, 다클라 생성 및 실행을 지원하는 게임 런처";
    public string AppVersion { get; } = GetVersionText();
    public string PublisherDisplayName { get; } = Package.Current.PublisherDisplayName;
    public Uri PrivacyPolicyUri { get; } = new("ms-appx:///Assets/Policies/privacy-policy.txt");

    public ProgramInfoPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 프로그램 정보 페이지의 외부 링크를 앱 내부 브라우저 페이지로 엽니다.
    /// </summary>
    private async void LinkButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not HyperlinkButton button)
            return;

        object? target = button.Tag;
        if (target is Uri localUri && IsPackagedTextDocument(localUri))
        {
            await OpenPackagedTextDocumentAsync(localUri);
            return;
        }

        string? url = target switch
        {
            Uri uri => uri.ToString(),
            _ => null
        };

        if (target is string linkKey)
        {
            if (App.CurrentWindow is MainWindow window)
                window.NavigateToWebViewPageByLinkKey(linkKey);

            return;
        }

        if (string.IsNullOrWhiteSpace(url))
            return;

        if (App.CurrentWindow is MainWindow currentWindow)
            currentWindow.NavigateToWebViewPage(url);
    }

    /// <summary>
    /// 패키지에 포함된 텍스트 문서를 읽어 내부 브라우저에서 보기 좋은 HTML로 표시합니다.
    /// </summary>
    private async Task OpenPackagedTextDocumentAsync(Uri documentUri)
    {
        try
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(documentUri);
            string text = await FileIO.ReadTextAsync(file);
            string html = BuildPlainTextDocumentHtml("개인정보처리방침", text);

            if (App.CurrentWindow is MainWindow window)
                window.NavigateToWebViewPageHtml(html);
        }
        catch
        {
            if (App.CurrentWindow is MainWindow window)
                window.NavigateToWebViewPage();
        }
    }

    /// <summary>
    /// 로컬 텍스트 문서를 WebView에 표시하기 위한 단순 HTML 문서로 감쌉니다.
    /// </summary>
    private static string BuildPlainTextDocumentHtml(string title, string content)
    {
        string encodedTitle = WebUtility.HtmlEncode(title);
        string encodedContent = WebUtility.HtmlEncode(content)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", "<br/>", StringComparison.Ordinal);

        return $$"""
<!DOCTYPE html>
<html lang="ko">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{encodedTitle}}</title>
  <style>
    :root {
      color-scheme: light dark;
      font-family: "Segoe UI", "Malgun Gothic", sans-serif;
    }
    body {
      margin: 0;
      background: #f6f3ed;
      color: #1c1b1a;
    }
    main {
      max-width: 880px;
      margin: 0 auto;
      padding: 32px 24px 48px;
      line-height: 1.75;
      font-size: 15px;
      white-space: normal;
    }
    h1 {
      margin: 0 0 20px;
      font-size: 28px;
      line-height: 1.3;
    }
    article {
      padding: 24px;
      border-radius: 16px;
      background: rgba(255,255,255,0.82);
      box-shadow: 0 10px 30px rgba(0,0,0,0.08);
    }
    @media (prefers-color-scheme: dark) {
      body {
        background: #171614;
        color: #f2efe9;
      }
      article {
        background: rgba(34,32,29,0.92);
        box-shadow: none;
      }
    }
  </style>
</head>
<body>
  <main>
    <h1>{{encodedTitle}}</h1>
    <article>{{encodedContent}}</article>
  </main>
</body>
</html>
""";
    }

    private static bool IsPackagedTextDocument(Uri uri)
        => uri.IsAbsoluteUri
           && (uri.Scheme.Equals("ms-appx", StringComparison.OrdinalIgnoreCase)
               || uri.Scheme.Equals("ms-appx-web", StringComparison.OrdinalIgnoreCase))
           && uri.AbsolutePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 패키지 버전을 사용자에게 표시할 문자열로 변환합니다.
    /// </summary>
    private static string GetVersionText()
    {
        PackageVersion version = Package.Current.Id.Version;
        return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }
}
