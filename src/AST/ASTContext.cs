using System;
using System.Collections.Generic;
using System.IO;

namespace CppSharp.AST
{
    public enum CppAbi
    {
        Itanium,
        Microsoft,
        ARM
    }

    /// <summary>
    /// A library contains all the modules.
    /// </summary>
    public class ASTContext
    {
        public List<TranslationUnit> TranslationUnits;

        public ASTContext()
        {
            TranslationUnits = new List<TranslationUnit>();
        }

        /// Finds an existing module or creates a new one given a file path.
        public TranslationUnit FindOrCreateModule(string file)
        {
            try
            {
                file = Path.GetFullPath(file);
            }
            catch (ArgumentException)
            {
                // Normalization errors are expected when dealing with virtual
                // compiler files like <built-in>.
            }
            
            var module = TranslationUnits.Find(m => m.FilePath.Equals(file));

            if (module == null)
            {
                module = new TranslationUnit(file);
                TranslationUnits.Add(module);
            }

            return module;
        }

        /// Finds an existing enum in the library modules.
        public IEnumerable<Enumeration> FindEnum(string name)
        {
            foreach (var module in TranslationUnits)
            {
                var type = module.FindEnum(name);
                if (type != null) yield return type;
            }
        }

        /// Finds the complete declaration of an enum.
        public Enumeration FindCompleteEnum(string name)
        {
            foreach (var @enum in FindEnum(name))
                if (!@enum.IsIncomplete)
                    return @enum;

            return null;
        }

        /// Finds an existing struct/class in the library modules.
        public IEnumerable<Class> FindClass(string name, bool create = false)
        {
            foreach (var module in TranslationUnits)
            {
                var type = module.FindClass(name);
                if (type != null) yield return type;
            }
        }

        /// Finds the complete declaration of a class.
        public Class FindCompleteClass(string name)
        {
            foreach (var @class in FindClass(name))
                if (!@class.IsIncomplete)
                    return @class;

            return null;
        }

        /// Finds an existing function in the library modules.
        public IEnumerable<Function> FindFunction(string name)
        {
            foreach (var module in TranslationUnits)
            {
                var type = module.FindFunction(name);
                if (type != null) yield return type;
            }
        }

        /// Finds an existing typedef in the library modules.
        public IEnumerable<TypedefDecl> FindTypedef(string name)
        {
            foreach (var module in TranslationUnits)
            {
                var type = module.FindTypedef(name);
                if (type != null) yield return type;
            }
        }

        /// Finds an existing declaration by name.
        public IEnumerable<T> FindDecl<T>(string name) where T : Declaration
        {
            foreach (var module in TranslationUnits)
            {
                if (module.FindEnum(name) as T != null)
                    yield return module.FindEnum(name) as T;
                else if (module.FindClass(name) as T != null)
                    yield return module.FindClass(name) as T;
                else if (module.FindFunction(name) as T != null)
                    yield return module.FindFunction(name) as T;
            }
        }

        public void SetEnumAsFlags(string name)
        {
            var enums = FindEnum(name);
            foreach(var @enum in enums)
                @enum.SetFlags();
        }

        public void ExcludeFromPass(string name, System.Type type)
        {
            var decls = FindDecl<Declaration>(name);

            foreach (var decl in decls)
                decl.ExcludeFromPasses.Add(type);
        }

        /// <summary>
        /// Use this to rename namespaces.
        /// </summary>
        public void RenameNamespace(string name, string newName)
        {
            foreach (var unit in TranslationUnits)
            {
                var @namespace = unit.FindNamespace(name);

                if (@namespace != null)
                    @namespace.Name = newName;
            }
        }
    }
}