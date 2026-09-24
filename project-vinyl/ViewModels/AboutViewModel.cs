namespace ProjectVinyl.ViewModels;

/// <summary>
/// ViewModel for the Legal / About view.
/// Static content — no commands or mutable state needed.
/// </summary>
public class AboutViewModel : ViewModelBase
{
    public string Title => "Legal & Credits";

    public string LicenseText => """
        MIT License

        Copyright (c) 2026 Project Vinyl Authors

        Permission is hereby granted, free of charge, to any person obtaining a copy
        of this software and associated documentation files (the "Software"), to deal
        in the Software without restriction, including without limitation the rights
        to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        copies of the Software, and to permit persons to whom the Software is
        furnished to do so, subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all
        copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        SOFTWARE.
        """;

    public string OriginalAuthors => """
        • nicol — Project creator, design, and direction
        • Claude (Anthropic) — AI co-author, implementation, and code generation
        """;

    public string ThirdPartyLibraries => """
        • Avalonia UI — MIT License
        • NAudio — MIT License
        • SoundTouch.Net — LGPL License
        • TagLibSharp — LGPL License
        • YoutubeExplode — MIT License
        • CommunityToolkit.Mvvm — MIT License
        • SkiaSharp — MIT License
        • HarfBuzzSharp — MIT License
        """;

    public string TermsSummary => "Do whatever you want with this software, but keep the original copyright notice and give credit to the authors listed above.";
}