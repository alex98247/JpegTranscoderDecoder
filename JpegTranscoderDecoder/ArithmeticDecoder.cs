namespace JpegTranscoderDecoder
{
    public class ArithmeticDecoder
    {
        private readonly long _halfRange;

        private readonly long _quarterRange;

        private readonly long _stateMask;

        private long _low;

        private long _high;

        public BitInputStream Input;

        private long _code;

        public ArithmeticDecoder(BitInputStream inputStream)
        {
            const long fullRange = 1L << 32;
            _halfRange = fullRange >> 1;
            _quarterRange = _halfRange >> 1;
            _stateMask = fullRange - 1;

            _low = 0;
            _high = _stateMask;

            Input = inputStream;
            _code = 0;
            for (var i = 0; i < 32; i++)
                _code = _code << 1 | ReadCodeBit();
        }

        public int Read(IFrequencyTable freqs)
        {
            long total = freqs.GetTotal();

            var range = _high - _low + 1;
            var offset = _code - _low;
            var value = ((offset + 1) * total - 1) / range;

            var start = 0;
            var end = freqs.GetSymbolLimit();
            while (end - start > 1)
            {
                var middle = (start + end) >> 1;
                if (freqs.GetLow(middle) > value)
                    end = middle;
                else
                    start = middle;
            }


            var symbol = start;
            Update(freqs, symbol);
            return symbol;
        }

        public void Update(IFrequencyTable freqs, int symbol)
        {
            var range = _high - _low + 1;

            long total = freqs.GetTotal();
            long symLow = freqs.GetLow(symbol);
            long symHigh = freqs.GetHigh(symbol);

            var newLow = _low + symLow * range / total;
            var newHigh = _low + symHigh * range / total - 1;
            _low = newLow;
            _high = newHigh;

            while (((_low ^ _high) & _halfRange) == 0)
            {
                Shift();
                _low = ((_low << 1) & _stateMask);
                _high = ((_high << 1) & _stateMask) | 1;
            }

            while ((_low & ~_high & _quarterRange) != 0)
            {
                Underflow();
                _low = (_low << 1) ^ _halfRange;
                _high = ((_high ^ _halfRange) << 1) | _halfRange | 1;
            }
        }

        protected void Shift()
        {
            _code = ((_code << 1) & _stateMask) | ReadCodeBit();
        }


        protected void Underflow()
        {
            _code = (_code & _halfRange) | ((_code << 1) & (_stateMask >> 1)) | ReadCodeBit();
        }

        private int ReadCodeBit()
        {
            var temp = Input.Read();
            if (temp == -1)
                temp = 0;
            return temp;
        }
    }
}