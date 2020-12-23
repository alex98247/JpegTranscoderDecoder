namespace JpegTranscoderDecoder
{
    public class SimpleFrequencyTable : IFrequencyTable
    {
        private readonly int[] _frequencies;

        private int[] _cumulative;

        private int _total;

        public SimpleFrequencyTable(int[] freqs)
        {
            _frequencies = (int[]) freqs.Clone();
            _total = 0;
            foreach (var x in _frequencies)
            {
                _total = x + _total;
            }

            _cumulative = null;
        }

        public int GetSymbolLimit() => _frequencies.Length;

        public void Increment(int symbol)
        {
            _total += 1;
            _frequencies[symbol]++;
            _cumulative = null;
        }

        public int GetTotal()
        {
            return _total;
        }

        public int GetLow(int symbol)
        {
            if (_cumulative == null)
                InitCumulative();
            return _cumulative[symbol];
        }

        public int GetHigh(int symbol)
        {
            if (_cumulative == null)
                InitCumulative();
            return _cumulative[symbol + 1];
        }


        private void InitCumulative()
        {
            _cumulative = new int[_frequencies.Length + 1];
            var sum = 0;
            for (var i = 0; i < _frequencies.Length; i++)
            {
                sum = _frequencies[i] + sum;
                _cumulative[i + 1] = sum;
            }
        }
    }
}