namespace JpegTranscoderDecoder
{
    public class Context {
        public readonly IFrequencyTable Frequencies;

        public Context[] Subcontexts;

        public Context(bool hasSubctx)
        {
            Frequencies = new SimpleFrequencyTable(new int[257]);
            Subcontexts = hasSubctx ? new Context[257] : null;
        }
    }
}