using BenchmarkDotNet.Running;

namespace Mayordomo.Web.Extractor.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("Mayordomo.Web.Extractor Benchmarks");
            Console.WriteLine("===================================");
            Console.WriteLine();

            var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

            if (summaries.Any())
            {
                Console.WriteLine();
                Console.WriteLine("Benchmark run completed successfully.");
                Console.WriteLine($"Results saved to: BenchmarkDotNet.Artifacts\\results");
            }

            Console.ReadLine();
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.ToString());
            Console.ReadLine();
        }
    }
}