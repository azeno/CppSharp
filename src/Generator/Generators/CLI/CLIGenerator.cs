using System.Collections.Generic;
using CppSharp.AST;
using CppSharp.Types;

namespace CppSharp.Generators.CLI
{
    /// <summary>
    /// C++/CLI generator responsible for driving the generation of
    /// source and header files.
    /// </summary>
    public class CLIGenerator : Generator
    {
        private readonly CLITypePrinter typePrinter;
        private readonly CppTypePrinter nativeTypePrinter;

        public CLIGenerator(Driver driver) : base(driver)
        {
            typePrinter = new CLITypePrinter(driver);
            Type.TypePrinterDelegate += type => type.Visit(typePrinter);
            nativeTypePrinter = new CppTypePrinter(driver.TypeDatabase);
            Type.NativeTypePrinterDelegate += type => type.Visit(nativeTypePrinter);
        }

        public override List<Template> Generate(TranslationUnit unit)
        {
            var outputs = new List<Template>();

            var header = new CLIHeadersTemplate(Driver, unit);
            outputs.Add(header);

            var source = new CLISourcesTemplate(Driver, unit);
            outputs.Add(source);

            return outputs;
        }

        public override bool SetupPasses()
        {
            // Note: The ToString override will only work if this pass runs
            // after the MoveOperatorToCallPass.
            if (Driver.Options.GenerateObjectOverrides)
                Driver.TranslationUnitPasses.AddPass(new ObjectOverridesPass());
            return true;
        }
    }
}