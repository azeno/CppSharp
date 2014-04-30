using CppSharp.AST;
using CppSharp.AST.Extensions;
using System.Linq;

namespace CppSharp.Passes
{
    public class MoveOperatorToClassPass : TranslationUnitPass
    {
        public override bool VisitMethodDecl(Method method)
        {
            // Ignore methods as they are not relevant for this pass.
            return true;
        }

        public override bool VisitFunctionDecl(Function function)
        {
            if (!function.IsOperator)
                return false;

            Class @class = null;
            ClassTemplate template = null;
            foreach (var param in function.Parameters)
            {
                FunctionToInstanceMethodPass.GetClassParameter(
                    param, out @class);

                if (@class != null) break;

                if (param.Type.IsPointerToTemplate(out template))
                    break;
            }

            if (@class == null && template == null)
                return false;

            // Create a new fake method so it acts as a static method.
            if (@class != null)
            {
                var method = CreateFakeFunction(function, @class);
                @class.Methods.Add(method);

                Driver.Diagnostics.Debug("Function converted to operator: {0}::{1}",
                    @class.Name, function.Name);
            }
            else
            {
                var functionTemplate = function.Namespace.Templates
                    .OfType<FunctionTemplate>()
                    .FirstOrDefault(t => t.TemplatedDecl == function);
                if (functionTemplate == null)
                    return false;
                if (functionTemplate.Parameters.Count != template.Parameters.Count)
                    return false;
                foreach (var specializedClass in template.Specializations)
                {
                    var arguments = specializedClass.Arguments.ToArray();
                    if (arguments.Length != functionTemplate.Parameters.Count)
                        continue;
                    var specialization = functionTemplate.Instantiate(arguments);
                    var method = CreateFakeFunction(specialization.SpecializedFunction, specializedClass);
                    specializedClass.Methods.Add(method);
                }

                Driver.Diagnostics.Debug("Function converted to operator: {0}::{1}",
                    template.Name, function.Name);
            }

            function.ExplicityIgnored = true;

            return true;
        }

        private static Method CreateFakeFunction(Function function, Class @class)
        {
            return new Method(function)
            {
                Namespace = @class,
                Kind = CXXMethodKind.Operator,
                OperatorKind = function.OperatorKind,
                SynthKind = FunctionSynthKind.NonMemberOperator,
                OriginalFunction = null,
                IsStatic = true
            };
        }
    }
}
