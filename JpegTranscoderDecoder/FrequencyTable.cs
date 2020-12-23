namespace JpegTranscoderDecoder
{
    public interface IFrequencyTable {
        void Increment(int symbol);
        int GetSymbolLimit();
        int GetTotal();
        int GetLow(int symbol);
        int GetHigh(int symbol);
	
    }
}
