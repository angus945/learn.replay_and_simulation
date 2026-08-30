using System;

namespace GameplayLessons
{
    internal static class Program
    {
        private static readonly string[] Names = { "domain", "application", "simulation", "testability", "replay" };
        private static readonly Action[] Lessons = {
            Stage01Domain.Run, Stage02Application.Run, Stage03Simulation.Run,
            Stage04Testability.Run, Stage05Replay.Run
        };

        private static int Main(string[] arguments)
        {
            string selection = arguments.Length == 0 ? "all" : arguments[0].ToLowerInvariant();
            if (arguments.Length > 1 || selection == "--help" || selection == "help" || selection == "-h")
            {
                Usage();
                return arguments.Length > 1 ? 2 : 0;
            }
            int selected = Array.IndexOf(Names, selection);
            if (int.TryParse(selection, out int number) && number >= 1 && number <= Lessons.Length)
                selected = number - 1;
            if (selection != "all" && selected < 0)
            {
                Console.Error.WriteLine("Unknown stage: " + selection);
                Usage();
                return 2;
            }
            for (int index = 0; index < Lessons.Length; index++)
            {
                if (selection != "all" && index != selected) continue;
                try
                {
                    Lessons[index]();
                    Console.WriteLine("PASS " + (index + 1).ToString("00") + " " + Names[index]);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("FAIL " + Names[index] + ": " + exception);
                    return 1;
                }
            }
            return 0;
        }

        private static void Usage()
        {
            Console.WriteLine("dotnet run --project tools/gameplay-lessons -- <stage|all>");
            Console.WriteLine("Stages: 1/domain, 2/application, 3/simulation, 4/testability, 5/replay");
        }
    }
}
