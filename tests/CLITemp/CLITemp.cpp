#include "CLITemp.h"

int Types::AttributedSum(int A, int B)
{
    return A + B;
}

template<typename T> TestTemplateClass<T>::TestTemplateClass(T v)
{
    this->value = v;
}

template<typename T> T TestTemplateClass<T>::Identity(T x)
{
    return x;
}

TestTemplateClassInt* TestTemplateClass2::CreateIntTemplate()
{
    return new TestTemplateClassInt(10);
}