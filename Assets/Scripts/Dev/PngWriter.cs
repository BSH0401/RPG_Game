using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 화면을 PNG 로 저장한다. 이 프로젝트에는 ScreenCapture·ImageConversion 모듈이 없어서
    /// 화면을 읽어 직접 PNG 로 인코딩한다(자동 플레이테스트 전용). WaitForEndOfFrame 뒤에 호출할 것.
    /// </summary>
    public static class PngWriter
    {
        public static void CaptureScreen(string path)
        {
            int w = Screen.width, h = Screen.height;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply(false);
            var px = tex.GetPixels32();
            Object.Destroy(tex);

            // 행마다 필터 바이트(0) + RGB. 텍스처는 아래에서 위로, PNG 는 위에서 아래로.
            var raw = new byte[h * (w * 3 + 1)];
            int o = 0;
            for (int y = h - 1; y >= 0; y--)
            {
                raw[o++] = 0;
                for (int x = 0; x < w; x++)
                {
                    var c = px[y * w + x];
                    raw[o++] = c.r;
                    raw[o++] = c.g;
                    raw[o++] = c.b;
                }
            }

            using (var fs = File.Create(path))
            {
                fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
                var ihdr = new byte[13];
                Put(ihdr, 0, (uint)w);
                Put(ihdr, 4, (uint)h);
                ihdr[8] = 8; // 비트 깊이
                ihdr[9] = 2; // RGB
                Chunk(fs, "IHDR", ihdr);
                Chunk(fs, "IDAT", Zlib(raw));
                Chunk(fs, "IEND", new byte[0]);
            }
        }

        static byte[] Zlib(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0x78);
                ms.WriteByte(0x9C);
                using (var ds = new DeflateStream(ms, System.IO.Compression.CompressionLevel.Fastest, true))
                    ds.Write(data, 0, data.Length);
                uint a = 1, b = 0;
                foreach (var d in data)
                {
                    a = (a + d) % 65521;
                    b = (b + a) % 65521;
                }
                var adler = new byte[4];
                Put(adler, 0, (b << 16) | a);
                ms.Write(adler, 0, 4);
                return ms.ToArray();
            }
        }

        static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            Put(len, 0, (uint)data.Length);
            s.Write(len, 0, 4);
            var td = new byte[4 + data.Length];
            for (int i = 0; i < 4; i++) td[i] = (byte)type[i];
            System.Array.Copy(data, 0, td, 4, data.Length);
            s.Write(td, 0, td.Length);
            var crc = new byte[4];
            Put(crc, 0, Crc(td));
            s.Write(crc, 0, 4);
        }

        static uint[] crcTable;

        static uint Crc(byte[] buf)
        {
            if (crcTable == null)
            {
                crcTable = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    crcTable[n] = c;
                }
            }
            uint crc = 0xFFFFFFFFu;
            foreach (var x in buf) crc = crcTable[(crc ^ x) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }

        static void Put(byte[] b, int i, uint v)
        {
            b[i] = (byte)(v >> 24);
            b[i + 1] = (byte)(v >> 16);
            b[i + 2] = (byte)(v >> 8);
            b[i + 3] = (byte)v;
        }
    }
}
