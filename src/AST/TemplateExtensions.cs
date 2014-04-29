using System;
using System.Collections.Generic;
using System.Linq;

namespace CppSharp.AST
{
    public static class TemplateExtensions
    {
        class Substitutor : ITypeVisitor<Type>
        {
            public readonly Dictionary<TemplateParameter, TemplateArgument> TypeMap;

            public Substitutor(Dictionary<TemplateParameter, TemplateArgument> typeMap)
            {
                TypeMap = typeMap;
            }

            public QualifiedType VisitQualifiedType(QualifiedType qualifiedType)
            {
                return new QualifiedType()
                {
                    Qualifiers = qualifiedType.Qualifiers,
                    Type = qualifiedType.Type.Visit(this)
                };
            }

            public Type VisitTagType(TagType tag, TypeQualifiers quals)
            {
                return new TagType(tag.Declaration)
                {
                    IsDependent = tag.IsDependent
                };
            }

            public Type VisitArrayType(ArrayType array, TypeQualifiers quals)
            {
                var elementType = array.Type.Visit(this);
                return new ArrayType()
                {
                    IsDependent = elementType.IsDependent,
                    Size = array.Size,
                    SizeType = array.SizeType,
                    Type = elementType
                };
            }

            public Type VisitFunctionType(FunctionType function, TypeQualifiers quals)
            {
                var result = new FunctionType()
                {
                    CallingConvention = function.CallingConvention,
                    Parameters = function.Parameters
                        .Select(p => new Parameter(p) 
                            {
                                QualifiedType = VisitQualifiedType(p.QualifiedType)
                            })
                        .ToList(),
                    ReturnType = VisitQualifiedType(function.ReturnType)
                };
                result.IsDependent = result.ReturnType.Type.IsDependent ||
                    result.Parameters.Any(p => p.Type.IsDependent);
                return result;
            }

            public Type VisitPointerType(PointerType pointer, TypeQualifiers quals)
            {
                var qualifiedPointee = VisitQualifiedType(pointer.QualifiedPointee);
                return new PointerType()
                {
                    IsDependent = qualifiedPointee.Type.IsDependent,
                    Modifier = pointer.Modifier,
                    QualifiedPointee = qualifiedPointee
                };
            }

            public Type VisitMemberPointerType(MemberPointerType member, TypeQualifiers quals)
            {
                var pointee = member.Pointee.Visit(this);
                return new MemberPointerType()
                {
                    IsDependent = pointee.IsDependent,
                    Pointee = pointee
                };
            }

            public Type VisitBuiltinType(BuiltinType builtin, TypeQualifiers quals)
            {
                return builtin;
            }

            public Type VisitTypedefType(TypedefType typedef, TypeQualifiers quals)
            {
                return typedef;
            }

            public Type VisitAttributedType(AttributedType attributed, TypeQualifiers quals)
            {
                var euivalent = VisitQualifiedType(attributed.Equivalent);
                var modifed = VisitQualifiedType(attributed.Modified);
                return new AttributedType()
                {
                    Equivalent = euivalent,
                    IsDependent = euivalent.Type.IsDependent || modifed.Type.IsDependent,
                    Modified = modifed
                };
            }

            public Type VisitDecayedType(DecayedType decayed, TypeQualifiers quals)
            {
                var decayedType = VisitQualifiedType(decayed.Decayed);
                var original = VisitQualifiedType(decayed.Original);
                var pointee = VisitQualifiedType(decayed.Pointee);
                return new DecayedType()
                {
                    Decayed = decayedType,
                    IsDependent = decayed.IsDependent || original.Type.IsDependent || pointee.Type.IsDependent,
                    Original = original,
                    Pointee = pointee
                };
            }

            public Type VisitTemplateSpecializationType(TemplateSpecializationType template, TypeQualifiers quals)
            {
                var arguments = template.Arguments
                    .Select(a => VisitTemplateArgument(a))
                    .ToList();
                return new TemplateSpecializationType()
                {
                    Arguments = arguments,
                    Desugared = template.Desugared != null
                        ? template.Desugared.Visit(this)
                        : null,
                    IsDependent = arguments.Any(a => a.Kind == TemplateArgument.ArgumentKind.Type && a.Type.Type.IsDependent),
                    Template = template.Template
                };
            }

            public Type VisitPrimitiveType(PrimitiveType type, TypeQualifiers quals)
            {
                return new BuiltinType(type);
            }

            public Type VisitDeclaration(Declaration decl, TypeQualifiers quals)
            {
                throw new NotImplementedException();
            }

            public Type VisitTemplateParameterType(TemplateParameterType param, TypeQualifiers quals)
            {
                TemplateArgument replacement;
                if (TypeMap.TryGetValue(param.Parameter, out replacement))
                    return replacement.Type.Type;
                else
                    return param;
            }

            public Type VisitTemplateParameterSubstitutionType(TemplateParameterSubstitutionType param, TypeQualifiers quals)
            {
                var replacement = VisitQualifiedType(param.Replacement);
                return new TemplateParameterSubstitutionType()
                {
                    IsDependent = replacement.Type.IsDependent,
                    Replacement = replacement
                };
            }

            public Type VisitInjectedClassNameType(InjectedClassNameType injected, TypeQualifiers quals)
            {
                var templateSpecialization = injected.TemplateSpecialization.Visit(this) as TemplateSpecializationType;
                return new InjectedClassNameType()
                {
                    Class = injected.Class,
                    IsDependent = templateSpecialization != null
                        ? templateSpecialization.IsDependent
                        : false,
                    TemplateSpecialization = templateSpecialization
                };
            }

            public Type VisitDependentNameType(DependentNameType dependent, TypeQualifiers quals)
            {
                return dependent;
            }

            public Type VisitPackExpansionType(PackExpansionType packExpansionType, TypeQualifiers quals)
            {
                return packExpansionType;
            }

            public Type VisitCILType(CILType type, TypeQualifiers quals)
            {
                return type;
            }

            public TemplateArgument VisitTemplateArgument(TemplateArgument argument)
            {
                switch (argument.Kind)
                {
                    case TemplateArgument.ArgumentKind.Type:
                        return new TemplateArgument()
                        {
                            Kind = argument.Kind,
                            Type = VisitQualifiedType(argument.Type)
                        };
                    case TemplateArgument.ArgumentKind.Declaration:
                        return new TemplateArgument()
                        {
                            Kind = argument.Kind,
                            Declaration = argument.Declaration
                        };
                    case TemplateArgument.ArgumentKind.Integral:
                        return new TemplateArgument()
                        {
                            Kind = argument.Kind,
                            Integral = argument.Integral
                        };
                    default:
                        return new TemplateArgument()
                        {
                            Kind = argument.Kind
                        };
                }
            }
        }

        public static FunctionTemplateSpecialization Instantiate(this FunctionTemplate template, params TemplateArgument[] arguments)
        {
            if (template.Parameters.Count != arguments.Length)
                throw new ArgumentException();
            var typeMap = template.Parameters
                .Select((p, i) => new 
                    { Parameter = p, Argument = arguments[i] })
                .ToDictionary(x => x.Parameter, x => x.Argument);
            var templatedFunction = template.TemplatedFunction;
            var templatedMethod = templatedFunction as Method;
            Function instance = templatedMethod != null
                ? new Method(templatedMethod)
                : new Function(templatedFunction);
            var specializationInfo = new FunctionTemplateSpecialization()
            {
                Arguments = arguments.ToList(),
                SpecializationKind = TemplateSpecializationKind.ImplicitInstantiation,
                SpecializedFunction = instance,
                Template = template
            };
            instance.SpecializationInfo = specializationInfo;
            var substitutor = new Substitutor(typeMap);
            instance.ReturnType = substitutor.VisitQualifiedType(instance.ReturnType);
            
            if (templatedMethod != null)
            {
                var method = instance as Method;
                if (templatedMethod.ConversionType.Type != null)
                    method.ConversionType = substitutor.VisitQualifiedType(templatedMethod.ConversionType);
            }
            for (int i = 0, c = instance.Parameters.Count; i < c; i++)
            {
                instance.Parameters[i] = new Parameter(instance.Parameters[i])
                {
                    QualifiedType = substitutor.VisitQualifiedType(instance.Parameters[i].QualifiedType)
                };
            }
            instance.IsDependent = instance.ReturnType.Type.IsDependent ||
                instance.Parameters.Any(p => p.Type.IsDependent);
            return specializationInfo;
        }
    }
}
