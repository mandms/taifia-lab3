namespace lab3
{
    public class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                return 1;
            }

            string inFile = args[0];
            string outFile = args[1];

            GrammarToNKA.ToNKA(inFile, outFile);
            return 0;
        }
    }
}
