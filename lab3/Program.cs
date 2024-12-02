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

            string inFile = args[1];
            string outFile = args[2];

            GrammarToNKA.ToNKA(inFile, outFile);
            return 0;
        }
    }
}
