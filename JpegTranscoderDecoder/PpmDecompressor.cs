using System;

namespace JpegTranscoderDecoder
{
    public class PpmDecompressor
    {
        private const int ModelOrder = 1;

        public ArithmeticDecoder dec;
        public PpmModel model;
        private int[] history;

        public PpmDecompressor(BitInputStream inStream)
        {
            dec = new ArithmeticDecoder(inStream);
            model = new PpmModel(ModelOrder, 256);
            history = new int[0];
        }

        public int DecompressSymbol()
        {
            var symbol = DecodeSymbol(dec, model, history);
            model.IncrementContexts(history, symbol);

            if (model.ModelOrder >= 1)
            {
                if (history.Length < model.ModelOrder)
                    Array.Resize(ref history, history.Length + 1);
                Array.Copy(history, 0, history, 1, history.Length - 1);
                history[0] = symbol;
            }

            return symbol;
        }


        private static int DecodeSymbol(ArithmeticDecoder dec, PpmModel model, int[] history)
        {
            var order = history.Length;
            while (order >= 0)
            {
                var ctx = model.RootContext;
                var isBreak = false;
                for (var i = 0; i < order; i++)
                {
                    ctx = ctx.Subcontexts[history[i]];
                    if (ctx == null)
                    {
                        order--;
                        isBreak = true;
                        break;
                    }
                }

                if (isBreak)
                    continue;

                var symbol = dec.Read(ctx.Frequencies);
                if (symbol < 256)
                    return symbol;

                order--;
            }

            return dec.Read(model.orderMinus1Freqs);
        }
    }
}