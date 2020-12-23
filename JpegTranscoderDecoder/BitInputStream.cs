using System;
using System.IO;

namespace JpegTranscoderDecoder
{
    public class BitInputStream
    {
        private const int HEADER_SIZE = 14;

        private readonly Stream _input;

        private int _currentByte;

        private int _numBitsRemaining;

        public BitInputStream(Stream inStream)
        {
            _input = inStream;
            _currentByte = 0;
            _numBitsRemaining = 0;
        }

        public int Read()
        {
            if (_currentByte == -1)
                return -1;
            if (_numBitsRemaining == 0)
            {
                _currentByte = _input.ReadByte();
                if (_currentByte == -1)
                    return -1;
                _numBitsRemaining = 8;
            }

            _numBitsRemaining--;
            return (_currentByte >> _numBitsRemaining) & 1;
        }

        public Tuple<int, int> ReadSize()
        {
            var width = 0;
            var height = 0;
            for (int i = 0; i < 2; i++)
            {
                var b = _input.ReadByte();
                width = (width << 8) | b;
            }

            for (int i = 0; i < 2; i++)
            {
                var b = _input.ReadByte();
                height = (height << 8) | b;
            }

            return Tuple.Create(width, height);
        }

        public Tuple<int, int> ReadHeader()
        {
            var qFactor = _input.ReadByte();
            var comp = _input.ReadByte();

            return Tuple.Create(qFactor, comp);
        }

        public Tuple<int, int> ReadSystemHeader()
        {
            var sizeDc = 0;
            var sizeAc = 0;
            for (int i = 0; i < 4; i++)
            {
                var b = _input.ReadByte();
                sizeDc = (sizeDc << 8) | b;
            }

            for (int i = 0; i < 4; i++)
            {
                var b = _input.ReadByte();
                sizeAc = (sizeAc << 8) | b;
            }

            return Tuple.Create(sizeDc, sizeAc);
        }

        public void Shift(int position)
        {
            _input.Position = position + HEADER_SIZE;
            _currentByte = 0;
            _numBitsRemaining = 0;
        }

        public void Close()
        {
            _input.Close();
            _currentByte = -1;
            _numBitsRemaining = 0;
        }
    }
}