using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PowerPosition.Portable
{
    public class Program
    {
        static void Main(string[] args)
        {
            var extractor = new Core.Extractor();
            extractor.Start();

            Console.ReadKey();
            extractor.Stop();
        }
    }
}
