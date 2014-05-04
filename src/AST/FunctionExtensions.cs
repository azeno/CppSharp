using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CppSharp.AST
{
    public class FunctionEqualityComparer : EqualityComparer<Function>
    {
        public static readonly FunctionEqualityComparer Instance = new FunctionEqualityComparer();

        public override bool Equals(Function x, Function y)
        {
            var result = x.Name.Equals(y.Name) &&
                x.ReturnType.Equals(y.ReturnType) &&
                x.IsReturnIndirect == y.IsReturnIndirect &&
                x.Parameters.SequenceEqual(y.Parameters, ParameterEqualityComparer.Instance) &&
                x.IsVariadic == y.IsVariadic &&
                x.IsInline == y.IsInline &&
                x.IsPure == y.IsPure &&
                x.IsDeleted == y.IsDeleted &&
                x.IsAmbiguous == y.IsAmbiguous &&
                x.OperatorKind == y.OperatorKind &&
                x.CallingConvention == y.CallingConvention;
            if (result)
            {
                // Check for methods
                var mx = x as Method;
                var my = y as Method;
                if (mx != null)
                    if (my != null)
                        return Equals(mx, my);
                    else
                        return false;
                else
                    if (my != null)
                        return false;
            }
            return result;
        }

        private bool Equals(Method x, Method y)
        {
            var result = x.IsVirtual == y.IsVirtual &&
                x.IsStatic == y.IsStatic &&
                x.IsConst == y.IsConst &&
                x.IsImplicit == y.IsImplicit &&
                x.IsExplicit == y.IsExplicit &&
                x.IsSynthetized == y.IsSynthetized &&
                x.IsOverride == y.IsOverride &&
                x.IsProxy == y.IsProxy &&
                x.Kind == y.Kind &&
                x.IsDefaultConstructor == y.IsDefaultConstructor &&
                x.IsCopyConstructor == y.IsCopyConstructor &&
                x.IsMoveConstructor == y.IsMoveConstructor &&
                x.Conversion == y.Conversion &&
                x.ExplicitInterfaceImpl == y.ExplicitInterfaceImpl;
            if (result)
            {
                if (x.ConversionType.Type != null)
                    if (y.ConversionType.Type != null)
                        return x.ConversionType.Equals(y.ConversionType);
                    else
                        return false;
                else
                    if (y.ConversionType.Type != null)
                        return false;
            }
            return result;
        }

        public override int GetHashCode(Function obj)
        {
            return obj.Name.GetHashCode();
        }
    }

    public class ParameterEqualityComparer : EqualityComparer<Parameter>
    {
        public static readonly ParameterEqualityComparer Instance = new ParameterEqualityComparer();

        public override bool Equals(Parameter x, Parameter y)
        {
            // Don't check the IsIndirect property - gets set at a later point by the parser
            return x.Name.Equals(y.Name) &&
                x.Index == y.Index &&
                x.Kind == y.Kind &&
                x.Usage == y.Usage &&
                x.QualifiedType.Equals(y.QualifiedType);
        }

        public override int GetHashCode(Parameter obj)
        {
            return obj.Name.GetHashCode();
        }
    }
}
