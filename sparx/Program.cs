namespace sparx
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("sparx package manager v1.0");

#if DEBUG
            List<string> argsList = new List<string>();

            Console.WriteLine("Debug stuff");
            Console.WriteLine("Enter something and it will add an argument.");
            Console.WriteLine("Type $quit to start sparx");

            while (true)
            {
                string input = Console.ReadLine();

                if (input == "$quit")
                {
                    break;
                }

                argsList.Add(input);
            }

            args = argsList.ToArray();
#endif

            SparxApp app = new SparxApp();

            app.Start(args);
        }
    }
}