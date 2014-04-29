using CppSharp.AST;
using CppSharp.AST.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CppSharp.Passes
{
    /// <summary>
    /// This pass will try to instantiate function templates with primitive types
    /// and object as fallback type. The generated function will expose these 
    /// template instantiations with a generic type parameter.
    /// See CLITemp.Native and CLITemp.Tests for an example.
    /// </summary>
    class FunctionTemplateInstantiationPass : TranslationUnitPass
    {
        private readonly PrimitiveType[] PrimitiveTypes = new[] {
            PrimitiveType.Bool,
            PrimitiveType.UInt8,
            PrimitiveType.Int16,
            PrimitiveType.UInt16,
            PrimitiveType.Int32,
            PrimitiveType.UInt32,
            PrimitiveType.Int64,
            PrimitiveType.UInt64,
            PrimitiveType.Float,
            PrimitiveType.Double
        };

        private static TemplateArgument CreateTemplateArgument(Type type)
        {
            return new TemplateArgument()
            {
                Type = new QualifiedType(type),
                Kind = TemplateArgument.ArgumentKind.Type
            };
        }

        private IEnumerable<TemplateArgument> GetInstantiationArguments()
        {
            foreach (var primitiveType in PrimitiveTypes)
                yield return CreateTemplateArgument(new BuiltinType(primitiveType));
            yield return CreateTemplateArgument(new CILType(typeof(object)));
        }

        public override bool VisitFunctionTemplateDecl(FunctionTemplate template)
        {
            if (!VisitDeclaration(template))
                return false;
            if (template.Ignore)
                return false;
            // We can only deal with templates having one parameter for now
            if (template.Parameters.Count != 1)
                return false;
            // We can only deal with type parameters
            if (!template.Parameters.All(p => p.IsTypeParameter))
                return false;
            // We can't instantiate methods of class templates yet
            var @class = template.Namespace as Class;
            if (@class != null && @class.IsDependent)
                return false;
            foreach (var type in GetInstantiationArguments())
            {
                var specializationInfo = template.Instantiate(type);
                template.Specializations.Add(specializationInfo);
            }
            return base.VisitFunctionTemplateDecl(template);
        }
    }
}
