using System.Collections.Generic;
using CppSharp.AST;
using CppSharp.Passes;
using CppSharp.Types;

namespace CppSharp.Generators.CSharp
{
    public class CSharpGenerator : Generator
    {
        private readonly CSharpTypePrinter typePrinter;
        private readonly CppTypePrinter nativeTypePrinter;
        private readonly CSharpExpressionPrinter expressionPrinter;

        public CSharpGenerator(Driver driver) : base(driver)
        {
            typePrinter = new CSharpTypePrinter(driver.TypeDatabase, driver.Options, driver.ASTContext);
            expressionPrinter = new CSharpExpressionPrinter();
            Type.TypePrinterDelegate += type => type.Visit(typePrinter).Type;
            nativeTypePrinter = new CppTypePrinter(driver.TypeDatabase);
            Type.NativeTypePrinterDelegate += type => type.Visit(nativeTypePrinter);
        }

        public override List<Template> Generate(TranslationUnit unit)
        {
            var outputs = new List<Template>();

            var template = new CSharpTextTemplate(Driver, unit, typePrinter, expressionPrinter);
            outputs.Add(template);

            return outputs;
        }

        public override bool SetupPasses()
        {
            // Both the CheckOperatorsOverloadsPass and CheckAbiParameters can
            // create and and new parameters to functions and methods. Make sure
            // CheckAbiParameters runs last because hidden structure parameters
            // should always occur first.

            Driver.AddTranslationUnitPass(new CheckAbiParameters(Driver.Options));

            return true;
        }
    }
}
