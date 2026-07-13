using NUnitLite;
using System.Linq;

namespace PersonalCloudLibrarySource.Tests
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var filteredArgs = args
                .Concat(new[] { "--test=PersonalCloudLibrarySource.Tests.Ui.UiContractTests.SourceSettings_ShowOnlyFieldsForSelectedProvider" })
                .ToArray();
            return new AutoRun().Execute(filteredArgs);
        }
    }
}
