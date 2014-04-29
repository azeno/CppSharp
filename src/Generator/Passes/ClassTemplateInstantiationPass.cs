using System.Linq;
using CppSharp.AST;
using CppSharp.AST.Extensions;
using CppSharp.Generators;
using System.Collections.Generic;
using CppSharp.Types;
using CppSharp.Generators.CLI;

namespace CppSharp.Passes
{
    /// <summary>
    /// This pass will instantiate class templates if it can find a proper
    /// typedef for it. See CLITemp.Native project for an example.
    /// </summary>
    public class ClassTemplateInstantiationPass : TranslationUnitPass
    {
        class TypeDefTypeMap : TypeMap
        {
            public readonly Class Class;
            public readonly CLITypePrinter TypePrinter;

            public TypeDefTypeMap(Class @class, CLITypePrinter typePrinter)
            {
                Class = @class;
                TypePrinter = typePrinter;
            }

            public override string CLISignature(Generators.CLI.CLITypePrinterContext ctx)
            {
                return TypePrinter.VisitDeclaration(Class);
            }

            public override void CLIMarshalToManaged(MarshalContext ctx)
            {
                Class.Visit(ctx.MarshalToManaged);
            }

            public override void CLIMarshalToNative(MarshalContext ctx)
            {
                Class.Visit(ctx.MarshalToNative);
            }
        }

        private readonly List<Class> instantiatedClasses = new List<Class>();

        public override bool VisitLibrary(ASTContext context)
        {
            instantiatedClasses.Clear();
            var ret = base.VisitLibrary(context);
            foreach (var @class in instantiatedClasses)
                @class.Namespace.Classes.Add(@class);
            return ret;
        }

        public override bool VisitClassTemplateDecl(ClassTemplate template)
        {
            if (!VisitDeclaration(template))
                return false;
            if (template.Ignore)
                return false;
            if (!template.TranslationUnit.IsGenerated || template.TranslationUnit.IsSystemHeader)
                return false;

            foreach (var instantiation in template.Specializations)
            {
                if (instantiation.IsDependent)
                    continue;
                
                // TODO: Handle explicit instantiations
                foreach (var typeDef in template.Namespace.Typedefs)
                {
                    if (typeDef.Ignore)
                        continue;

                    var templateSpecializationType = GetTemplateSpecializationType(typeDef);
                    if (templateSpecializationType == null)
                        continue;
                    Class c;
                    if (!templateSpecializationType.Desugared.IsTagDecl(out c))
                        continue;
                    if (c != instantiation)
                        continue;

                    instantiation.Name = typeDef.Name;
                    instantiation.OriginalName = typeDef.OriginalName;
                    instantiatedClasses.Add(instantiation);

                    var typePrinter = new CLITypePrinter(Driver);
                    var typeMap = new TypeDefTypeMap(instantiation, typePrinter)
                    {
                        Type = templateSpecializationType,
                        TypeMapDatabase = Driver.TypeDatabase
                    };
                    Driver.TypeDatabase.AddTypeMap(templateSpecializationType, typeMap);

                    break;
                }
            }
            return base.VisitClassTemplateDecl(template);
        }

        private TemplateSpecializationType GetTemplateSpecializationType(TypedefDecl typeDef)
        {
            var result = typeDef.Type as TemplateSpecializationType;
            if (result != null)
                return result;
            var typeDefType = typeDef.Type as TypedefType;
            if (typeDefType != null)
                return GetTemplateSpecializationType(typeDefType.Declaration);
            return null;
        }
    }
}
