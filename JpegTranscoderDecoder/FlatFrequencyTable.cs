using System;

namespace JpegTranscoderDecoder
{
    public class FlatFrequencyTable : IFrequencyTable
    {
        private const int NumSymbols = 257;

        public int GetTotal() => NumSymbols;

        public int GetLow(int symbol) => symbol;

        public int GetSymbolLimit() => 257;

        public int GetHigh(int symbol) => symbol + 1;

        public void Increment(int symbol) => new InvalidOperationException();
    }
}