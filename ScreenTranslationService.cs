using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Collections.Concurrent;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace dịchMH;

public sealed class ScreenTranslationService
{
    private static readonly SemaphoreSlim TranslationSlots = new(4);
    private readonly ConcurrentDictionary<string, string> _translationCache = new(StringComparer.Ordinal);
    private PreviousTranslatedLine[] _previousLines = Array.Empty<PreviousTranslatedLine>();

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public int CachedTranslationCount => _translationCache.Count;

    public IReadOnlyList<string> AvailableOcrLanguages =>
        OcrEngine.AvailableRecognizerLanguages
            .Select(language => language.LanguageTag)
            .ToArray();

    public Task<byte[]> CaptureScreenAreaAsync(Rectangle area) => CapturePngAsync(area);

    public async Task<IReadOnlyList<TranslatedScreenLine>> TranslateCapturedImageAsync(byte[] image, string languageTag)
    {
        IReadOnlyList<RecognizedScreenLine> recognizedLines = await RecognizeLinesAsync(image, languageTag);
        if (recognizedLines.Count == 0)
        {
            if (_previousLines.Length > 0)
            {
                IReadOnlyList<TranslatedScreenLine> retainedLines = _previousLines
                    .Where(line => line.MissingScans < 2)
                    .Select(line => line.Translated)
                    .ToArray();
                if (retainedLines.Count > 0)
                {
                    _previousLines = _previousLines
                        .Select(line => line with { MissingScans = line.MissingScans + 1 })
                        .ToArray();
                    return retainedLines;
                }
            }

            throw new InvalidOperationException("Không nhận diện được chữ trong vùng đã chọn. Hãy chọn vùng lớn hơn hoặc kiểm tra ngôn ngữ OCR.");
        }

        var nextLines = new List<PreviousTranslatedLine>(recognizedLines.Count);
        var matchedPrevious = new bool[_previousLines.Length];

        foreach (RecognizedScreenLine line in recognizedLines)
        {
            int previousIndex = FindPreviousLineWithSameText(line.Text);
            string translation;

            if (previousIndex >= 0)
            {
                matchedPrevious[previousIndex] = true;
                translation = _previousLines[previousIndex].Translated.Text;
            }
            else
            {
                translation = await TranslateLineAsync(line.Text, languageTag);
            }

            nextLines.Add(new PreviousTranslatedLine(
                line.Text,
                new TranslatedScreenLine(translation, line.Bounds),
                0));
        }

        for (int previousIndex = 0; previousIndex < _previousLines.Length; previousIndex++)
        {
            if (matchedPrevious[previousIndex])
            {
                continue;
            }

            bool lineChangedAtSamePosition = recognizedLines.Any(line =>
                !string.Equals(line.Text, _previousLines[previousIndex].SourceText, StringComparison.Ordinal)
                && BoundsAreNear(_previousLines[previousIndex].Translated.Bounds, line.Bounds));

            if (!lineChangedAtSamePosition
                && _previousLines[previousIndex].MissingScans < 2)
            {
                nextLines.Add(_previousLines[previousIndex] with
                {
                    MissingScans = _previousLines[previousIndex].MissingScans + 1
                });
            }
        }

        _previousLines = nextLines.ToArray();
        return nextLines.Select(line => line.Translated).ToArray();
    }

    private int FindPreviousLineWithSameText(string sourceText)
    {
        for (int index = 0; index < _previousLines.Length; index++)
        {
            if (string.Equals(_previousLines[index].SourceText, sourceText, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool BoundsAreNear(RectangleF first, RectangleF second)
    {
        float firstCenterX = first.X + first.Width / 2;
        float firstCenterY = first.Y + first.Height / 2;
        float secondCenterX = second.X + second.Width / 2;
        float secondCenterY = second.Y + second.Height / 2;
        float deltaX = firstCenterX - secondCenterX;
        float deltaY = firstCenterY - secondCenterY;
        return deltaX * deltaX + deltaY * deltaY <= 24 * 24;
    }

    private static Task<byte[]> CapturePngAsync(Rectangle area)
    {
        if (area.Width <= 0 || area.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(area), "Vùng chụp không hợp lệ.");
        }

        return Task.Run(() =>
        {
            using var bitmap = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(area.Left, area.Top, 0, 0, area.Size, CopyPixelOperation.SourceCopy);
            }

            using var output = new MemoryStream();
            bitmap.Save(output, ImageFormat.Png);
            return output.ToArray();
        });
    }

    private static async Task<IReadOnlyList<RecognizedScreenLine>> RecognizeLinesAsync(byte[] imageBytes, string languageTag)
    {
        var engine = OcrEngine.TryCreateFromLanguage(new Language(languageTag));
        if (engine is null)
        {
            throw new InvalidOperationException($"Windows chưa có gói OCR cho ngôn ngữ {languageTag}. Hãy cài gói nhận dạng chữ của ngôn ngữ đó trong Cài đặt Windows.");
        }

        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(imageBytes);
            await writer.StoreAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);
        var result = await engine.RecognizeAsync(softwareBitmap);
        return result.Lines
            .Where(line => !string.IsNullOrWhiteSpace(line.Text) && line.Words.Count > 0)
            .Select(line => new RecognizedScreenLine(
                line.Text,
                GetLineBounds(line)))
            .ToArray();
    }

    private static RectangleF GetLineBounds(Windows.Media.Ocr.OcrLine line)
    {
        var wordBounds = line.Words.Select(word => word.BoundingRect).ToArray();
        double left = wordBounds.Min(bounds => bounds.X);
        double top = wordBounds.Min(bounds => bounds.Y);
        double right = wordBounds.Max(bounds => bounds.X + bounds.Width);
        double bottom = wordBounds.Max(bounds => bounds.Y + bounds.Height);
        return new RectangleF((float)left, (float)top, (float)(right - left), (float)(bottom - top));
    }

    private static async Task<string> TranslateToVietnameseAsync(string text)
    {
        string url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=vi&dt=t&q="
            + WebUtility.UrlEncode(text);
        using HttpResponseMessage response = await HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement segments = document.RootElement[0];
        var translated = new List<string>();
        foreach (JsonElement segment in segments.EnumerateArray())
        {
            if (segment.ValueKind == JsonValueKind.Array
                && segment.GetArrayLength() > 0
                && segment[0].ValueKind == JsonValueKind.String)
            {
                translated.Add(segment[0].GetString()!);
            }
        }

        if (translated.Count == 0)
        {
            throw new InvalidOperationException("Dịch vụ dịch không trả về kết quả. Hãy thử lại.");
        }

        return string.Concat(translated);
    }

    private async Task<string> TranslateLineAsync(string text, string languageTag)
    {
        string cacheKey = languageTag + "\n" + text;
        if (_translationCache.TryGetValue(cacheKey, out string? cached))
        {
            return cached;
        }

        await TranslationSlots.WaitAsync();
        try
        {
            if (_translationCache.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            string translation = await TranslateToVietnameseAsync(text);
            _translationCache[cacheKey] = translation;
            if (_translationCache.Count > 512)
            {
                _translationCache.TryRemove(_translationCache.Keys.First(), out _);
            }

            return translation;
        }
        finally
        {
            TranslationSlots.Release();
        }
    }
}

public sealed record TranslatedScreenLine(string Text, RectangleF Bounds);

internal sealed record RecognizedScreenLine(string Text, RectangleF Bounds);
internal sealed record PreviousTranslatedLine(
    string SourceText,
    TranslatedScreenLine Translated,
    int MissingScans);
