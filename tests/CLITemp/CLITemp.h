#include "../Tests.h"

#include <vector>
#include <ostream>

// Tests for C++ types
struct DLL_API Types
{
    // AttributedType
#ifdef __clang__
#define ATTR __attribute__((stdcall))
#else
#define ATTR
#endif

    // Note: This fails with C# currently due to mangling bugs.
    // Move it back once it's fixed upstream.
    typedef int AttributedFuncType(int, int) ATTR;
    AttributedFuncType AttributedSum;
};

// Tests the insertion operator (<<) to ToString method pass
class DLL_API Date
{
public:
    Date(int m, int d, int y)
    {
        mo = m; da = d; yr = y;
    }
    // Not picked up by parser yet
    //friend std::ostream& operator<<(std::ostream& os, const Date& dt);
    int mo, da, yr;
};

std::ostream& operator<<(std::ostream& os, const Date& dt)
{
    os << dt.mo << '/' << dt.da << '/' << dt.yr;
    return os;
}

// Tests function templates
class DLL_API ClassContainingTemplateFunctions
{
public:
    // Template function
    template<typename T> T Identity(T x) { return x; }
    // Should get removed by duplicate name pass (const overload)
    template<typename T> T Identity(const T x) const { return x; }
    // This should be ignored
    template<int N> void Ignore() { };
    // And also this
    template<typename T, typename S> void Ignore() { };
    // And this
    template<typename T> void Ignore(std::vector<T> v) { };
    // But not this
    template<typename T> T Valid(std::vector<int> v, T x) { return x; };
    // Should also work
    template<typename T> T& Valid(std::vector<int> v, T x) const { return x; }
};

// Tests class templates
template<typename T>
class DLL_API TestTemplateClass
{
public:
    TestTemplateClass(T v);
    T Identity(T x);
    T value;
};

// Explicit instantiation
template class TestTemplateClass<int>;
// Implicit instantiation
typedef TestTemplateClass<int> TestTemplateClassInt;

// Now use the typedef
class DLL_API TestTemplateClass2
{
public:
    TestTemplateClassInt* CreateIntTemplate();
};