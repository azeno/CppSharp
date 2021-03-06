using System;
using System.Collections.Generic;
using System.Linq;

namespace CppSharp.AST
{
    /// <summary>
    /// Represents a declaration context.
    /// </summary>
    public abstract class DeclarationContext : Declaration
    {
        public bool IsAnonymous { get; set; }

        public List<Namespace> Namespaces;
        public List<Enumeration> Enums;
        public List<Function> Functions;
        public List<Class> Classes;
        public List<Template> Templates;
        public List<TypedefDecl> Typedefs;
        public List<Variable> Variables;
        public List<Event> Events;
        public List<TypeReference> TypeReferences;

        // Used to keep track of anonymous declarations.
        public Dictionary<ulong, Declaration> Anonymous; 

        // True if the context is inside an extern "C" context.
        public bool IsExternCContext;

        public override string LogicalName
        {
            get { return IsAnonymous ? "<anonymous>" : base.Name; }
        }

        public override string LogicalOriginalName
        {
            get { return IsAnonymous ? "<anonymous>" : base.OriginalName; }
        }

        protected DeclarationContext()
        {
            Namespaces = new List<Namespace>();
            Enums = new List<Enumeration>();
            Functions = new List<Function>();
            Classes = new List<Class>();
            Templates = new List<Template>();
            Typedefs = new List<TypedefDecl>();
            Variables = new List<Variable>();
            Events = new List<Event>();
            TypeReferences = new List<TypeReference>();
            Anonymous = new Dictionary<ulong, Declaration>();
        }

        protected DeclarationContext(DeclarationContext dc)
            : base(dc)
        {
            Namespaces = new List<Namespace>(dc.Namespaces);
            Enums = new List<Enumeration>(dc.Enums);
            Functions = new List<Function>(dc.Functions);
            Classes = new List<Class>(dc.Classes);
            Templates = new List<Template>(dc.Templates);
            Typedefs = new List<TypedefDecl>(dc.Typedefs);
            Variables = new List<Variable>(dc.Variables);
            Events = new List<Event>(dc.Events);
            TypeReferences = new List<TypeReference>(dc.TypeReferences);
            Anonymous = new Dictionary<ulong, Declaration>(dc.Anonymous);
            IsAnonymous = dc.IsAnonymous;
        }

        public IEnumerable<DeclarationContext> GatherParentNamespaces()
        {
            var children = new List<DeclarationContext>();
            var currentNamespace = this;

            while (currentNamespace != null)
            {
                if (!(currentNamespace is TranslationUnit))
                    children.Add(currentNamespace);

                currentNamespace = currentNamespace.Namespace;
            }

            children.Reverse();
            return children;
        }

        public Declaration FindAnonymous(ulong key)
        {
            return Anonymous.ContainsKey(key) ? Anonymous[key] : null;
        }

        public DeclarationContext FindDeclaration(IEnumerable<string> declarations)
        {
            DeclarationContext currentDeclaration = this;

            foreach (var declaration in declarations)
            {
                var subDeclaration = currentDeclaration.Namespaces
                    .Concat<DeclarationContext>(currentDeclaration.Classes)
                    .FirstOrDefault(e => e.Name.Equals(declaration));

                if (subDeclaration == null)
                    return null;

                currentDeclaration = subDeclaration;
            }

            return currentDeclaration as DeclarationContext;
        }

        public Namespace FindNamespace(string name)
        {
            var namespaces = name.Split(new string[] { "::" },
                StringSplitOptions.RemoveEmptyEntries);

            return FindNamespace(namespaces);
        }

        public Namespace FindNamespace(IEnumerable<string> namespaces)
        {
            DeclarationContext currentNamespace = this;

            foreach (var @namespace in namespaces)
            {
                var childNamespace = currentNamespace.Namespaces.Find(
                    e => e.Name.Equals(@namespace));

                if (childNamespace == null)
                    return null;

                currentNamespace = childNamespace;
            }

            return currentNamespace as Namespace;
        }

        public Namespace FindCreateNamespace(string name)
        {
            var @namespace = FindNamespace(name);

            if (@namespace == null)
            {
                @namespace = new Namespace
                    {
                        Name = name,
                        Namespace = this,
                    };

                Namespaces.Add(@namespace);
            }

            return @namespace;
        }

        public Enumeration FindEnum(string name, bool createDecl = false)
        {
            var entries = name.Split(new string[] { "::" },
                StringSplitOptions.RemoveEmptyEntries).ToList();

            if (entries.Count <= 1)
            {
                var @enum = Enums.Find(e => e.Name.Equals(name));

                if (@enum == null && createDecl)
                {
                    @enum = new Enumeration() { Name = name, Namespace = this };
                    Enums.Add(@enum);
                }

                return @enum;
            }

            var enumName = entries[entries.Count - 1];
            var namespaces = entries.Take(entries.Count - 1);

            var @namespace = FindNamespace(namespaces);
            if (@namespace == null)
                return null;

            return @namespace.FindEnum(enumName, createDecl);
        }

        public Enumeration FindEnum(IntPtr ptr)
        {
            return Enums.FirstOrDefault(f => f.OriginalPtr == ptr);
        }

        public Function FindFunction(string name, bool createDecl = false)
        {
            var function =  Functions.Find(e => e.Name.Equals(name));

            if (function == null && createDecl)
            {
                function = new Function() { Name = name, Namespace = this };
                Functions.Add(function);
            }

            return function;
        }

        public Function FindFunction(IntPtr ptr)
        {
            return Functions
                .Concat(Templates.OfType<FunctionTemplate>()
                    .Select(t => t.TemplatedFunction))
                .FirstOrDefault(f => f.OriginalPtr == ptr);
        }

        public Function FindFunction(Function function)
        {
            return Functions
                .Concat(Templates.OfType<FunctionTemplate>()
                    .Select(t => t.TemplatedFunction))
                .FirstOrDefault(f => FunctionEqualityComparer.Instance.Equals(function, f));
        }

        Class CreateClass(string name, bool isComplete)
        {
            var  @class = new Class
            {
                Name = name,
                Namespace = this,
                IsIncomplete = !isComplete
            };

            return @class;
        }

        public Class FindClass(string name,
            StringComparison stringComparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var entries = name.Split(new[] { "::" },
                StringSplitOptions.RemoveEmptyEntries).ToList();

            if (entries.Count <= 1)
            {
                var @class = Classes.Find(c => c.Name.Equals(name, stringComparison));
                if (@class != null)
                    return @class.CompleteDeclaration == null ?
                        @class : (Class) @class.CompleteDeclaration;
                return null;
            }

            var className = entries[entries.Count - 1];
            var namespaces = entries.Take(entries.Count - 1);

            DeclarationContext declContext = FindDeclaration(namespaces);
            if (declContext == null)
            {
                declContext = FindClass(entries[0]);
                if (declContext == null)
                    return null;
            }

            return declContext.FindClass(className);
        }

        public Class FindClass(string name, bool isComplete,
            bool createDecl = false)
        {
            var @class = FindClass(name);

            if (@class == null)
            {
                if (createDecl)
                {
                    @class = CreateClass(name, isComplete);
                    Classes.Add(@class);
                }

                return @class;
            }

            if (@class.IsIncomplete == !isComplete)
                return @class;

            if (!createDecl)
                return null;

            var newClass = CreateClass(name, isComplete);

            // Replace the incomplete declaration with the complete one.
            if (@class.IsIncomplete)
            {
                var index = Classes.FindIndex(c => c == @class);
                @class.CompleteDeclaration = newClass;
                Classes[index] = newClass;
            }

            return newClass;
        }

        public FunctionTemplate FindFunctionTemplate(string name,
            List<TemplateParameter> @params, Function templatedFunction)
        {
            return Templates.OfType<FunctionTemplate>()
                .FirstOrDefault(template => 
                    template.Name.Equals(name) &&
                    template.Parameters.SequenceEqual(@params) &&
                    FunctionEqualityComparer.Instance.Equals(template.TemplatedFunction, templatedFunction));
        }

        public FunctionTemplate FindFunctionTemplate(IntPtr ptr)
        {
            return Templates.FirstOrDefault(template =>
                template.OriginalPtr == ptr) as FunctionTemplate;
        }

        public ClassTemplate FindClassTemplate(IntPtr ptr)
        {
            return Templates.FirstOrDefault(template =>
                template.OriginalPtr == ptr) as ClassTemplate;
        }

        public ClassTemplate FindClassTemplate(string name, List<TemplateParameter> @params)
        {
            return Templates.OfType<ClassTemplate>()
                .FirstOrDefault(template =>
                    template.Name.Equals(name) &&
                    template.Parameters.SequenceEqual(@params));
        }

        public TypedefDecl FindTypedef(string name, bool createDecl = false)
        {
            var entries = name.Split(new string[] { "::" },
                StringSplitOptions.RemoveEmptyEntries).ToList();

            if (entries.Count <= 1)
            {
                var typeDef = Typedefs.Find(e => e.Name.Equals(name));

                if (typeDef == null && createDecl)
                {
                    typeDef = new TypedefDecl() { Name = name, Namespace = this };
                    Typedefs.Add(typeDef);
                }

                return typeDef;
            }

            var typeDefName = entries[entries.Count - 1];
            var namespaces = entries.Take(entries.Count - 1);

            var @namespace = FindNamespace(namespaces);
            if (@namespace == null)
                return null;

            return @namespace.FindTypedef(typeDefName, createDecl);
        }

        public T FindType<T>(string name) where T : Declaration
        {
            var type = FindEnum(name)
                ?? FindFunction(name)
                ?? (Declaration)FindClass(name)
                ?? FindTypedef(name);

            return type as T;
        }

        public Enumeration FindEnumWithItem(string name)
        {
            var result = Enums.Find(e => e.ItemsByName.ContainsKey(name));
            if (result == null)
                result = Namespaces.Select(ns => ns.FindEnumWithItem(name)).FirstOrDefault();
            if (result == null)
                result = Classes.Select(c => c.FindEnumWithItem(name)).FirstOrDefault();
            return result;
        }

        public virtual IEnumerable<Function> FindOperator(CXXOperatorKind kind)
        {
            return Functions.Where(fn => fn.OperatorKind == kind);
        }

        public virtual IEnumerable<Function> GetOverloads(Function function)
        {
            if (function.IsOperator)
                return FindOperator(function.OperatorKind);
            return Functions.Where(fn => fn.Name == function.Name);
        }

        public bool HasDeclarations
        {
            get
            {
                Predicate<Declaration> pred = (t => !t.Ignore);
                return Enums.Exists(pred) || HasFunctions || Typedefs.Exists(pred)
                    || Classes.Any() || Namespaces.Exists(n => n.HasDeclarations);
            }
        }

        public bool HasFunctions
        {
            get
            {
                Predicate<Declaration> pred = (t => !t.Ignore);
                return Functions.Exists(pred) || Namespaces.Exists(n => n.HasFunctions);
            }
        }

        public bool IsRoot { get { return Namespace == null; } }
    }

    /// <summary>
    /// Represents a C++ namespace.
    /// </summary>
    public class Namespace : DeclarationContext
    {
        public override string LogicalName
        {
            get { return IsInline ? string.Empty : base.Name; }
        }

        public override string LogicalOriginalName
        {
            get { return IsInline ? string.Empty : base.OriginalName; }
        }

        public bool IsInline;

        public override T Visit<T>(IDeclVisitor<T> visitor)
        {
            return visitor.VisitNamespace(this);
        }
    }
}