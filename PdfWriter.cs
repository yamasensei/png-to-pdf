using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace PngToPdf
{
    /// <summary>
    /// Minimal PDF writer — images only, no external dependencies.
    /// Embeds each image as a JPEG stream inside a PDF page.
    /// </summary>
    public static class PdfWriter
    {
        // A4 at 72 dpi in points
        private const float A4W = 595f;
        private const float A4H = 842f;

        public static void WritePdf(string outputPath, List<string> imagePaths, string pageMode)
        {
            // Convert all images to JPEG bytes first
            var pages = new List<(byte[] jpegBytes, int imgW, int imgH, float pageW, float pageH)>();

            foreach (var path in imagePaths)
            {
                using (var bmp = new Bitmap(path))
                {
                    int iw = bmp.Width, ih = bmp.Height;
                    float pw, ph;

                    if (pageMode == "A4 dọc")  { pw = A4W; ph = A4H; }
                    else if (pageMode == "A4 ngang") { pw = A4H; ph = A4W; }
                    else { pw = iw; ph = ih; }

                    // Re-encode to JPEG for PDF embedding
                    byte[] jpg = ToJpeg(bmp);
                    pages.Add((jpg, iw, ih, pw, ph));
                }
            }

            // ── Build PDF ──────────────────────────────────────────
            var buf = new List<byte>();
            var offsets = new List<long>();   // xref byte offsets

            void Append(string s) => buf.AddRange(Encoding.Latin1.GetBytes(s));
            void AppendBytes(byte[] b) => buf.AddRange(b);
            long Pos() => buf.Count;

            // Header
            Append("%PDF-1.4\n");

            // Object numbering: 1=catalog, 2=pages, 3..= page+image pairs (2 objs each)
            int firstPage = 3;
            int objCount = 2 + pages.Count * 2;

            // We'll come back and fix the Pages /Kids array — store placeholder positions
            // Strategy: write everything, track offsets, write xref at the end.

            var objOffsets = new long[objCount + 2]; // 1-indexed

            // ── Catalog (obj 1) ────────────────────────────────────
            objOffsets[1] = Pos();
            Append("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

            // ── Pages (obj 2) — write after we know all page obj ids ─
            // Build kids list
            var kids = new System.Text.StringBuilder();
            for (int i = 0; i < pages.Count; i++)
            {
                int pageObj = firstPage + i * 2;
                kids.Append($"{pageObj} 0 R ");
            }

            objOffsets[2] = Pos();
            Append($"2 0 obj\n<< /Type /Pages /Count {pages.Count} /Kids [{kids}] >>\nendobj\n");

            // ── Page + Image objects ───────────────────────────────
            for (int i = 0; i < pages.Count; i++)
            {
                var (jpg, iw, ih, pw, ph) = pages[i];
                int pageObj  = firstPage + i * 2;
                int imageObj = pageObj + 1;

                // Compute image rect (fit inside page, centered)
                float scale = Math.Min(pw / iw, ph / ih);
                float dw = iw * scale, dh = ih * scale;
                float dx = (pw - dw) / 2f, dy = (ph - dh) / 2f;

                string xname = $"Im{i}";

                // Content stream
                string stream =
                    $"q\n{F(dw)} 0 0 {F(dh)} {F(dx)} {F(dy)} cm\n/{xname} Do\nQ\n";
                byte[] streamBytes = Encoding.Latin1.GetBytes(stream);

                // Page object
                objOffsets[pageObj] = Pos();
                Append($"{pageObj} 0 obj\n");
                Append($"<< /Type /Page /Parent 2 0 R\n");
                Append($"   /MediaBox [0 0 {F(pw)} {F(ph)}]\n");
                Append($"   /Resources << /XObject << /{xname} {imageObj} 0 R >> >>\n");
                Append($"   /Contents {imageObj + pages.Count} 0 R\n");  // we'll add content streams after
                Append($">>\nendobj\n");

                // Image XObject
                objOffsets[imageObj] = Pos();
                Append($"{imageObj} 0 obj\n");
                Append($"<< /Type /XObject /Subtype /Image\n");
                Append($"   /Width {iw} /Height {ih}\n");
                Append($"   /ColorSpace /DeviceRGB /BitsPerComponent 8\n");
                Append($"   /Filter /DCTDecode /Length {jpg.Length}\n");
                Append($">>\nstream\n");
                AppendBytes(jpg);
                Append("\nendstream\nendobj\n");
            }

            // ── Content streams (one per page) ────────────────────
            int contentBase = firstPage + pages.Count * 2;
            // Re-wire pages to correct content obj numbers
            // (We wrote /Contents above with wrong numbers — rebuild properly)

            // Clear and redo everything cleanly with a two-pass approach
            // Actually let's redo with correct numbering from scratch:
            buf.Clear();
            Array.Clear(objOffsets, 0, objOffsets.Length);

            // Object layout:
            // 1 = catalog
            // 2 = pages
            // for each page i:  3+i*3 = page, 4+i*3 = image xobj, 5+i*3 = content stream
            int n = pages.Count;

            Append("%PDF-1.4\n");

            objOffsets[1] = Pos();
            Append("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

            var kids2 = new StringBuilder();
            for (int i = 0; i < n; i++) kids2.Append($"{3 + i * 3} 0 R ");

            objOffsets[2] = Pos();
            Append($"2 0 obj\n<< /Type /Pages /Count {n} /Kids [{kids2}] >>\nendobj\n");

            // Expand offsets array
            int totalObjs = 2 + n * 3;
            objOffsets = new long[totalObjs + 2];

            // Rewrite
            buf.Clear();
            Append("%PDF-1.4\n");

            objOffsets[1] = Pos();
            Append("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

            objOffsets[2] = Pos();
            Append($"2 0 obj\n<< /Type /Pages /Count {n} /Kids [{kids2}] >>\nendobj\n");

            for (int i = 0; i < n; i++)
            {
                var (jpg, iw, ih, pw, ph) = pages[i];
                int pgObj  = 3 + i * 3;
                int imgObj = 4 + i * 3;
                int csObj  = 5 + i * 3;

                float scale = Math.Min(pw / iw, ph / ih);
                float dw = iw * scale, dh = ih * scale;
                float dx = (pw - dw) / 2f, dy = (ph - dh) / 2f;

                string xname = $"Im{i}";

                // Page
                objOffsets[pgObj] = Pos();
                Append($"{pgObj} 0 obj\n");
                Append($"<< /Type /Page /Parent 2 0 R\n");
                Append($"   /MediaBox [0 0 {F(pw)} {F(ph)}]\n");
                Append($"   /Resources << /XObject << /{xname} {imgObj} 0 R >> >>\n");
                Append($"   /Contents {csObj} 0 R\n");
                Append($">>\nendobj\n");

                // Image XObject
                objOffsets[imgObj] = Pos();
                Append($"{imgObj} 0 obj\n");
                Append($"<< /Type /XObject /Subtype /Image\n");
                Append($"   /Width {iw} /Height {ih}\n");
                Append($"   /ColorSpace /DeviceRGB /BitsPerComponent 8\n");
                Append($"   /Filter /DCTDecode /Length {jpg.Length}\n");
                Append($">>\nstream\n");
                AppendBytes(jpg);
                Append("\nendstream\nendobj\n");

                // Content stream
                string cs = $"q\n{F(dw)} 0 0 {F(dh)} {F(dx)} {F(dy)} cm\n/{xname} Do\nQ\n";
                byte[] csBytes = Encoding.Latin1.GetBytes(cs);
                objOffsets[csObj] = Pos();
                Append($"{csObj} 0 obj\n<< /Length {csBytes.Length} >>\nstream\n");
                AppendBytes(csBytes);
                Append("\nendstream\nendobj\n");
            }

            // ── xref ──────────────────────────────────────────────
            long xrefPos = Pos();
            Append($"xref\n0 {totalObjs + 1}\n");
            Append("0000000000 65535 f \n");
            for (int i = 1; i <= totalObjs; i++)
                Append($"{objOffsets[i]:D10} 00000 n \n");

            Append($"trailer\n<< /Size {totalObjs + 1} /Root 1 0 R >>\n");
            Append($"startxref\n{xrefPos}\n%%EOF\n");

            File.WriteAllBytes(outputPath, buf.ToArray());
        }

        private static string F(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        private static byte[] ToJpeg(Bitmap bmp)
        {
            // Ensure RGB (no alpha channel issues)
            using (var rgb = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format24bppRgb))
            using (var g = Graphics.FromImage(rgb))
            {
                g.Clear(Color.White);
                g.DrawImage(bmp, 0, 0);
                using (var ms = new MemoryStream())
                {
                    var enc = GetEncoder(ImageFormat.Jpeg);
                    var ep = new EncoderParameters(1);
                    ep.Param[0] = new EncoderParameter(Encoder.Quality, 92L);
                    rgb.Save(ms, enc, ep);
                    return ms.ToArray();
                }
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat fmt)
        {
            foreach (var c in ImageCodecInfo.GetImageEncoders())
                if (c.FormatID == fmt.Guid) return c;
            return null;
        }
    }
}
