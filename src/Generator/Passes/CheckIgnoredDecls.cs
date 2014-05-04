﻿using CppSharp.AST;
using CppSharp.AST.Extensions;
using CppSharp.Types;
using System.Collections.Generic;
using System.Linq;

namespace CppSharp.Passes
{
    public class CheckIgnoredDeclsPass : TranslationUnitPass
    {
        readonly Stack<TemplateParameter> CurrentParameters = new Stack<TemplateParameter>();

        public bool CheckDeclarationAccess(Declaration decl)
        {
            var generateNonPublicDecls = Driver.Options.IsCSharpGenerator;

            switch (decl.Access)
            {
            case AccessSpecifier.Public:
                return true;
            case AccessSpecifier.Protected:
                var @class = decl.Namespace as Class;
                if (@class != null && @class.IsValueType)
                    return false;
                return generateNonPublicDecls;
            case AccessSpecifier.Private:
                var method = decl as Method;
                var isOverride = method != null && method.IsOverride;
                return generateNonPublicDecls && isOverride;
            }

            return true;
        }

        public override bool VisitDeclaration(Declaration decl)
        {
            if (AlreadyVisited(decl))
                return false;

            if (decl.ExplicityIgnored)
                return true;

            if (!CheckDeclarationAccess(decl))
            {
                Log.Debug("Decl '{0}' was ignored due to invalid access",
                    decl.Name);
                decl.ExplicityIgnored = true;
                return true;
            }

            if (decl.IsDependent && CurrentParameters.Count == 0)
            {
                decl.ExplicityIgnored = true;
                Log.Debug("Decl '{0}' was ignored due to dependent context",
                    decl.Name);
                return true;
            }

            return true;
        }

        public override bool VisitFieldDecl(Field field)
        {
            if (!VisitDeclaration(field))
                return false;

            var type = field.Type;

            string msg;
            if (!HasInvalidType(type, out msg))
                return false;

            field.ExplicityIgnored = true;

            var @class = (Class)field.Namespace;

            var cppTypePrinter = new CppTypePrinter(Driver.TypeDatabase);
            var typeName = type.Visit(cppTypePrinter);

            Log.Debug("Field '{0}::{1}' was ignored due to {2} type '{3}'",
                @class.Name, field.Name, msg, typeName);

            return true;
        }

        public override bool VisitFunctionDecl(Function function)
        {
            if (!VisitDeclaration(function))
                return false;

            var ret = function.ReturnType;

            string msg;
            if (HasInvalidType(ret.Type, out msg))
            {
                function.ExplicityIgnored = true;
                Log.Debug("Function '{0}' was ignored due to {1} return decl",
                    function.QualifiedOriginalName, msg);
                return false;
            }

            foreach (var param in function.Parameters)
            {
                if (HasInvalidDecl(param, out msg))
                {
                    function.ExplicityIgnored = true;
                    Log.Debug("Function '{0}' was ignored due to {1} param",
                        function.QualifiedOriginalName, msg);
                    return false;
                }

                if (HasInvalidType(param.Type, out msg))
                {
                    function.ExplicityIgnored = true;
                    Log.Debug("Function '{0}' was ignored due to {1} param",
                        function.QualifiedOriginalName, msg);
                    return false;
                }

                var decayedType = param.Type.Desugar() as DecayedType;
                if (decayedType != null)
                {
                    function.ExplicityIgnored = true;
                    Log.Debug("Function '{0}' was ignored due to unsupported decayed type param",
                        function.QualifiedOriginalName);
                    return false;
                }

                if (param.Kind == ParameterKind.IndirectReturnType)
                {
                    Class retClass;
                    param.Type.Desugar().IsTagDecl(out retClass);
                    if (retClass == null)
                    {
                        function.ExplicityIgnored = true;
                        Log.Debug(
                            "Function '{0}' was ignored due to an indirect return param not of a tag type",
                            function.QualifiedOriginalName);
                        return false;
                    }
                }
            }

            return true;
        }

        public override bool VisitMethodDecl(Method method)
        {
            if (!CheckIgnoredBaseOverridenMethod(method))
                return false;

            if (method.IsDependent && method.IsConstructor)
            {
                method.ExplicityIgnored = true;
                Log.Debug("Constructor '{0}' was ignored due to dependent context",
                    method.Name);
                return true;
            }

            return base.VisitMethodDecl(method);
        }

        public override bool VisitClassTemplateDecl(ClassTemplate template)
        {
            string msg;
            if (!HasInvalidTemplateParameters(template, out msg))
            {
                Log.Debug(
                    "Class template '{0}' was ignored due to {1}.",
                    template, msg);

                Ignore(template);
                return false;
            }
            foreach (var param in template.Parameters)
                CurrentParameters.Push(param);
            var result = base.VisitClassTemplateDecl(template);
            foreach (var param in template.Parameters)
                CurrentParameters.Pop();
            return result;
        }

        public override bool VisitFunctionTemplateDecl(FunctionTemplate template)
        {
            if (template.TemplatedFunction.IsOperator)
            {
                Log.Debug(
                    "Function template '{0}' was ignored due being an operator.",
                    template);
                Ignore(template);
                return false;
            }
            string msg;
            if (!HasInvalidTemplateParameters(template, out msg))
            {
                Log.Debug(
                    "Function template '{0}' was ignored due to {1}.",
                    template, msg);

                Ignore(template);
                return false;
            }
            foreach (var param in template.Parameters)
                CurrentParameters.Push(param);
            var result = base.VisitFunctionTemplateDecl(template);
            foreach (var param in template.Parameters)
                CurrentParameters.Pop();
            if (template.TemplatedFunction.Ignore)
                template.ExplicityIgnored = true;
            return result;
        }

        private static void Ignore(Template template)
        {
            template.ExplicityIgnored = true;
            template.TemplatedDecl.ExplicityIgnored = true;
        }

        private bool HasInvalidTemplateParameters(Template template, out string msg)
        {
            if (template.Parameters.Any(p => !p.IsTypeParameter))
            {
                msg = "containing non-type parameters";
                return false;
            }
            if (template.Parameters.Count > 1)
            {
                msg = "containing more than one type parameter";
                return false;
            }
            msg = null;
            return true;
        }

        bool CheckIgnoredBaseOverridenMethod(Method method)
        {
            var @class = method.Namespace as Class;

            if (method.IsVirtual)
            {
                Class ignoredBase;
                if (HasIgnoredBaseClass(method, @class, out ignoredBase))
                {
                    Log.Debug(
                        "Virtual method '{0}' was ignored due to ignored base '{1}'",
                        method.QualifiedOriginalName, ignoredBase.Name);

                    method.ExplicityIgnored = true;
                    return false;
                }

                if (method.IsOverride)
                {
                    var baseOverride = @class.GetRootBaseMethod(method);
                    if (baseOverride != null && baseOverride.Ignore)
                    {
                        Log.Debug(
                            "Virtual method '{0}' was ignored due to ignored override '{1}'",
                            method.QualifiedOriginalName, baseOverride.Name);

                        method.ExplicityIgnored = true;
                        return false;
                    }
                }
            }

            return true;
        }

        static bool HasIgnoredBaseClass(INamedDecl @override, Class @class,
            out Class ignoredBase)
        {
            var isIgnored = false;
            ignoredBase = null;

            foreach (var baseClassSpec in @class.Bases)
            {
                if (!baseClassSpec.IsClass)
                    continue;

                var @base = baseClassSpec.Class;
                if (!@base.Methods.Exists(m => m.Name == @override.Name))
                    continue;

                ignoredBase = @base;
                isIgnored |= @base.Ignore
                    || HasIgnoredBaseClass(@override, @base, out ignoredBase);

                if (isIgnored)
                    break;
            }

            return isIgnored;
        }

        public override bool VisitTypedefDecl(TypedefDecl typedef)
        {
            if (!VisitDeclaration(typedef))
                return false;

            string msg;
            if (HasInvalidType(typedef.Type, out msg))
            {
                typedef.ExplicityIgnored = true;
                Log.Debug("Typedef '{0}' was ignored due to {1} type",
                    typedef.Name, msg);
                return false;
            }

            return true;
        }

        public override bool VisitProperty(Property property)
        {
            if (!VisitDeclaration(property))
                return false;

            string msg;
            if (HasInvalidDecl(property, out msg))
            {
                property.ExplicityIgnored = true;
                Log.Debug("Property '{0}' was ignored due to {1} decl",
                    property.Name, msg);
                return false;
            }

            if (HasInvalidType(property.Type, out msg))
            {
                property.ExplicityIgnored = true;
                Log.Debug("Property '{0}' was ignored due to {1} type",
                    property.Name, msg);
                return false;
            }

            return true;
        }

        public override bool VisitVariableDecl(Variable variable)
        {
            if (!VisitDeclaration(variable))
                return false;

            string msg;
            if (HasInvalidDecl(variable, out msg))
            {
                variable.ExplicityIgnored = true;
                Log.Debug("Variable '{0}' was ignored due to {1} decl",
                    variable.Name, msg);
                return false;
            }

            if (HasInvalidType(variable.Type, out msg))
            {
                variable.ExplicityIgnored = true;
                Log.Debug("Variable '{0}' was ignored due to {1} type",
                    variable.Name, msg);
                return false;
            }

            return true;
        }

        public override bool VisitEvent(Event @event)
        {
            if (!VisitDeclaration(@event))
                return false;

            string msg;
            if (HasInvalidDecl(@event, out msg))
            {
                @event.ExplicityIgnored = true;
                Log.Debug("Event '{0}' was ignored due to {1} decl",
                    @event.Name, msg);
                return false;
            }

            foreach (var param in @event.Parameters)
            {
                if (HasInvalidDecl(param, out msg))
                {
                    @event.ExplicityIgnored = true;
                    Log.Debug("Event '{0}' was ignored due to {1} param",
                        @event.Name, msg);
                    return false;
                }

                if (HasInvalidType(param.Type, out msg))
                {
                    @event.ExplicityIgnored = true;
                    Log.Debug("Event '{0}' was ignored due to {1} param",
                        @event.Name, msg);
                    return false;
                }
            }

            return true;
        }

        #region Helpers

        /// <remarks>
        /// Checks if a given type is invalid, which can happen for a number of
        /// reasons: incomplete definitions, being explicitly ignored, or also
        /// by being a type we do not know how to handle.
        /// </remarks>
        bool HasInvalidType(AST.Type type, out string msg)
        {
            if (type == null)
            {
                msg = "null";
                return true;
            }

            if (!IsTypeComplete(type))
            {
                msg = "incomplete";
                return true;
            }

            if (IsTypeIgnored(type))
            {
                msg = "ignored";
                return true;
            }

            var arrayType = type as ArrayType;
            PrimitiveType primitive;
            if (arrayType != null && arrayType.SizeType == ArrayType.ArraySize.Constant &&
                !arrayType.Type.IsPrimitiveType(out primitive) &&
                !arrayType.Type.Desugar().IsPointerToPrimitiveType())
            {
                msg = "unsupported";
                return true;
            }

            msg = null;
            return false;
        }

        bool HasInvalidDecl(Declaration decl, out string msg)
        {
            if (decl == null)
            {
                msg = "null";
                return true;
            }

            if (!IsDeclComplete(decl))
            {
                msg = "incomplete";
                return true;
            }

            if (IsDeclIgnored(decl))
            {
                msg = "ignored";
                return true;
            }

            msg = null;
            return false;
        }

        static bool IsTypeComplete(AST.Type type)
        {
            var checker = new TypeCompletionChecker();
            return type.Visit(checker);
        }

        static bool IsDeclComplete(Declaration decl)
        {
            var checker = new TypeCompletionChecker();
            return decl.Visit(checker);
        }

        bool IsTypeIgnored(AST.Type type)
        {
            var checker = new TypeIgnoreChecker(Driver.TypeDatabase, CurrentParameters);
            type.Visit(checker);

            return checker.IsIgnored;
        }

        bool IsDeclIgnored(Declaration decl)
        {
            var checker = new TypeIgnoreChecker(Driver.TypeDatabase, CurrentParameters);
            decl.Visit(checker);

            return checker.IsIgnored;
        }

        #endregion
    }
}
