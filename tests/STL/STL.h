#include "../Tests.h"
#include <vector>
#include <ostream>

struct DLL_API TestVectors
{
    std::vector<int> GetIntVector();
    int SumIntVector(std::vector<int>& vec);

    std::vector<int> IntVector;
};

DLL_API void WriteToOStream(std::ostream& stream, const char* s)
{
    stream << s;
};
