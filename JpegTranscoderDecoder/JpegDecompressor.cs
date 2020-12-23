using System;
using System.Collections.Generic;

namespace JpegTranscoderDecoder
{
    class JpegDecompressor
    {
        private BitInputStream _inputStream;

        private PpmDecompressor ppmDecoder;

        private Tuple<int, int> systemHeader;

        public int Width;

        public int Height;

        public int QFactor;

        public int Comp;

        public JpegDecompressor(BitInputStream inStream)
        {
            _inputStream = inStream;
            var size = _inputStream.ReadSize();
            Width = size.Item1;
            Height = size.Item2;
            var header = _inputStream.ReadHeader();
            QFactor = header.Item1;
            Comp = header.Item2;
            systemHeader = _inputStream.ReadSystemHeader();
            ppmDecoder = new PpmDecompressor(inStream);
        }

        public int[] DecompressDCLevel()
        {
            var result = new List<int>();
            var symbol = ppmDecoder.DecompressSymbol();
            while (symbol != 256)
            {
                result.Add(symbol >> 4);
                result.Add(symbol & 15);
                symbol = ppmDecoder.DecompressSymbol();
            }

            return result.ToArray();
        }

        private int[] DecompressDC(int[] levels)
        {
            int[] result = new int[levels.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                var value = 0;
                var isPositive = false;
                if (levels[i] == 0)
                {
                    result[i] = 0;
                    continue;
                }

                for (int j = 0; j < levels[i]; j++)
                {
                    var bit = _inputStream.Read();
                    if (j == 0)
                        isPositive = bit == 1 ? true : false;
                    value = (value << 1) | bit;
                }

                if (isPositive)
                    result[i] = value;
                else
                {
                    result[i] = value - (int) Math.Pow(2, levels[i]) + 1;
                }
            }

            return DeltaFilter(result);
        }

        public List<RunLevel> DecompressACLevel()
        {
            var result = new List<RunLevel>();
            var symbol = ppmDecoder.DecompressSymbol();
            while (symbol != 256)
            {
                result.Add(new RunLevel(symbol >> 4, symbol & 15));
                symbol = ppmDecoder.DecompressSymbol();
            }

            return result;
        }

        private int ReadInt(int level)
        {
            var value = 0;
            var isPositive = false;
            for (int j = 0; j < level; j++)
            {
                var bit = _inputStream.Read();
                if (j == 0)
                    isPositive = bit == 1;
                value = (value << 1) | bit;
            }

            if (isPositive)
                return value;

            return value - (int) Math.Pow(2, level) + 1;
        }

        public int[][] DecompressAC(List<RunLevel> runLevels, int[] dc)
        {
            var result = new int[dc.Length][];
            var i = 0;
            var k = 1;
            var current = new int[64];
            for (var j = 0; j < runLevels.Count; j++)
            {
                if (runLevels[j].LevelLength == 0 && runLevels[j].Run == 0 && k <= 63)
                {
                    current[0] = dc[i];
                    result[i] = (int[]) current.Clone();
                    current = new int[64];
                    i++;
                    k = 1;
                    continue;
                }

                if (k > 63)
                {
                    current[0] = dc[i];
                    result[i] = (int[]) current.Clone();
                    current = new int[64];
                    i++;
                    k = 1;
                    j--;
                    continue;
                }

                k += runLevels[j].Run;
                var ac = ReadInt(runLevels[j].LevelLength);
                current[k] = ac;
                k++;
            }

            for (int j = i; j < dc.Length; j++)
            {
                result[j] = (int[])current.Clone(); //new int[64];
                result[j][0] = dc[i];
            }

            return result;
        }

        private static int[] DeltaFilter(int[] deltaValues)
        {
            var values = new int[deltaValues.Length];
            values[0] = deltaValues[0];
            for (var j = 1; j < deltaValues.Length; j++)
            {
                values[j] = values[j - 1] + deltaValues[j];
            }

            return values;
        }

        public void Decompress(string jpegPath)
        {
            var dcLevel = DecompressDCLevel();
            _inputStream.Shift(systemHeader.Item1);
            ppmDecoder = new PpmDecompressor(_inputStream);
            var acLevel = DecompressACLevel();
            _inputStream.Shift(systemHeader.Item1 + systemHeader.Item2);
            var dc = DecompressDC(dcLevel);
            var result = DecompressAC(acLevel, dc);

            var encoder = new JpegEncoder();
            encoder.jo_write_jpg(jpegPath, result, Width, Height, Comp, QFactor);

        }
    }
}