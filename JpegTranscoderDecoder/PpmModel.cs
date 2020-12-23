
namespace JpegTranscoderDecoder
{
    public class PpmModel
    {
        public int ModelOrder;

        private readonly int _escapeSymbol;

        public Context RootContext;
        public IFrequencyTable orderMinus1Freqs;

        public PpmModel(int order, int escapeSymbol)
        {
            this.ModelOrder = order;
            this._escapeSymbol = escapeSymbol;

            if (order >= 0)
            {
                RootContext = new Context(order >= 1);
                RootContext.Frequencies.Increment(escapeSymbol);
            }
            else
                RootContext = null;

            orderMinus1Freqs = new FlatFrequencyTable();
        }

        public void IncrementContexts(int[] history, int symbol)
        {
            if (ModelOrder == -1)
                return;

            var ctx = RootContext;
            ctx.Frequencies.Increment(symbol);
            var i = 0;
            foreach (var sym in history)
            {
                var subctxs = ctx.Subcontexts;

                if (subctxs[sym] == null)
                {
                    subctxs[sym] = new Context(i + 1 < ModelOrder);
                    subctxs[sym].Frequencies.Increment(_escapeSymbol);
                }

                ctx = subctxs[sym];
                ctx.Frequencies.Increment(symbol);
                i++;
            }
        }
    }
}